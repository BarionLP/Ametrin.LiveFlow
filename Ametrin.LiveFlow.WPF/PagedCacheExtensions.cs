using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ametrin.LiveFlow.WPF;

public static class PagedCacheExtensions
{
    public static async Task<PagedCacheCollectionView<T>> BindToDataGridAsync<T>(this PagedCache<T> cache, DataGrid dataGrid, PropertyInfoSource infoSource = PropertyInfoSource.Type, T? loadingItem = default, bool disposeCache = false)
    {
        ArgumentNullException.ThrowIfNull(dataGrid);
        if(!dataGrid.EnableRowVirtualization) throw new ArgumentException("DataGrid needs EnableRowVirtualization=\"True\"", nameof(dataGrid));
        var panel = dataGrid.FindChild<VirtualizingStackPanel>() ?? throw new NullReferenceException();
        if(VirtualizingPanel.GetScrollUnit(panel) is not ScrollUnit.Item) throw new ArgumentException("DataGrid needs VirtualizingPanel.ScrollUnit=\"Item\"");
        if(!VirtualizingPanel.GetIsVirtualizing(panel)) throw new ArgumentException("DataGrid needs VirtualizingPanel.IsVirtualizing=\"True\"");

        var view = await PagedCacheCollectionView.CreateAsync(cache, infoSource, loadingItem, disposeCache);
        dataGrid.ItemsSource = view;
        view.Refresh();
        return view;
    }

    internal static T? FindChild<T>(this DependencyObject obj) where T : DependencyObject
    {
        var children = obj.EnumerateChildren();
        return children.OfType<T>().FirstOrDefault() ?? children.Select(FindChild<T>).FirstOrDefault(c => c is not null);
    }

    internal static IEnumerable<DependencyObject> EnumerateChildren(this DependencyObject obj)
    {
        var count = VisualTreeHelper.GetChildrenCount(obj);
        for (var index = 0; index < count; index++)
        {
            if (obj.GetChild(index) is DependencyObject child)
            {
                yield return child;
            }
        }
    }

    internal static DependencyObject? GetChild(this DependencyObject child, int index) => VisualTreeHelper.GetChild(child, index);
}
