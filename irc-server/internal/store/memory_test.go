package store

import "testing"

func TestMessageStoreSupportsCursorAndIdempotency(t *testing.T) {
	messages, err := NewMessageStore("", 2)
	if err != nil {
		t.Fatal(err)
	}
	first, err := messages.Append("alice", "hello", false, "client-1")
	if err != nil {
		t.Fatal(err)
	}
	replayed, err := messages.Append("alice", "changed", false, "client-1")
	if err != nil {
		t.Fatal(err)
	}
	if replayed.ID != first.ID || replayed.Text != first.Text {
		t.Fatalf("idempotent replay returned %+v, want %+v", replayed, first)
	}
	if _, err := messages.Append("bob", "second", true, ""); err != nil {
		t.Fatal(err)
	}
	if _, err := messages.Append("carol", "third", true, ""); err != nil {
		t.Fatal(err)
	}
	page, err := messages.ReadAfter(first.ID, 10)
	if err != nil {
		t.Fatal(err)
	}
	if len(page.Items) != 2 || page.Items[0].Sender != "bob" || page.Items[1].Sender != "carol" {
		t.Fatalf("unexpected page: %+v", page.Items)
	}
}
