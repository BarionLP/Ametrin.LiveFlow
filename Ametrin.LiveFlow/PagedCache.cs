using System.Diagnostics;

namespace Ametrin.LiveFlow;

public sealed class PagedCache<T>(IPageableDataSource<T> dataSource, int pageSize, int maxPagesInCache)
{
    private readonly IPageableDataSource<T> dataSource = dataSource;
    private readonly int pageSize = pageSize;
    private readonly int maxPagesInCache = maxPagesInCache;
    internal readonly Dictionary<int, Page> Cache = new(capacity: maxPagesInCache);
    internal readonly LRUSet RequestHistroy = new(capacity: maxPagesInCache);
    internal readonly Stack<T[]> PagePool = new(capacity: maxPagesInCache);

    public async Task<Option<T>> TryGetValueAsync(int index)
    {
        var (pageNumber, offset) = GetPageNumberAndItemOffset(index);
        var pageStartIndex = pageNumber * pageSize;


        if (Cache.TryGetValue(pageNumber, out var page))
        {
            if (offset >= page.Size) return Option.Error<T>();
            RequestHistroy.Add(pageNumber);
            return Option.Success(page.Buffer[offset]);
        }

        if (Cache.Count >= maxPagesInCache)
        {
            var leastRecentPageNumber = RequestHistroy.PopLeastRecent();
            PagePool.Push(Cache[leastRecentPageNumber].Buffer);
            Cache.Remove(leastRecentPageNumber);
        }

        if (!PagePool.TryPop(out var buffer))
        {
            buffer = new T[pageSize];
        }

        var result = await dataSource.TryGetPageAsync(pageStartIndex, buffer);

        if (OptionsMarshall.TryGetValue(result, out var value))
        {
            Cache[pageNumber] = new(value.buffer, value.elementsWritten);
            RequestHistroy.Add(pageNumber);
            return offset < value.elementsWritten ? value.buffer[offset] : Option.Error<T>();
        }

        PagePool.Push(buffer);
        return Option.Error<T>();
    }

    public Option<T> TryGetValueFromCache(int index)
    {
        var (pageNumber, offset) = GetPageNumberAndItemOffset(index);
        // var pageStartIndex = pageNumber * pageSize;

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
        var (pageIndex, pageOffset) = GetPageNumberAndItemOffset(index);
        return IsInCacheInternal(pageIndex, pageOffset);
    }

    public void ClearCache()
    {
        foreach (var (_, page) in Cache)
        {
            PagePool.Push(page.Buffer);
        }
        Cache.Clear();
        RequestHistroy.Clear();
    }

    private (int pageNumber, int itemOffset) GetPageNumberAndItemOffset(int index) => (index / pageSize, index % pageSize);

    private bool IsInCacheInternal(int pageNumber, int itemOffset) => Cache.TryGetValue(pageNumber, out var page) && itemOffset < page.Size;

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