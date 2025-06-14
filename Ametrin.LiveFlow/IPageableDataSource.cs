using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Ametrin.LiveFlow;

public interface IPageableDataSource<T>
{
    public Task<Result<int>> TryGetPageAsync(int startIndex, T[] buffer);
    public Task<Option<int>> TryGetItemCountAsync();
}

public sealed class MemoryDataSource<T>(ObservableCollection<T> storage, TimeSpan delay) : IPageableDataSource<T>, INotifyCollectionChanged
{
    private readonly TimeSpan delay = delay;

    public ObservableCollection<T> Storage { get; } = storage;

    public event NotifyCollectionChangedEventHandler? CollectionChanged { add => Storage.CollectionChanged += value; remove => Storage.CollectionChanged -= value; }

    public async Task<Result<int>> TryGetPageAsync(int startIndex, T[] buffer)
    {
        if (startIndex >= Storage.Count || startIndex < 0)
        {
            return new IndexOutOfRangeException();
        }
        var length = int.Min(buffer.Length, Storage.Count - startIndex);

        await Task.Delay(delay);

        for (var i = 0; i < length; i++)
        {
            buffer[i] = Storage[startIndex + i];
        }

        return Result.Success(length);
    }

    public Task<Option<int>> TryGetItemCountAsync() => Task.FromResult(Option.Success(Storage.Count));
}
