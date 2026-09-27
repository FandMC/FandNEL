package store

import (
	"encoding/json"
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"strings"
	"sync"
	"time"

	"fandnel/irc-server/internal/domain"
)

var ErrCursorExpired = errors.New("message cursor expired")

type snapshot struct {
	NextID   int64            `json:"nextId"`
	Messages []domain.Message `json:"messages"`
}

type MessageStore struct {
	mu       sync.RWMutex
	dataFile string
	max      int
	nextID   int64
	messages []domain.Message
	byClient map[string]domain.Message
}

func NewMessageStore(dataFile string, max int) (*MessageStore, error) {
	store := &MessageStore{dataFile: dataFile, max: max, nextID: 1, byClient: make(map[string]domain.Message)}
	if dataFile == "" {
		return store, nil
	}
	contents, err := os.ReadFile(dataFile)
	if errors.Is(err, os.ErrNotExist) {
		return store, nil
	}
	if err != nil {
		return nil, err
	}
	var saved snapshot
	if err := json.Unmarshal(contents, &saved); err != nil {
		return nil, fmt.Errorf("decode message store: %w", err)
	}
	store.nextID = saved.NextID
	if store.nextID < 1 {
		store.nextID = 1
	}
	if len(saved.Messages) > max {
		saved.Messages = saved.Messages[len(saved.Messages)-max:]
	}
	store.messages = append(store.messages, saved.Messages...)
	return store, nil
}

func (s *MessageStore) Append(sender, text string, isIRC bool, clientMessageID string) (domain.Message, error) {
	clientMessageID = strings.TrimSpace(clientMessageID)
	s.mu.Lock()
	if clientMessageID != "" {
		if existing, ok := s.byClient[clientMessageID]; ok {
			s.mu.Unlock()
			return existing, nil
		}
	}
	message := domain.Message{ID: s.nextID, Sender: sender, Text: text, IsIRC: isIRC, CreatedAt: time.Now().UTC()}
	s.nextID++
	s.messages = append(s.messages, message)
	if clientMessageID != "" {
		s.byClient[clientMessageID] = message
	}
	if len(s.messages) > s.max {
		s.messages = s.messages[len(s.messages)-s.max:]
	}
	snapshot := snapshot{NextID: s.nextID, Messages: append([]domain.Message(nil), s.messages...)}
	s.mu.Unlock()
	if err := s.persist(snapshot); err != nil {
		return message, err
	}
	return message, nil
}

func (s *MessageStore) ReadAfter(cursor int64, limit int) (domain.Page, error) {
	s.mu.RLock()
	defer s.mu.RUnlock()
	if len(s.messages) > 0 && cursor < s.messages[0].ID-1 {
		return domain.Page{}, ErrCursorExpired
	}
	items := make([]domain.Message, 0, limit)
	for _, message := range s.messages {
		if message.ID > cursor {
			items = append(items, message)
			if len(items) == limit {
				break
			}
		}
	}
	next := cursor
	if len(items) > 0 {
		next = items[len(items)-1].ID
	}
	return domain.Page{Items: items, NextCursor: next}, nil
}

func (s *MessageStore) CurrentCursor() int64 {
	s.mu.RLock()
	defer s.mu.RUnlock()
	return s.nextID - 1
}

func (s *MessageStore) persist(value snapshot) error {
	if s.dataFile == "" {
		return nil
	}
	if err := os.MkdirAll(filepath.Dir(s.dataFile), 0o750); err != nil {
		return err
	}
	contents, err := json.Marshal(value)
	if err != nil {
		return err
	}
	temporary := s.dataFile + ".tmp"
	if err := os.WriteFile(temporary, contents, 0o640); err != nil {
		return err
	}
	return os.Rename(temporary, s.dataFile)
}
