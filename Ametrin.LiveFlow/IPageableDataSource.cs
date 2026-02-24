using System.Threading;

namespace Ametrin.LiveFlow;

public interface IPageableDataSource<T>
{
    /// <param name="startIndex">start index of the page</param>
    /// <param name="buffer">buffer to fill. Length is the page size</param>
    /// <returns>number of items written to <paramref name="buffer"/> or the error</returns>
    public Task<Result<int>> TryGetPageAsync(int startIndex, T[] buffer, CancellationToken token = default);

    /// <returns>total number of items in the source or Error if indeterminable</returns>
    public Task<Option<int>> TryGetItemCountAsync(CancellationToken token = default);
}
