using System.Windows;
using Ametrin.LiveFlow.WPF;
using Bogus;

namespace Ametrin.LiveFlow.WpfSample;

public partial class MainWindow : Window
{
    private static readonly Faker<User> faker = new Faker<User>()
        .RuleFor(static u => u.Index, static f => f.IndexGlobal)
        .RuleFor(static u => u.Name, static f => f.Name.FullName())
        .RuleFor(static u => u.Age, static f => Random.Shared.Next(13, 99))
        .RuleFor(static u => u.Guid, static f => Guid.CreateVersion7())
        ;

    private static readonly FakeDataSource<User> dataSource = new([.. faker.GenerateLazy(10_000_000)], new() { MaxConcurrentConnections = 1, Delay = TimeSpan.FromMilliseconds(1000) });

    private readonly PagedCache<User> cache;
    private PagedCacheCollectionView<User> view = default!;
    public MainWindow()
    {
        cache = new(dataSource, new() { PageSize = 96 });
        InitializeComponent();

        Loaded += async (sender, args) =>
        {
            view = await cache.BindToDataGridAsync(TestDataGrid);
            await Task.Delay(4000);
            dataSource.Storage[0] = faker.Generate();
            await Task.Delay(4000);
            dataSource.Storage.Add(faker.Generate());
            await Task.Delay(4000);
            dataSource.Storage.Insert(1, faker.Generate());
        };

        Closed += (sender, args) =>
        {
            cache.Dispose();
            view.Dispose();
        };
    }
}

internal sealed record User(int Index, string Name, int Age, Guid Guid)
{
    public User() : this(0, "", 0, Guid.Empty)
    {
    }
}
