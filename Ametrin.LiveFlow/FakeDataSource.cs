using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;

namespace Ametrin.LiveFlow;

/// <summary>
/// simulates real data base connections
/// </summary>
/// <typeparam name="T"></typeparam>
/// <param name="storage">the underlying data source</param>
/// <param name="config"></param>
public sealed class FakeDataSource<T>(ObservableCollection<T> storage, FakeDataSourceConfig config) : IPageableDataSource<T>, INotifyCollectionChanged
{
    public ObservableCollection<T> Storage { get; } = storage;
    public FakeDataSourceConfig Config { get; } = config;

    public event NotifyCollectionChangedEventHandler? CollectionChanged { add => Storage.CollectionChanged += value; remove => Storage.CollectionChanged -= value; }

    private readonly SemaphoreSlim semaphore = new(config.MaxConcurrentConnections);
    public async Task<Result<int>> TryGetPageAsync(int startIndex, T[] buffer, CancellationToken token = default)
    {
        if (startIndex >= Storage.Count || startIndex < 0)
        {
            return new IndexOutOfRangeException();
        }
        var length = int.Min(buffer.Length, Storage.Count - startIndex);

        await semaphore.WaitAsync(token);
        await Task.Delay(Config.Delay, token);

        for (var i = 0; i < length; i++)
        {
            buffer[i] = Storage[startIndex + i];
        }

        semaphore.Release();
        return Result.Success(length);
    }

    public async Task<Option<int>> TryGetItemCountAsync(CancellationToken token = default)
    {
        await Task.Delay(Config.Delay, token);
        return Option.Success(Storage.Count);
    }
}

public sealed class FakeDataSourceConfig
{
    public TimeSpan Delay { get; init; } = TimeSpan.Zero;
    public int MaxConcurrentConnections { get; init; } = int.MaxValue;
}
