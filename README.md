# Ametrin.LiveFlow

Ametrin.LiveFlow is a high-performance .NET library for efficiently managing and displaying large collections of data.  
It provides a smart caching mechanism that optimizes memory usage while ensuring smooth data access

## Features

- 🚀 Efficient pagination with smart caching
- 💾 Memory-optimized page management with LRU (Least Recently Used) eviction
- 🔄 Support for dynamic data sources with change notifications
- 📊 WPF integration with virtual collection support
- 🎯 Configurable cache settings
- 🔧 Extensible data source interface

## Use Cases
- Data Virtualization with realtime updates
  - on demand streaming of huge databases
  - loading huge file trees
- page based caching for REST APIs
- page based batch processing

## Quick Start

### 1. Implement IPageableDataSource

```csharp
public class MyDataSource : IPageableDataSource<MyDataType>
{
    public async Task<Result<int>> TryGetPageAsync(int startIndex, MyDataType[] buffer)
    {
        // Implement your data fetching logic here
        // Fill the buffer with data and return the number of items
        // use a Semaphore(Slim) to limit the number of concurrent connections
    }

    public async Task<Option<int>> TryGetItemCountAsync()
    {
        // Return total number of items if known, or None if unknown
        // (none is not supported by the WPF plugin, yet)
    }

    // Optionally implement INotifyCollectionChanged for real time cache updates
    // some changes may trigger a cache rebuild
}
```

### 2. Create and Configure PagedCache

```csharp
var config = new PagedCacheConfig
{
    MaxPagesInCache = 16,     // Maximum number of pages to keep in memory
    PageSize = 64,            // Number of items per page
};

var dataSource = new MyDataSource();
var cache = new PagedCache<MyDataType>(dataSource, config);
```

### 3. WPF Integration

```xaml
<DataGrid x:Name="MyDataGrid"
          IsReadOnly="True" // required
          EnableRowVirtualization="True" // required
          VirtualizingPanel.IsVirtualizing="True" // required
          VirtualizingPanel.VirtualizationMode="Recycling" // optional 
          VirtualizingPanel.ScrollUnit="Item" // required
          ScrollViewer.IsDeferredScrollingEnabled="True" // recommended (prevents mass loading when dragging the scroll bar)
          />
```

```csharp
// In your code-behind or view model:
public async Task InitializeCollection()
{
    var cache = new PagedCache<MyDataType>(dataSource, config);
    await cache.BindToDataGridAsync(MyDataGrid);
}
```


## Sample Projects

The repository includes several sample projects to help you get started:

- `Ametrin.LiveFlow.Sample`: Console application demonstrating basic usage
- `Ametrin.LiveFlow.WpfSample`: WPF application showing UI integration

## Advanced Features

### Property Info Source

The WPF collection view supports different ways of generating columns:

- `PropertyInfoSource.Type`: Uses type reflection (default)
- `PropertyInfoSource.FirstElement`: Derives properties from the first loaded element. (supports ExpandoObjects)
- `PropertyInfoSource.None`: No auto generated columns.

## Contributions

Written by [Barion](https://github.com/BarionLP)  
Funded by [RoBotos Systems](https://github.com/RoBotos-Systems)

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.