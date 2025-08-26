using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Threading;

namespace Ametrin.LiveFlow;

public sealed class PagedCache<T> : IDisposable
{
    private readonly IPageableDataSource<T> dataSource;
    internal readonly Dictionary<int, Page> Cache;
    internal readonly LRUSet RequestHistory;
    internal readonly Stack<T[]> PagePool;
    private readonly ReaderWriterLockSlim @lock = new(LockRecursionPolicy.SupportsRecursion);

    public event NotifyCollectionChangedEventHandler? SourceChanged;

    public PagedCacheConfig Config { get; }

    public PagedCache(IPageableDataSource<T> dataSource, PagedCacheConfig config)
    {
        this.dataSource = dataSource;
        Cache = new(capacity: config.MaxPagesInCache);
        RequestHistory = new(capacity: config.MaxPagesInCache);
        PagePool = new(capacity: config.MaxPagesInCache);
        Config = config;

        if (dataSource is INotifyCollectionChanged notifyChanged)
        {
            notifyChanged.CollectionChanged += OnSourceChanged;
        }
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Replace:
                {
                    if (e.NewItems is not [T newElement])
                    {
                        throw new ArgumentException("Unexpected format of Replace action", nameof(e));
                    }

                    var (pageNumber, offset) = GetPageNumberAndItemOffset(e.NewStartingIndex);

                    if (Cache.TryGetValue(pageNumber, out var page))
                    {
                        if (offset >= page.Size) throw new InvalidOperationException($"{dataSource.GetType().Name} tried to replace an element outside of the known range");
                        page.Buffer[offset] = newElement;
                        // no need to propagate on cache miss because count does not change
                        SourceChanged?.Invoke(this, e);
                    }
                }
                break;

            case NotifyCollectionChangedAction.Add:
                {
                    if (e.NewItems is not [T newElement])
                    {
                        throw new ArgumentException("Unexpected format of Add action", nameof(e));
                    }

                    var (pageNumber, offset) = GetPageNumberAndItemOffset(e.NewStartingIndex);
                    if (Cache.TryGetValue(pageNumber, out var page))
                    {
                        if (offset != page.Size) goto default; // we can't insert without rebuilding the cache
                        @lock.EnterWriteLock();
                        page.Buffer[offset] = newElement;
                        Cache[pageNumber] = page with { Size = page.Size + 1 };
                        @lock.ExitWriteLock();
                    }
                    // propagate on cache miss because we need to update the CollectionViews count
                    SourceChanged?.Invoke(this, e);
                }
                break;

            case NotifyCollectionChangedAction.Remove:
            case NotifyCollectionChangedAction.Reset:
            case NotifyCollectionChangedAction.Move:
            default:
                ClearCache();
                SourceChanged?.Invoke(this, new(NotifyCollectionChangedAction.Reset));
                break;
        }
    }

    public async Task<Result<T>> TryGetValueAsync(int index)
    {
        @lock.EnterReadLock();
        var (pageNumber, offset) = GetPageNumberAndItemOffset(index);
        if (!Cache.TryGetValue(pageNumber, out var page))
        {
            @lock.ExitReadLock();
            if (OptionsMarshall.TryGetError(await LoadPageAsync(pageNumber), out var error))
            {
                return error;
            }
            @lock.EnterReadLock();
            page = Cache[pageNumber];
        }

        try
        {
            if (offset >= page.Size)
            {
                return new IndexOutOfRangeException();
            }

            RequestHistory.Add(pageNumber);
            return page.Buffer[offset];
        }
        finally
        {
            @lock.ExitReadLock();
        }
    }

    private readonly ConcurrentDictionary<int, Task<ErrorState>> _activeRequests = [];
    private readonly Lock startingPageLoadLock = new();
    private async Task<ErrorState> LoadPageAsync(int pageNumber)
    {
        startingPageLoadLock.Enter();
        if (Cache.ContainsKey(pageNumber))
        {
            return default;
        }

        if (_activeRequests.TryGetValue(pageNumber, out var task))
        {
            startingPageLoadLock.Exit();
            return await task;
        }

        task = Impl();
        _activeRequests[pageNumber] = task;
        startingPageLoadLock.Exit();
        try
        {
            return await task;
        }
        catch(Exception e)
        {
            return e;
        }
        finally
        {
            _activeRequests.Remove(pageNumber, out _);
        }

        async Task<ErrorState> Impl()
        {
            @lock.EnterWriteLock();
            if (Cache.Count >= Config.MaxPagesInCache)
            {
                var leastRecentPageNumber = RequestHistory.PopLeastRecent();
                PagePool.Push(Cache[leastRecentPageNumber].Buffer);
                Cache.Remove(leastRecentPageNumber);
            }
            @lock.ExitWriteLock();

            if (!PagePool.TryPop(out var buffer))
            {
                buffer = new T[Config.PageSize];
            }

            var pageStartIndex = pageNumber * Config.PageSize;
            var result = await dataSource.TryGetPageAsync(pageStartIndex, buffer);

            if (!OptionsMarshall.TryGetValue(result, out var elementsRead))
            {
                PagePool.Push(buffer);
                return OptionsMarshall.GetError(result);
            }

            @lock.EnterWriteLock();
            Debug.Assert(!Cache.ContainsKey(pageNumber));
            Cache[pageNumber] = new(buffer, elementsRead);
            @lock.ExitWriteLock();
            return default;
        }
    }

    public async Task<Result<IEnumerable<T>>> GetPageAsync(int pageNumber)
    {
        @lock.EnterReadLock();
        try
        {
            if (Cache.TryGetValue(pageNumber, out var page))
            {
                return page.Buffer.Take(page.Size).ToResult();
            }
        }
        finally
        {
            @lock.ExitReadLock();
        }

        var error = await LoadPageAsync(pageNumber);
        if (OptionsMarshall.TryGetError(error, out var e)) return e;

        @lock.EnterReadLock();
        try
        {
            var page = Cache[pageNumber];
            return page.Buffer.Take(page.Size).ToResult();
        }
        finally
        {
            @lock.ExitReadLock();
        }
    }

    public Option<T> TryGetValueFromCache(int index)
    {
        var (pageNumber, offset) = GetPageNumberAndItemOffset(index);

        @lock.EnterReadLock();
        try
        {
            if (!Cache.TryGetValue(pageNumber, out var page))
            {
                return Option.Error<T>();
            }

            if (offset >= page.Size)
            {
                return Option.Error<T>();
            }

            RequestHistory.Add(pageNumber);
            return Option.Success(page.Buffer[offset]);
        }
        finally
        {
            @lock.ExitReadLock();
        }
    }

    public Option<T> TryGetFirstCachedValue()
    {
        @lock.EnterReadLock();

        try
        {
            if(Cache.Count is 0)
            {
                return Option.Error<T>();
            }

            var pageNumber = Cache.Min(p => p.Key);
            
            // do not add this request to the history because it was not specifically for this page
            return Option.Success(Cache[pageNumber].Buffer[0]);
        }
        finally
        {
            @lock.ExitReadLock();
        }
    }

    public bool IsInCache(int index)
    {
        var (pageNumber, itemOffset) = GetPageNumberAndItemOffset(index);
        @lock.EnterReadLock();
        var result = Cache.TryGetValue(pageNumber, out var page) && itemOffset < page.Size;
        @lock.ExitReadLock();
        return result;
    }

    public bool IsInCache(T element)
    {
        @lock.EnterReadLock();
        try
        {
            foreach (var (_, page) in Cache)
            {
                var index = Array.IndexOf(page.Buffer, element);

                if (index >= 0 && index < page.Size)
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            @lock.ExitReadLock();
        }
    }

    public Task<Option<int>> TryGetSourceCountAsync() => dataSource.TryGetItemCountAsync();

    public void ClearCache()
    {
        @lock.EnterWriteLock();
        foreach (var (_, page) in Cache)
        {
            PagePool.Push(page.Buffer);
        }
        Cache.Clear();
        RequestHistory.Clear();
        @lock.ExitWriteLock();
    }

    private (int pageNumber, int itemOffset) GetPageNumberAndItemOffset(int index) => (index / Config.PageSize, index % Config.PageSize);

    public void Dispose()
    {
        ClearCache();
        PagePool.Clear();
        if (dataSource is INotifyCollectionChanged notifyChanged)
        {
            notifyChanged.CollectionChanged -= OnSourceChanged;
        }
        if(Config.DisposeDataSource && dataSource is IDisposable disposable)
        {
            disposable.Dispose();
        }
        @lock.Dispose();
    }

    internal readonly record struct Page(T[] Buffer, int Size);

    internal readonly struct LRUSet(int capacity)
    {
        // as long as there are only a few pages in cache we should be fine with a list
        private readonly List<int> list = new(capacity);
        private readonly Lock @lock = new();

        public int GetLeastRecent() => list[0];
        public int GetMostRecent() => list[^1];

        public int PopLeastRecent()
        {
            using var scope = @lock.EnterScope();
            var leastRecent = list[0];
            list.RemoveAt(0);
            return leastRecent;
        }

        public void Add(int value)
        {
            using var scope = @lock.EnterScope();
            if (list.Count > 0 && list[^1] == value) return;
            list.Remove(value);
            list.Add(value);
        }

        public void Clear() => list.Clear();
    }
}

public sealed class PagedCacheConfig
{
    public int PageSize { get; init; } = 128;
    public int MaxPagesInCache { get; init; } = 8;
    public bool DisposeDataSource { get; init; } = false;
}