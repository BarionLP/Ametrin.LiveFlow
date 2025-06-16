using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Ametrin.LiveFlow;

public sealed class FakeDataSource<T>(ObservableCollection<T> storage, FakeDataSourceConfig config) : IPageableDataSource<T>, INotifyCollectionChanged
{
    public ObservableCollection<T> Storage { get; } = storage;
    public FakeDataSourceConfig Config { get; } = config;
    public int MaxConcurrentConnections => Config.MaxConcurrentConnections;

    public event NotifyCollectionChangedEventHandler? CollectionChanged { add => Storage.CollectionChanged += value; remove => Storage.CollectionChanged -= value; }

    private volatile int threadsReading = 0;
    public async Task<Result<int>> TryGetPageAsync(int startIndex, T[] buffer)
    {
        if (threadsReading >= MaxConcurrentConnections) throw new InvalidOperationException($"Cannot read from more than {MaxConcurrentConnections} streams");
        threadsReading++;
        if (startIndex >= Storage.Count || startIndex < 0)
        {
            return new IndexOutOfRangeException();
        }
        var length = int.Min(buffer.Length, Storage.Count - startIndex);

        await Task.Delay(Config.Delay);

        for (var i = 0; i < length; i++)
        {
            buffer[i] = Storage[startIndex + i];
        }

        threadsReading--;
        return Result.Success(length);
    }

    public async Task<Option<int>> TryGetItemCountAsync()
    {
        await Task.Delay(Config.Delay);
        return Option.Success(Storage.Count);
    }
}

public sealed class FakeDataSourceConfig
{
    public TimeSpan Delay { get; init; } = TimeSpan.Zero;
    public int MaxConcurrentConnections { get; init; } = int.MaxValue;
}
