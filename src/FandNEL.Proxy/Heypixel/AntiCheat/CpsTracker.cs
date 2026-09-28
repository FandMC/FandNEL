using System.Threading;

namespace FandNEL.Proxy.Heypixel;

public sealed class CpsTracker
{
    private readonly Queue<long> leftClickTimestamps = new();
    private readonly Queue<long> rightClickTimestamps = new();
    private readonly Lock syncRoot = new();

    public void RecordLeftClick(int ignoredValue)
    {
        RecordClick(isLeftClick: true);
    }

    public void RecordLeftClick()
    {
        RecordClick(isLeftClick: true);
    }

    public void RecordRightClick()
    {
        RecordClick(isLeftClick: false);
    }

    public int GetLeftCps()
    {
        return GetCurrentCount(leftClickTimestamps);
    }

    public int GetRightCps()
    {
        return GetCurrentCount(rightClickTimestamps);
    }

    public double GetLeftClicksPerSecond()
    {
        return GetCurrentCount(leftClickTimestamps);
    }

    public double GetRightClicksPerSecond()
    {
        return GetCurrentCount(rightClickTimestamps);
    }

    public int CountClicks(bool isLeftClick, int intervalMilliseconds)
    {
        using (syncRoot.EnterScope())
        {
            Queue<long> timestamps = isLeftClick ? leftClickTimestamps : rightClickTimestamps;
            long cutoff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - intervalMilliseconds;
            return timestamps.Count(timestamp => timestamp >= cutoff);
        }
    }

    private void RecordClick(bool isLeftClick)
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        using (syncRoot.EnterScope())
        {
            Queue<long> timestamps = isLeftClick ? leftClickTimestamps : rightClickTimestamps;
            timestamps.Enqueue(timestamp);
            RemoveExpired(timestamps);
        }
    }

    private int GetCurrentCount(Queue<long> timestamps)
    {
        using (syncRoot.EnterScope())
        {
            RemoveExpired(timestamps);
            return timestamps.Count;
        }
    }

    private static void RemoveExpired(Queue<long> timestamps)
    {
        long cutoff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 1000;
        while (timestamps.Count > 0 && timestamps.Peek() < cutoff)
        {
            timestamps.Dequeue();
        }
    }
}
