package httpapi

import (
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"log/slog"
	"net/http"
	"strconv"
	"strings"
	"unicode/utf8"

	"fandnel/irc-server/internal/config"
	"fandnel/irc-server/internal/domain"
	"fandnel/irc-server/internal/presence"
	"fandnel/irc-server/internal/store"
)

const (
	gameIDHeader   = "X-Game-ID"
	clientIDHeader = "X-Client-ID"
	maxJSONBody    = 32 * 1024
)

type Server struct {
	cfg      config.Config
	presence *presence.Service
	messages *store.MessageStore
	logger   *slog.Logger
}

func NewServer(cfg config.Config, presenceService *presence.Service, messages *store.MessageStore, logger *slog.Logger) *Server {
	return &Server{cfg: cfg, presence: presenceService, messages: messages, logger: logger}
}

func (s *Server) Handler() http.Handler {
	mux := http.NewServeMux()
	mux.HandleFunc("GET /healthz", s.health)
	mux.HandleFunc("POST /api/v1/session", s.v1Session)
	mux.HandleFunc("GET /api/v1/chat/messages", s.v1Messages)
	mux.HandleFunc("POST /api/v1/chat/messages", s.v1Send)
	// 旧路径是兼容层，业务逻辑仍然走同一套匿名会话和消息存储。
	mux.HandleFunc("POST /api/auth/login", s.legacyLogin)
	mux.HandleFunc("POST /api/chat/poll", s.legacyPoll)
	mux.HandleFunc("POST /api/chat/send", s.legacySend)
	return requestLogger(s.logger, mux)
}

type loginRequest struct {
	GameID   string `json:"gameId"`
	ClientID string `json:"clientId"`
}

type sessionResponse struct {
	ClientID      string `json:"clientId"`
	GameID        string `json:"gameId"`
	InitialCursor int64  `json:"initialCursor"`
}

type v1SendRequest struct {
	Text            string `json:"text"`
	ClientMessageID string `json:"clientMessageId"`
}

type v1MessagePage struct {
	Items      []domain.Message `json:"items"`
	NextCursor int64            `json:"nextCursor"`
	Online     int              `json:"online"`
}

type legacyLoginResponse struct {
	Success      bool   `json:"success"`
	SessionToken string `json:"sessionToken,omitempty"`
	Message      string `json:"message,omitempty"`
}

type legacyPollRequest struct {
	LastID int64 `json:"lastId"`
}

type legacyMessage struct {
	ID     int64  `json:"id"`
	Sender string `json:"sender"`
	Text   string `json:"text"`
	IsIRC  bool   `json:"isIrc"`
}

type legacyPollResponse struct {
	Success  bool            `json:"success"`
	Messages []legacyMessage `json:"messages,omitempty"`
	Online   int             `json:"online"`
	Disabled bool            `json:"disabled"`
	Message  string          `json:"message,omitempty"`
}

type legacySendRequest struct {
	Text string `json:"text"`
}

type legacySendResponse struct {
	Success bool   `json:"success"`
	Message string `json:"message,omitempty"`
}

func (s *Server) health(w http.ResponseWriter, r *http.Request) {
	writeJSON(w, http.StatusOK, map[string]string{"status": "ok"})
}

func (s *Server) v1Session(w http.ResponseWriter, r *http.Request) {
	var request loginRequest
	if !decodeJSON(w, r, &request) {
		return
	}
	client := s.presence.Touch(request.ClientID, request.GameID)
	writeJSON(w, http.StatusOK, sessionResponse{
		ClientID: client.ID, GameID: client.GameID, InitialCursor: s.messages.CurrentCursor(),
	})
}

func (s *Server) v1Messages(w http.ResponseWriter, r *http.Request) {
	s.touchPresence(r)
	cursor, limit, ok := parsePageQuery(w, r, s.cfg.MaxPageSize)
	if !ok {
		return
	}
	page, err := s.messages.ReadAfter(cursor, limit)
	if errors.Is(err, store.ErrCursorExpired) {
		writeProblem(w, http.StatusConflict, "cursor_expired", "消息游标已过期，请从新的游标开始。")
		return
	}
	if err != nil {
		writeProblem(w, http.StatusInternalServerError, "read_failed", "读取消息失败。")
		return
	}
	writeJSON(w, http.StatusOK, v1MessagePage{Items: page.Items, NextCursor: page.NextCursor, Online: s.presence.Online()})
}

func (s *Server) v1Send(w http.ResponseWriter, r *http.Request) {
	client := s.touchPresence(r)
	var request v1SendRequest
	if !decodeJSON(w, r, &request) {
		return
	}
	text, err := validateText(request.Text, s.cfg.MaxMessageLength)
	if err != nil {
		writeProblem(w, http.StatusBadRequest, "invalid_message", err.Error())
		return
	}
	message, err := s.messages.Append(client.GameID, text, false, request.ClientMessageID)
	if err != nil {
		writeProblem(w, http.StatusInternalServerError, "write_failed", "保存消息失败。")
		return
	}
	writeJSON(w, http.StatusCreated, message)
}

func (s *Server) legacyLogin(w http.ResponseWriter, r *http.Request) {
	var request map[string]any
	if !decodeJSON(w, r, &request) {
		return
	}
	writeJSON(w, http.StatusOK, legacyLoginResponse{Success: true})
}

func (s *Server) legacyPoll(w http.ResponseWriter, r *http.Request) {
	s.touchPresence(r)
	var request legacyPollRequest
	if !decodeJSON(w, r, &request) {
		return
	}
	page, err := s.messages.ReadAfter(request.LastID, s.cfg.MaxPageSize)
	if errors.Is(err, store.ErrCursorExpired) {
		// 旧客户端无法处理游标冲突。返回保留窗口中的消息，让它把游标推进到最新值。
		page, err = s.messages.ReadAfter(0, s.cfg.MaxPageSize)
	}
	if err != nil {
		writeJSON(w, http.StatusInternalServerError, legacyPollResponse{Message: "读取消息失败。"})
		return
	}
	messages := make([]legacyMessage, 0, len(page.Items))
	for _, message := range page.Items {
		messages = append(messages, legacyMessage{ID: message.ID, Sender: message.Sender, Text: message.Text, IsIRC: message.IsIRC})
	}
	writeJSON(w, http.StatusOK, legacyPollResponse{Success: true, Messages: messages, Online: s.presence.Online()})
}

func (s *Server) legacySend(w http.ResponseWriter, r *http.Request) {
	var request legacySendRequest
	if !decodeJSON(w, r, &request) {
		return
	}
	client := s.touchPresence(r)
	text, err := validateText(strings.TrimPrefix(request.Text, "/IRC "), s.cfg.MaxMessageLength)
	if err != nil {
		writeJSON(w, http.StatusBadRequest, legacySendResponse{Message: err.Error()})
		return
	}
	if _, err := s.messages.Append(client.GameID, text, true, ""); err != nil {
		writeJSON(w, http.StatusInternalServerError, legacySendResponse{Message: "保存消息失败。"})
		return
	}
	writeJSON(w, http.StatusOK, legacySendResponse{Success: true})
}

func (s *Server) touchPresence(r *http.Request) presence.Client {
	return s.presence.Touch(r.Header.Get(clientIDHeader), r.Header.Get(gameIDHeader))
}

func parsePageQuery(w http.ResponseWriter, r *http.Request, maxLimit int) (int64, int, bool) {
	cursor, err := parseNonNegativeInt64(r.URL.Query().Get("after"))
	if err != nil {
		writeProblem(w, http.StatusBadRequest, "invalid_cursor", "游标必须是非负整数。")
		return 0, 0, false
	}
	limit := 50
	if raw := r.URL.Query().Get("limit"); raw != "" {
		limit, err = strconv.Atoi(raw)
		if err != nil || limit < 1 || limit > maxLimit {
			writeProblem(w, http.StatusBadRequest, "invalid_limit", fmt.Sprintf("limit 必须在 1 到 %d 之间。", maxLimit))
			return 0, 0, false
		}
	}
	return cursor, limit, true
}

func parseNonNegativeInt64(raw string) (int64, error) {
	if raw == "" {
		return 0, nil
	}
	value, err := strconv.ParseInt(raw, 10, 64)
	if err != nil || value < 0 {
		return 0, errors.New("invalid cursor")
	}
	return value, nil
}

func validateText(text string, maxLength int) (string, error) {
	text = strings.TrimSpace(text)
	if text == "" {
		return "", errors.New("消息不能为空。")
	}
	if !utf8.ValidString(text) {
		return "", errors.New("消息必须是有效的 UTF-8。")
	}
	if utf8.RuneCountInString(text) > maxLength {
		return "", fmt.Errorf("消息不能超过 %d 个字符。", maxLength)
	}
	return text, nil
}

func decodeJSON(w http.ResponseWriter, r *http.Request, target any) bool {
	if r.Method != http.MethodPost {
		writeProblem(w, http.StatusMethodNotAllowed, "method_not_allowed", "请求方法不支持。")
		return false
	}
	r.Body = http.MaxBytesReader(w, r.Body, maxJSONBody)
	decoder := json.NewDecoder(r.Body)
	if err := decoder.Decode(target); err != nil {
		writeProblem(w, http.StatusBadRequest, "invalid_json", "请求体不是有效 JSON。")
		return false
	}
	var extra any
	if err := decoder.Decode(&extra); err != io.EOF {
		writeProblem(w, http.StatusBadRequest, "invalid_json", "请求体只能包含一个 JSON 对象。")
		return false
	}
	return true
}

func requestLogger(logger *slog.Logger, next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		writer := &statusWriter{ResponseWriter: w}
		next.ServeHTTP(writer, r)
		logger.Info("http request", "method", r.Method, "path", r.URL.Path, "status", writer.status)
	})
}

type statusWriter struct {
	http.ResponseWriter
	status int
}

func (w *statusWriter) WriteHeader(status int) {
	w.status = status
	w.ResponseWriter.WriteHeader(status)
}

func (w *statusWriter) Write(body []byte) (int, error) {
	if w.status == 0 {
		w.status = http.StatusOK
	}
	return w.ResponseWriter.Write(body)
}

func writeJSON(w http.ResponseWriter, status int, value any) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.WriteHeader(status)
	_ = json.NewEncoder(w).Encode(value)
}

func writeProblem(w http.ResponseWriter, status int, code, message string) {
	writeJSON(w, status, map[string]string{"code": code, "message": message})
}
