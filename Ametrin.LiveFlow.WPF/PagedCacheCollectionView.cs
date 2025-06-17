using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;

namespace Ametrin.LiveFlow.WPF;

public static class PagedCacheCollectionView
{
    public static async Task<PagedCacheCollectionView<T>> CreateAsync<T>(PagedCache<T> cache, PropertyInfoSource propertyInfoSource = PropertyInfoSource.Type)
    {
        var count = (await cache.TryGetSourceCountAsync()).OrThrow(); // data sources without count are not yet supported
        // make sure first page is cached so it can be displayed instantly (and useful for column generation)
        var firstElement = await cache.TryGetValueAsync(0);
        var properties = propertyInfoSource switch
        {
            PropertyInfoSource.None => [],
            PropertyInfoSource.Type => TypeDescriptor.GetProperties(typeof(T)).Cast<PropertyDescriptor>().Select(desc => new ItemPropertyInfo(desc.Name, desc.PropertyType, desc)),
            PropertyInfoSource.FirstElement when typeof(T) == typeof(ExpandoObject) => firstElement.Require<IDictionary<string, object?>>().Map(static obj => obj.Select(static pair => new ItemPropertyInfo(pair.Key, pair.Value?.GetType(), null))).Or(static () => []),
            PropertyInfoSource.FirstElement => firstElement.Map(static obj => TypeDescriptor.GetProperties(obj!).Cast<PropertyDescriptor>().Select(desc => new ItemPropertyInfo(desc.Name, desc.PropertyType, desc))).Or(static () => []),
            _ => throw new UnreachableException(),
        };

        T loadingItem = firstElement.OrThrow() switch
        {
            ExpandoObject obj => (T)(object)obj.Aggregate(new ExpandoObject(), static (l, pair) => { ((IDictionary<string, object?>) l)[pair.Key] = pair.Value; return l; }),
            _ => Activator.CreateInstance<T>(),
        };

        return new PagedCacheCollectionView<T>(cache, count, new([.. properties]), loadingItem);
    }
}

/// <summary>
/// bind to DataGrid with <see cref="PagedCacheExtensions.BindToDataGridAsync{T}(PagedCache{T}, System.Windows.Controls.DataGrid, PropertyInfoSource)"/><br/>
/// create with <see cref="PagedCacheCollectionView.CreateAsync{T}(PagedCache{T}, PropertyInfoSource)"/>
/// </summary>
/// <typeparam name="T"></typeparam>
public sealed class PagedCacheCollectionView<T> : ICollectionView, IList<T>, IItemProperties, IDisposable
{
    private readonly PagedCache<T> cache;
    private readonly T loadingItem;

    public int Count { get; private set; }
    public bool IsReadOnly => true;
    public bool CanFilter => false;
    public bool CanGroup => false;
    public bool CanSort => false;
    public object? CurrentItem => this[CurrentPosition];
    public int CurrentPosition { get; private set; } = 0;
    public bool IsEmpty => false;
    public CultureInfo Culture { get; set; } = CultureInfo.CurrentCulture;
    public ReadOnlyCollection<ItemPropertyInfo> ItemProperties { get; }

    public event EventHandler? CurrentChanged;
    public event CurrentChangingEventHandler? CurrentChanging;
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <summary>
    /// </summary>
    /// <param name="cache"></param>
    /// <param name="count"></param>
    internal PagedCacheCollectionView(PagedCache<T> cache, int count, ReadOnlyCollection<ItemPropertyInfo> itemProperties, T loadingItem)
    {
        this.cache = cache;
        Count = count;
        ItemProperties = itemProperties;
        this.loadingItem = loadingItem;
        cache.CacheChanged += OnCacheChanged;
    }

    public T this[int index]
    {
        get
        {
            if (index == -1) return default!;

            if (OptionsMarshall.TryGetValue(cache.TryGetValueFromCache(index), out var value))
            {
                if (index > cache.Config.PageSize)
                {
                    _ = cache.TryGetValueAsync(index - cache.Config.PageSize);
                }

                if (index < Count - cache.Config.PageSize)
                {
                    _ = cache.TryGetValueAsync(index + cache.Config.PageSize);
                }

                return value;
            }

            _ = LoadAsyncAndNotify(index, loadingItem);

            return loadingItem;
        }
        set => throw new NotSupportedException();
    }

    private async Task LoadAsyncAndNotify(int index, T tempObj)
    {
        var item = (await cache.TryGetValueAsync(index)).OrThrow();
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, newItem: item, oldItem: tempObj, index));
    }

    public bool MoveCurrentToFirst() => MoveCurrentToPosition(0);
    public bool MoveCurrentToNext() => MoveCurrentToPosition(CurrentPosition + 1);
    public bool MoveCurrentToPrevious() => MoveCurrentToPosition(CurrentPosition - 1);
    public bool MoveCurrentToPosition(int position)
    {
        if (!OnCurrentChanging())
        {
            return false;
        }

        CurrentPosition = position;
        CurrentChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private bool OnCurrentChanging()
    {
        var args = new CurrentChangingEventArgs();
        CurrentChanging?.Invoke(this, args);
        return !args.Cancel;
    }

    public void Refresh()
    {
        CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Reset));
    }

    private async void OnCacheChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                Debug.Assert(e.OldItems is null or { Count: 0 });
                Count += e.NewItems!.Count;
                break;

            case NotifyCollectionChangedAction.Remove:
                Debug.Assert(e.NewItems is null or { Count: 0 });
                Count -= e.OldItems!.Count;
                break;

            case NotifyCollectionChangedAction.Replace:
                break;

            case NotifyCollectionChangedAction.Reset:
            default:
                Count = (await cache.TryGetSourceCountAsync()).OrThrow();
                break;
        }

        CollectionChanged?.Invoke(this, e);
    }


    public void Dispose()
    {
        cache.CacheChanged -= OnCacheChanged;
    }


    public IEnumerable SourceCollection { get { for (var i = 0; i < cache.Config.PageSize; i++) yield return cache.TryGetValueFromCache(i).Or(default!); } }
    bool ICollectionView.IsCurrentAfterLast => throw new NotImplementedException();
    bool ICollectionView.IsCurrentBeforeFirst => throw new NotImplementedException();
    SortDescriptionCollection? ICollectionView.SortDescriptions => null;
    Predicate<object> ICollectionView.Filter { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    ObservableCollection<GroupDescription>? ICollectionView.GroupDescriptions => null;
    ReadOnlyObservableCollection<object>? ICollectionView.Groups => null;
    bool ICollectionView.Contains(object item) => item is T t && cache.IsInCache(t);
    bool ICollectionView.MoveCurrentTo(object item) => throw new NotSupportedException();
    bool ICollectionView.MoveCurrentToLast() => throw new NotSupportedException();
    IDisposable ICollectionView.DeferRefresh() => new DeferredRefresh();
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => throw new NotSupportedException();
    IEnumerator IEnumerable.GetEnumerator() => SourceCollection.GetEnumerator();

    int IList<T>.IndexOf(T item) => throw new NotSupportedException();
    void IList<T>.Insert(int index, T item) => throw new NotSupportedException();
    void IList<T>.RemoveAt(int index) => throw new NotSupportedException();
    void ICollection<T>.Add(T item) => throw new NotSupportedException();
    void ICollection<T>.Clear() => throw new NotSupportedException();
    bool ICollection<T>.Contains(T item) => throw new NotSupportedException();
    void ICollection<T>.CopyTo(T[] array, int arrayIndex) => throw new NotSupportedException();
    bool ICollection<T>.Remove(T item) => throw new NotSupportedException();

    private readonly struct DeferredRefresh : IDisposable
    {
        public void Dispose()
        {

        }
    }
}
