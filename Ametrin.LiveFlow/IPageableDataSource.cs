namespace Ametrin.LiveFlow;

public interface IPageableDataSource<T>
{
    public Task<Result<(T[] buffer, int elementsWritten)>> TryGetPageAsync(int startIndex, T[] buffer);
}

public sealed class MemoryDataSource<T>(ImmutableArray<T> storage) : IPageableDataSource<T>
{
    private readonly ImmutableArray<T> storage = storage;

    public Task<Result<(T[] buffer, int elementsWritten)>> TryGetPageAsync(int startIndex, T[] buffer)
    {
        if (startIndex >= storage.Length)
        {
            return Task.FromResult(Result.Error<(T[], int)>(new IndexOutOfRangeException()));
        }
        var length = int.Min(buffer.Length, storage.Length - startIndex);

        storage.AsSpan(startIndex, length).CopyTo(buffer);
        return Task.FromResult(Result.Success((buffer, length)));
    }
}
