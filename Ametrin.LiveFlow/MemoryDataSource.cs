using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;

namespace Ametrin.LiveFlow;

/// <summary>
/// In Memory implementation for <see cref="IPageableDataSource{T}"/> (supports <see cref="INotifyCollectionChanged"/>)
/// </summary>
/// <typeparam name="T"></typeparam>
/// <param name="storage"></param>
public sealed class MemoryDataSource<T>(ObservableCollection<T> storage) : IPageableDataSource<T>, INotifyCollectionChanged
{
    public ObservableCollection<T> Storage { get; } = storage;

    public event NotifyCollectionChangedEventHandler? CollectionChanged { add => Storage.CollectionChanged += value; remove => Storage.CollectionChanged -= value; }

    public Task<Result<int>> TryGetPageAsync(int startIndex, T[] buffer, CancellationToken token = default)
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

    public Task<Option<int>> TryGetItemCountAsync(CancellationToken token = default) => Task.FromResult(Option.Success(Storage.Count));
}
