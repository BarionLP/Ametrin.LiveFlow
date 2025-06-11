namespace Ametrin.LiveFlow;

public interface IPageableDataSource<T>
{
    public Task<Result<int>> TryGetPageAsync(int startIndex, T[] buffer);
    public Task<Option<int>> TryGetItemCountAsync();
}

public sealed class MemoryDataSource<T>(ImmutableArray<T> storage) : IPageableDataSource<T>
{
    public ImmutableArray<T> Storage { get; } = storage;

    public Task<Result<int>> TryGetPageAsync(int startIndex, T[] buffer)
    {
        if (startIndex >= Storage.Length)
        {
            return Task.FromResult(Result.Error<int>(new IndexOutOfRangeException()));
        }
        var length = int.Min(buffer.Length, Storage.Length - startIndex);

        Storage.AsSpan(startIndex, length).CopyTo(buffer);
        return Task.FromResult(Result.Success(length));
    }

    public Task<Option<int>> TryGetItemCountAsync() => Task.FromResult(Option.Success(Storage.Length));
}
