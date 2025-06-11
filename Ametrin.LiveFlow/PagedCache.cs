namespace Ametrin.LiveFlow;

public sealed class PagedCache<T>(IPageableDataSource<T> dataSource, PagedCacheConfig config)
{
    private readonly IPageableDataSource<T> dataSource = dataSource;
    internal readonly Dictionary<int, Page> Cache = new(capacity: config.MaxPagesInCache);
    internal readonly LRUSet RequestHistroy = new(capacity: config.MaxPagesInCache);
    internal readonly Stack<T[]> PagePool = new(capacity: config.MaxPagesInCache);

    public PagedCacheConfig Config { get; } = config;
    public int PageSize => Config.PageSize;

    public async Task<Option<T>> TryGetValueAsync(int index)
    {
        var (pageNumber, offset) = GetPageNumberAndItemOffset(index);

        if (Cache.TryGetValue(pageNumber, out var page))
        {
            if (offset >= page.Size) return Option.Error<T>();
            RequestHistroy.Add(pageNumber);
            return Option.Success(page.Buffer[offset]);
        }

        if (Cache.Count >= Config.MaxPagesInCache)
        {
            var leastRecentPageNumber = RequestHistroy.PopLeastRecent();
            PagePool.Push(Cache[leastRecentPageNumber].Buffer);
            Cache.Remove(leastRecentPageNumber);
        }

        if (!PagePool.TryPop(out var buffer))
        {
            buffer = new T[PageSize];
        }

        var pageStartIndex = pageNumber * PageSize;
        var result = await dataSource.TryGetPageAsync(pageStartIndex, buffer);

        if (OptionsMarshall.TryGetValue(result, out var elementsWritten))
        {
            Cache[pageNumber] = new(buffer, elementsWritten);
            RequestHistroy.Add(pageNumber);
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
            RequestHistroy.Add(pageNumber);
            return Option.Success(page.Buffer[offset]);
        }

        return Option.Error<T>();
    }

    public bool IsInCache(int index)
    {
        var (pageNumber, itemOffset) = GetPageNumberAndItemOffset(index);
        return Cache.TryGetValue(pageNumber, out var page) && itemOffset < page.Size;
    }

    public Task<Option<int>> TryGetSourceCountAsync() => dataSource.TryGetItemCountAsync();

    public void ClearCache()
    {
        foreach (var (_, page) in Cache)
        {
            PagePool.Push(page.Buffer);
        }
        Cache.Clear();
        RequestHistroy.Clear();
    }

    private (int pageNumber, int itemOffset) GetPageNumberAndItemOffset(int index) => (index / PageSize, index % PageSize);

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