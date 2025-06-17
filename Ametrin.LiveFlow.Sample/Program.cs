using Ametrin.LiveFlow;

var dataSource = new MemoryDataSource<string>([.. Enumerable.Range(0, 100_000).Select(static _ => Guid.CreateVersion7().ToString())]);

// creating a new paged cache
using var cache = new PagedCache<string>(dataSource, new() { PageSize = 10, MaxPagesInCache = 3 });

// requesting the first element. As it is not in cache yet, this will fetch the first page.
var firstElement = (await cache.TryGetValueAsync(0)).OrThrow(); // we know it exists so we just throw on error

// since the first 10 elements are now in cache we can access them directly
var secondElement = cache.TryGetValueFromCache(1).OrThrow();

(await cache.TryGetValueAsync(10)).OrThrow();
(await cache.TryGetValueAsync(20)).OrThrow();

// loading a forth page will remove the first page from the cache (see MaxPagesInCache)
var fourthPage = (await cache.TryGetValueAsync(30)).OrThrow();

cache.IsInCache(0);

cache.SourceChanged += (sender, args) =>
{
    // listen to source changes that affect the cache
};
