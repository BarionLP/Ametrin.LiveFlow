using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Ametrin.LiveFlow;

public sealed class MemoryDataSource<T>(ObservableCollection<T> storage) : IPageableDataSource<T>, INotifyCollectionChanged
{

    public ObservableCollection<T> Storage { get; } = storage;
    public int MaxConcurrentConnections => int.MaxValue;

    public event NotifyCollectionChangedEventHandler? CollectionChanged { add => Storage.CollectionChanged += value; remove => Storage.CollectionChanged -= value; }

    public Task<Result<int>> TryGetPageAsync(int startIndex, T[] buffer)
    {
        if (startIndex >= Storage.Count || startIndex < 0)
        {
            return Task.FromResult(Result.Error<int>(new IndexOutOfRangeException()));
        }
        var length = int.Min(buffer.Length, Storage.Count - startIndex);


        for (var i = 0; i < length; i++)
        {
            buffer[i] = Storage[startIndex + i];
        }

        return Task.FromResult(Result.Success(length));
    }

    public Task<Option<int>> TryGetItemCountAsync() => Task.FromResult(Option.Success(Storage.Count));
}
