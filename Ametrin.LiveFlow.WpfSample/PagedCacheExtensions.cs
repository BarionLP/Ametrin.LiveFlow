using System.ComponentModel;

namespace Ametrin.LiveFlow.WpfSample;

public static class PagedCacheExtensions
{
    public static async Task<ICollectionView> GetViewAsync<T>(this PagedCache<T> cache) => await PagedCacheCollectionView.CreateAsync(cache);
}
