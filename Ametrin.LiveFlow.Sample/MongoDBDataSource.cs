using System.Collections.Specialized;
using System.Diagnostics;
using Ametrin.Optional;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Ametrin.LiveFlow.Sample;

public sealed class MongoDBServerSource(IMongoCollection<BsonDocument> collection) : IPageableDataSource<BsonDocument>, INotifyCollectionChanged
{
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    private readonly IMongoCollection<BsonDocument> collection = collection ?? throw new ArgumentNullException(nameof(collection));
    private FilterDefinition<BsonDocument> _rowFilter = FilterDefinition<BsonDocument>.Empty;
    public FilterDefinition<BsonDocument> RowFilter
    {
        get => _rowFilter;
        set
        {
            value ??= FilterDefinition<BsonDocument>.Empty;
            if (_rowFilter == value) return;
            _rowFilter = value;
            CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Reset));
        }
    }

    public async Task<Option<int>> TryGetItemCountAsync()
    {
        return (int)await collection.CountDocumentsAsync(RowFilter);
    }

    public Task<Result<int>> TryGetPageAsync(int startIndex, BsonDocument[] buffer)
    {
        if (startIndex < 0)
        {
            return Task.FromResult(Result.Error<int>(new IndexOutOfRangeException()));
        }

        var items = collection.Find(RowFilter).Sort(Builders<BsonDocument>.Sort.Ascending("_id")).Skip(startIndex).ToEnumerable().Take(buffer.Length);

        var i = 0;
        foreach (var doc in items)
        {
            if (i >= buffer.Length) throw new UnreachableException();

            buffer[i] = doc;
            i++;
        }

        return Task.FromResult(Result.Success(i));
    }
}
