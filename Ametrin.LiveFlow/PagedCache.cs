using System.Collections.Specialized;
using System.Diagnostics;

namespace Ametrin.LiveFlow;

public sealed class PagedCache<T>
{
    private readonly IPageableDataSource<T> dataSource;
    internal readonly Dictionary<int, Page> Cache;
    internal readonly LRUSet RequestHistory;
    internal readonly Stack<T[]> PagePool;

    public event NotifyCollectionChangedEventHandler? CacheChanged;

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
                        if (offset >= page.Size) throw new UnreachableException();
                        page.Buffer[offset] = newElement;
                        // no need to propagate on cache miss because count does not change
                        CacheChanged?.Invoke(this, e);
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
                        page.Buffer[offset] = newElement;
                        Cache[pageNumber] = page with { Size = page.Size + 1 };
                    }
                    // propagate on cache miss because we need to update the CollectionViews count 
                    CacheChanged?.Invoke(this, e); 
                }
                break;

            case NotifyCollectionChangedAction.Remove:
            case NotifyCollectionChangedAction.Reset:
            case NotifyCollectionChangedAction.Move:
            default:
                ClearCache();
                CacheChanged?.Invoke(this, new(NotifyCollectionChangedAction.Reset));
                break;
        }
    }

    public async Task<Option<T>> TryGetValueAsync(int index)
    {
        var (pageNumber, offset) = GetPageNumberAndItemOffset(index);

        if (Cache.TryGetValue(pageNumber, out var page))
        {
            if (offset >= page.Size) return Option.Error<T>();
            RequestHistory.Add(pageNumber);
            return Option.Success(page.Buffer[offset]);
        }

        if (Cache.Count >= Config.MaxPagesInCache)
        {
            var leastRecentPageNumber = RequestHistory.PopLeastRecent();
            PagePool.Push(Cache[leastRecentPageNumber].Buffer);
            Cache.Remove(leastRecentPageNumber);
        }

        if (!PagePool.TryPop(out var buffer))
        {
            buffer = new T[Config.PageSize];
        }

        var pageStartIndex = pageNumber * Config.PageSize;
        var result = await dataSource.TryGetPageAsync(pageStartIndex, buffer);

        if (OptionsMarshall.TryGetValue(result, out var elementsWritten))
        {
            Cache[pageNumber] = new(buffer, elementsWritten);
            RequestHistory.Add(pageNumber);
            return offset < elementsWritten ? buffer[offset] : Option.Error<T>();
        }

        PagePool.Push(buffer);
        return Option.Error<T>();
    }

    public Option<T> TryGetValueFromCache(int index)
    {
        var (pageNumber, offset) = GetPageNumberAndItemOffset(index);

        if (Cache.TryGetValue(pageNumber, out var page))
        {
            if (offset >= page.Size) return Option.Error<T>();
            RequestHistory.Add(pageNumber);
            return Option.Success(page.Buffer[offset]);
        }

        return Option.Error<T>();
    }

    public bool IsInCache(int index)
    {
        var (pageNumber, itemOffset) = GetPageNumberAndItemOffset(index);
        return Cache.TryGetValue(pageNumber, out var page) && itemOffset < page.Size;
    }

    public bool IsInCache(T element)
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

    public Task<Option<int>> TryGetSourceCountAsync() => dataSource.TryGetItemCountAsync();

    public void ClearCache()
    {
        foreach (var (_, page) in Cache)
        {
            PagePool.Push(page.Buffer);
        }
        Cache.Clear();
        RequestHistory.Clear();
    }

    private (int pageNumber, int itemOffset) GetPageNumberAndItemOffset(int index) => (index / Config.PageSize, index % Config.PageSize);

    internal readonly record struct Page(T[] Buffer, int Size);

    internal readonly struct LRUSet(int capacity)
    {
        // as long as there are only a few pages in cache we should be fine with a list
        private readonly List<int> list = new(capacity);

        public int GetLeastRecent() => list[0];
        public int GetMostRecent() => list[^1];

        public int PopLeastRecent()
        {
            var leastRecent = list[0];
            list.RemoveAt(0);
            return leastRecent;
        }

        public void Add(int value)
        {
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
}