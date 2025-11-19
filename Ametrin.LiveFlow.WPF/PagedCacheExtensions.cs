using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ametrin.LiveFlow.WPF;

public static class PagedCacheExtensions
{

    extension<T>(PagedCache<T> cache)
    {
        public async Task<PagedCacheCollectionView<T>> BindToDataGridAsync(DataGrid dataGrid, PropertyInfoSource infoSource = PropertyInfoSource.Type, T? loadingItem = default, bool disposeCache = false)
        {
            ArgumentNullException.ThrowIfNull(dataGrid);
            if (!dataGrid.EnableRowVirtualization) throw new ArgumentException("DataGrid needs EnableRowVirtualization=\"True\"", nameof(dataGrid));
            var panel = dataGrid.FindChild<VirtualizingStackPanel>() ?? throw new NullReferenceException();
            if (VirtualizingPanel.GetScrollUnit(panel) is not ScrollUnit.Item) throw new ArgumentException("DataGrid needs VirtualizingPanel.ScrollUnit=\"Item\"");
            if (!VirtualizingPanel.GetIsVirtualizing(panel)) throw new ArgumentException("DataGrid needs VirtualizingPanel.IsVirtualizing=\"True\"");

            var view = await PagedCacheCollectionView.CreateAsync(cache, infoSource, loadingItem, disposeCache);
            dataGrid.ItemsSource = view;
            view.Refresh();
            return view;
        }

        public async Task<PagedCacheCollectionView<T>> BindToTreeViewAsync(TreeView treeView, PropertyInfoSource infoSource = PropertyInfoSource.Type, T? loadingItem = default, bool disposeCache = false)
        {
            ArgumentNullException.ThrowIfNull(treeView);
            var panel = treeView.FindChild<VirtualizingStackPanel>() ?? throw new NullReferenceException();
            if (VirtualizingPanel.GetScrollUnit(panel) is not ScrollUnit.Item) throw new ArgumentException("TreeView needs VirtualizingPanel.ScrollUnit=\"Item\"");
            if (!VirtualizingPanel.GetIsVirtualizing(panel)) throw new ArgumentException("TreeView needs VirtualizingPanel.IsVirtualizing=\"True\"");

            var view = await PagedCacheCollectionView.CreateAsync(cache, infoSource, loadingItem, disposeCache);
            treeView.ItemsSource = view;
            view.Refresh();
            return view;
        }
    }

    extension(DependencyObject obj)
    {
        internal T? FindChild<T>() where T : DependencyObject
        {
            var children = obj.EnumerateChildren();
            return children.OfType<T>().FirstOrDefault() ?? children.Select(FindChild<T>).FirstOrDefault(c => c is not null);
        }

        internal IEnumerable<DependencyObject> EnumerateChildren()
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

        internal DependencyObject? GetChild(int index) => VisualTreeHelper.GetChild(obj, index);
    }

}
