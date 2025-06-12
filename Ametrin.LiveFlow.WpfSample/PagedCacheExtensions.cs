using System.Windows.Controls;

namespace Ametrin.LiveFlow.WpfSample;

public static class PagedCacheExtensions
{
    public static async Task<PagedCacheCollectionView<T>> BindToDataGridAsync<T>(this PagedCache<T> cache, DataGrid dataGrid)
    {
        ArgumentNullException.ThrowIfNull(dataGrid);

        var view = await PagedCacheCollectionView.CreateAsync(cache);
        dataGrid.ItemsSource = view;
        view.Refresh();
        return view;
    }
}
