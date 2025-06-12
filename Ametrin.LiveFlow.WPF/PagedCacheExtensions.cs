using System.Windows.Controls;

namespace Ametrin.LiveFlow.WPF;

public static class PagedCacheExtensions
{
    public static async Task<PagedCacheCollectionView<T>> BindToDataGridAsync<T>(this PagedCache<T> cache, DataGrid dataGrid, PropertyInfoSource infoSource = PropertyInfoSource.Type)
    {
        ArgumentNullException.ThrowIfNull(dataGrid);

        var view = await PagedCacheCollectionView.CreateAsync(cache, infoSource);
        dataGrid.ItemsSource = view;
        view.Refresh();
        return view;
    }
}
