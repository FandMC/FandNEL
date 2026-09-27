package config

import (
	"fmt"
	"os"
	"strconv"
	"time"
)

type Config struct {
	ListenAddr       string
	DataFile         string
	MaxHistory       int
	MaxMessageLength int
	PresenceTTL      time.Duration
	MaxPageSize      int
}

func Load() (Config, error) {
	cfg := Config{
		ListenAddr:       envString("IRC_LISTEN_ADDR", ":5127"),
		DataFile:         envString("IRC_DATA_FILE", "data/messages.json"),
		MaxHistory:       envInt("IRC_MAX_HISTORY", 1000),
		MaxMessageLength: envInt("IRC_MAX_MESSAGE_LENGTH", 256),
		PresenceTTL:      envDuration("IRC_PRESENCE_TTL", 45*time.Second),
		MaxPageSize:      envInt("IRC_MAX_PAGE_SIZE", 100),
	}
	if cfg.MaxHistory < 1 || cfg.MaxMessageLength < 1 || cfg.MaxPageSize < 1 {
		return Config{}, fmt.Errorf("history, message length and page size must be positive")
	}
	if cfg.PresenceTTL <= 0 {
		return Config{}, fmt.Errorf("IRC_PRESENCE_TTL must be positive")
	}
	return cfg, nil
}

func envString(key, fallback string) string {
	if value, ok := os.LookupEnv(key); ok {
		return value
	}
	return fallback
}

func envInt(key string, fallback int) int {
	value, ok := os.LookupEnv(key)
	if !ok {
		return fallback
	}
	parsed, err := strconv.Atoi(value)
	if err != nil {
		return fallback
	}
	return parsed
}

func envDuration(key string, fallback time.Duration) time.Duration {
	value, ok := os.LookupEnv(key)
	if !ok {
		return fallback
	}
	parsed, err := time.ParseDuration(value)
	if err != nil {
		return fallback
	}
	return parsed
}
