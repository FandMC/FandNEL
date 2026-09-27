package domain

import "time"

type Message struct {
	ID        int64     `json:"id"`
	Sender    string    `json:"sender"`
	Text      string    `json:"text"`
	IsIRC     bool      `json:"isIrc"`
	CreatedAt time.Time `json:"createdAt"`
}

type Page struct {
	Items      []Message
	NextCursor int64
}
