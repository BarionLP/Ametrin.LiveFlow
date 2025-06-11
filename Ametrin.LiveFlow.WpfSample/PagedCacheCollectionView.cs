using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Ametrin.Optional;
using Bogus;

namespace Ametrin.LiveFlow.WpfSample;

public static class PagedCacheCollectionView
{
    public static async Task<PagedCacheCollectionView<T>> CreateAsync<T>(PagedCache<T> cache, PropertyInfoSource propertyInfoSource = PropertyInfoSource.Type)
    {
        var count = await cache.TryGetSourceCountAsync();
        // make sure first page is cached so it can be displayed instantly (and useful for column generation)
        var firstElement = await cache.TryGetValueAsync(0);
        var properties = propertyInfoSource switch
        {
            PropertyInfoSource.None => new([]),
            PropertyInfoSource.Type => TypeDescriptor.GetProperties(typeof(T)),
            PropertyInfoSource.FirstElement => firstElement.Map(static obj => TypeDescriptor.GetProperties(obj!)).Or(static () => new([])),
            _ => throw new UnreachableException(),
        };
        return new PagedCacheCollectionView<T>(cache, count.OrThrow(), new([.. properties.Cast<PropertyDescriptor>().Select(desc => new ItemPropertyInfo(desc.Name, desc.PropertyType, desc))]));
    }
}

public enum PropertyInfoSource { None, Type, FirstElement }

public sealed class PagedCacheCollectionView<T> : ICollectionView, IList<T>, IItemProperties
{
    private readonly PagedCache<T> cache;

    /// <summary>
    /// create with <see cref="PagedCacheCollectionView.CreateAsync{T}(PagedCache{T})"/>
    /// </summary>
    /// <param name="cache"></param>
    /// <param name="count"></param>
    internal PagedCacheCollectionView(PagedCache<T> cache, int count, ReadOnlyCollection<ItemPropertyInfo> itemProperties)
    {
        this.cache = cache;
        Count = count;
        ItemProperties = itemProperties;
    }

    public int Count { get; private set; }
    public bool IsReadOnly => true;
    public bool CanFilter => false;
    public bool CanGroup => false;
    public bool CanSort => false;
    public T this[int index] { get => cache.TryGetValueAsync(index).Result.OrThrow(); set => throw new NotSupportedException(); }
    public object? CurrentItem => this[CurrentPosition];
    public int CurrentPosition { get; private set; } = 0;
    public bool IsEmpty => false;
    public CultureInfo Culture { get; set; } = CultureInfo.CurrentCulture;

    public event EventHandler? CurrentChanged;
    public event CurrentChangingEventHandler? CurrentChanging;
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

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

    public ReadOnlyCollection<ItemPropertyInfo> ItemProperties { get; }


    public IEnumerable SourceCollection { get { for (var i = 0; i < Count; i++) yield return cache.TryGetValueFromCache(i).Or(default!); } }
    bool ICollectionView.IsCurrentAfterLast => throw new NotImplementedException();
    bool ICollectionView.IsCurrentBeforeFirst => throw new NotImplementedException();
    SortDescriptionCollection? ICollectionView.SortDescriptions => null;
    Predicate<object> ICollectionView.Filter { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    ObservableCollection<GroupDescription>? ICollectionView.GroupDescriptions => null;
    ReadOnlyObservableCollection<object>? ICollectionView.Groups => null;
    bool ICollectionView.Contains(object item) => throw new NotSupportedException();
    bool ICollectionView.MoveCurrentTo(object item) => throw new NotSupportedException();
    bool ICollectionView.MoveCurrentToLast() => throw new NotSupportedException();
    void ICollectionView.Refresh() => throw new NotSupportedException();
    public IDisposable DeferRefresh() => new DeferredRefresh();
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
