namespace Ametrin.LiveFlow;

public interface IPageableDataSource<T>
{
    public int MaxConcurrentConnections { get; }
    public Task<Result<int>> TryGetPageAsync(int startIndex, T[] buffer);
    public Task<Option<int>> TryGetItemCountAsync();
}
