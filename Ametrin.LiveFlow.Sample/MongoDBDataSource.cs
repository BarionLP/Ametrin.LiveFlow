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

    public async Task<Option<int>> TryGetItemCountAsync(CancellationToken token = default)
    {
        return (int)await collection.CountDocumentsAsync(RowFilter, cancellationToken: token);
    }

    public async Task<Result<int>> TryGetPageAsync(int startIndex, BsonDocument[] buffer, CancellationToken token = default)
    {
        if (buffer is null)
        {
            return new ArgumentNullException(nameof(buffer));
        }

        if (startIndex < 0)
        {
            return new ArgumentOutOfRangeException(nameof(startIndex));
        }

        if (buffer.Length is 0)
        {
            return 0;
        }

        using var cursor = await collection.Find(RowFilter).Sort(Builders<BsonDocument>.Sort.Ascending("_id")).Skip(startIndex).Limit(buffer.Length).ToCursorAsync(token);

        var i = 0;
        while (i < buffer.Length && await cursor.MoveNextAsync(token))
        {
            foreach (var doc in cursor.Current)
            {
                buffer[i++] = doc;
                if (i >= buffer.Length)
                    break;
            }
        }


        return i;
    }

    public void Dispose()
    {
    }
}
