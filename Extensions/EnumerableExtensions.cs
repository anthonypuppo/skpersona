namespace SKPersona.Extensions;

public static class EnumerableExtensions
{
    public static IEnumerable<TSource> TakeWhileAggregate<TSource, TAccumulate>(
        this IEnumerable<TSource> source,
        TAccumulate seed,
        Func<TAccumulate, TSource, TAccumulate> aggregator,
        Func<TAccumulate, bool> predicate)
    {
        using var enumerator = source.GetEnumerator();
        var aggregate = seed;

        while (enumerator.MoveNext())
        {
            var item = enumerator.Current;

            aggregate = aggregator(aggregate, item);

            if (predicate(aggregate))
            {
                yield return item;
            }
            else
            {
                yield break;
            }
        }
    }
}
