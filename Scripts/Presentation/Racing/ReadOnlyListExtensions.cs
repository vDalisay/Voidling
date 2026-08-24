using System.Collections.Generic;

namespace Voidling.Presentation.Racing;

internal static class ReadOnlyListExtensions
{
    public static int IndexOf<T>(this IReadOnlyList<T> source, T value)
    {
        var comparer = EqualityComparer<T>.Default;
        for (var i = 0; i < source.Count; i++)
        {
            if (comparer.Equals(source[i], value))
                return i;
        }

        return -1;
    }
}
