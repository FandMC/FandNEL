namespace FandNEL.Proxy.Heypixel;

public static class ListShuffleExtensions
{
    public static void Shuffle<T>(this List<T> values, Random random)
    {
        for (int index = values.Count - 1; index > 0; index--)
        {
            int replacementIndex = random.Next(index + 1);
            (values[index], values[replacementIndex]) = (values[replacementIndex], values[index]);
        }
    }
}
