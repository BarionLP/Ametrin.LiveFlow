using System.Collections.ObjectModel;
using Ametrin.Optional.Testing.TUnit;

namespace Ametrin.LiveFlow.Test;

public sealed class PagedCacheTests
{
    [Test]
    public async Task Works()
    {
        const int DATA_SIZE = 1_000;
        const int PAGE_SIZE = 128;

        ObservableCollection<string> data = [.. Enumerable.Range(0, DATA_SIZE).Select(static i => Guid.NewGuid().ToString())];
        var cache = new PagedCache<string>(new MemoryDataSource<string>(data), new() { PageSize = PAGE_SIZE, MaxPagesInCache = 3 });

        await Assert.That(cache.IsInCache(0)).IsFalse();
        await Assert.That(cache.IsInCache(DATA_SIZE / 2)).IsFalse();
        await Assert.That(cache.IsInCache(DATA_SIZE - 1)).IsFalse();
        await Assert.That(cache.IsInCache(DATA_SIZE)).IsFalse();
        await Assert.That(cache.TryGetValueFromCache(DATA_SIZE - 4)).IsError();
        await Assert.That(cache.TryGetValueFromCache(12)).IsError();

        await Assert.That(cache.TryGetValueAsync(0)).IsSuccess(data[0]);
        await Assert.That(cache.IsInCache(0)).IsTrue();
        await Assert.That(cache.TryGetValueFromCache(PAGE_SIZE - 1)).IsSuccess(data[PAGE_SIZE - 1]);
        await Assert.That(cache.IsInCache(PAGE_SIZE)).IsFalse();
        await Assert.That(cache.TryGetValueAsync(PAGE_SIZE)).IsSuccess(data[PAGE_SIZE]);
        await Assert.That(cache.IsInCache(PAGE_SIZE)).IsTrue();

        await Assert.That(await cache.TryGetValueAsync(DATA_SIZE)).IsError(); // here we try loading it in cache
        await Assert.That(await cache.TryGetValueAsync(DATA_SIZE)).IsError(); // here we test the cache hit index checks
        await Assert.That(cache.TryGetValueFromCache(DATA_SIZE - 1)).IsSuccess(data[DATA_SIZE - 1]);
        await Assert.That(await cache.TryGetValueAsync(DATA_SIZE + PAGE_SIZE)).IsError();

        cache.ClearCache();
        await Assert.That(cache.PagePool.Count).IsEqualTo(3);
        await Assert.That(cache.TryGetValueFromCache(DATA_SIZE - 1)).IsError();


        await Assert.That(cache.TryGetValueAsync(0)).IsSuccess(data[0]);
        await Assert.That(cache.RequestHistory.GetMostRecent()).IsEqualTo(0);

        await Assert.That(cache.TryGetValueAsync(PAGE_SIZE)).IsSuccess(data[PAGE_SIZE]);
        await Assert.That(cache.RequestHistory.GetMostRecent()).IsEqualTo(1);

        await Assert.That(cache.TryGetValueAsync(PAGE_SIZE * 2)).IsSuccess(data[PAGE_SIZE * 2]);
        await Assert.That(cache.RequestHistory.GetMostRecent()).IsEqualTo(2);
        await Assert.That(cache.RequestHistory.GetLeastRecent()).IsEqualTo(0);
        await Assert.That(cache.PagePool.Count).IsEqualTo(0);

        await Assert.That(cache.TryGetValueAsync(PAGE_SIZE * 3)).IsSuccess(data[PAGE_SIZE * 3]);
        await Assert.That(cache.RequestHistory.GetMostRecent()).IsEqualTo(3);
        await Assert.That(cache.RequestHistory.GetLeastRecent()).IsEqualTo(1);
        await Assert.That(cache.TryGetValueFromCache(0)).IsError();
        await Assert.That(cache.PagePool.Count).IsEqualTo(0);

        await Assert.That(cache.Dispose).ThrowsNothing();
    }

    [Test]
    public async Task HandlesConcurrentRequest()
    {
        const int DATA_SIZE = 1_000;
        const int PAGE_SIZE = 128;

        ObservableCollection<string> data = [.. Enumerable.Range(0, DATA_SIZE).Select(static i => Guid.NewGuid().ToString())];
        var cache = new PagedCache<string>(new FakeDataSource<string>(data, new() { Delay = TimeSpan.FromMilliseconds(200), MaxConcurrentConnections = 2 }), new() { PageSize = PAGE_SIZE, MaxPagesInCache = 3 });

        var task1 = cache.TryGetValueAsync(0);
        var task2 = cache.TryGetValueAsync(PAGE_SIZE);
        var task5 = cache.TryGetValueAsync(PAGE_SIZE);
        var task3 = cache.TryGetValueAsync(PAGE_SIZE + 1);
        var task4 = cache.TryGetValueAsync(PAGE_SIZE * 2);

        await Assert.That(task1).IsSuccess(data[0]);
        await Assert.That(task2).IsSuccess(data[PAGE_SIZE]);
        await Assert.That(task3).IsSuccess(data[PAGE_SIZE + 1]);
        await Assert.That(task4).IsSuccess(data[PAGE_SIZE * 2]);
        await Assert.That(task5).IsSuccess(data[PAGE_SIZE]);

        await Assert.That(cache.Dispose).ThrowsNothing();
    }
}
