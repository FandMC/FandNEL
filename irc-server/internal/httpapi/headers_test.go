package httpapi

import "testing"

func TestDecodeHeaderValue(t *testing.T) {
	tests := map[string]string{
		"Steve":              "Steve",
		"%E7%8E%A9%E5%AE%B6": "玩家",
		"A%2BB":              "A+B",
		"bad%2":              "bad%2",
	}
	for encoded, expected := range tests {
		if actual := decodeHeaderValue(encoded); actual != expected {
			t.Fatalf("decodeHeaderValue(%q) = %q, want %q", encoded, actual, expected)
		}
	}
}
