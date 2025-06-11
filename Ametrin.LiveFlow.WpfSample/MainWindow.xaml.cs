using System.Collections.Immutable;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using Bogus;

namespace Ametrin.LiveFlow.WpfSample;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private static readonly Faker<User> faker = new Faker<User>()
        .RuleFor(static u => u.Index, static f => f.IndexGlobal)
        .RuleFor(static u => u.Name, static f => f.Name.FullName())
        .RuleFor(static u => u.Age, static f => Random.Shared.Next(13, 99))
        .RuleFor(static u => u.Guid, static f => Guid.CreateVersion7())
        ;

    private static readonly MemoryDataSource<User> dataSource = new([.. faker.GenerateLazy(10_000_000)]);

    private readonly PagedCache<User> cache;
    public MainWindow()
    {
        cache = new(dataSource, new() { PageSize = 96 });
        InitializeComponent();

        Loaded += async (sender, args) =>
        {
            TestDataGrid.ItemsSource = await cache.GetViewAsync();
            // TestDataGrid.ItemsSource = dataSource.Storage;
        };

    }
}

internal sealed record User(int Index, string Name, int Age, Guid Guid)
{
    public User() : this(0, "", 0, Guid.Empty)
    {
    }
}
