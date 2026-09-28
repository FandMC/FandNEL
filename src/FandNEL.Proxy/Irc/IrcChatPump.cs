using Serilog;

namespace FandNEL.Proxy.Irc;

/// <summary>负责 IRC 增量轮询、首次游标对齐和周期提示。</summary>
internal sealed class IrcChatPump(
    IrcChatOptions options,
    IrcChatSession session,
    IrcChatDelivery delivery,
    Func<IReadOnlyList<IrcMessage>, Task> relay)
{
    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        var primed = false;
        var nextOnlineHint = DateTimeOffset.UtcNow.Add(IrcConstants.OnlineHintInterval);

        while (!cancellationToken.IsCancellationRequested)
        {
            var wait = options.PollInterval;
            try
            {
                if (delivery.IsEmpty)
                    wait = IrcConstants.NoConnectionsDelay;
                else
                {
                    var poll = await session.PollAsync(cancellationToken).ConfigureAwait(false);
                    if (!poll.Success)
                        wait = IrcConstants.PollFailureDelay;
                    else
                    {
                        if (primed)
                            await relay(poll.Messages).ConfigureAwait(false);
                        primed = true;
                        var now = DateTimeOffset.UtcNow;
                        if (options.ShowOnlineHint && now >= nextOnlineHint)
                        {
                            nextOnlineHint = now.Add(IrcConstants.OnlineHintInterval);
                            await delivery.BroadcastAsync(string.Format(IrcConstants.OnlineHintFormat, poll.Online))
                                .ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Log.Warning(exception, "IRC: unexpected poll loop error");
                wait = IrcConstants.UnexpectedPollFailureDelay;
            }

            if (cancellationToken.IsCancellationRequested)
                break;
            await DelayAsync(wait, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
