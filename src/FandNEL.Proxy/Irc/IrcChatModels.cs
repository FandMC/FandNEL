using System.Text.Json.Serialization;

namespace FandNEL.Proxy.Irc;

internal sealed record IrcMessage(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("sender")] string Sender,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("isIrc")] bool IsIrc);

internal sealed record IrcPollRequest(
    [property: JsonPropertyName("lastId")] long LastId);

internal sealed record IrcSendRequest(
    [property: JsonPropertyName("text")] string Text);

internal sealed record IrcJsonTextComponent(
    [property: JsonPropertyName(IrcConstants.TextComponentField)] string Text);

internal sealed record IrcSendResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("message")] string? Message);

internal sealed record IrcPollResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("messages")] IReadOnlyList<IrcMessage>? Messages,
    [property: JsonPropertyName("online")] int Online,
    [property: JsonPropertyName("message")] string? Message);

internal sealed record IrcPollResult(bool Success, IReadOnlyList<IrcMessage> Messages, int Online)
{
    internal static readonly IrcPollResult Failure = new(false, [], 0);
}

internal sealed record IrcSendResult(bool Success, string Message);
