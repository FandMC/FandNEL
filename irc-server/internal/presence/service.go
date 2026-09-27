package presence

import (
	"crypto/rand"
	"encoding/hex"
	"strings"
	"sync"
	"time"
)

type Client struct {
	ID       string
	GameID   string
	LastSeen time.Time
}

type Service struct {
	mu      sync.Mutex
	ttl     time.Duration
	clients map[string]Client
}

func NewService(ttl time.Duration) *Service {
	return &Service{ttl: ttl, clients: make(map[string]Client)}
}

func NewClientID() string {
	bytes := make([]byte, 16)
	if _, err := rand.Read(bytes); err != nil {
		return hex.EncodeToString([]byte(time.Now().UTC().String()))
	}
	return hex.EncodeToString(bytes)
}

func (s *Service) Touch(clientID, gameID string) Client {
	clientID = strings.TrimSpace(clientID)
	if clientID == "" {
		clientID = NewClientID()
	}
	now := time.Now().UTC()
	if gameID == "" {
		gameID = "unknown"
	}
	s.mu.Lock()
	defer s.mu.Unlock()
	s.pruneLocked(now)
	client := Client{ID: clientID, GameID: strings.TrimSpace(gameID), LastSeen: now}
	s.clients[clientID] = client
	return client
}

func (s *Service) Online() int {
	s.mu.Lock()
	defer s.mu.Unlock()
	s.pruneLocked(time.Now().UTC())
	return len(s.clients)
}

func (s *Service) pruneLocked(now time.Time) {
	for id, client := range s.clients {
		if now.Sub(client.LastSeen) > s.ttl {
			delete(s.clients, id)
		}
	}
}
