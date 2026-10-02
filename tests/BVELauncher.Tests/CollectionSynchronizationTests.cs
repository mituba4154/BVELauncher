using System.Collections.ObjectModel;
using BVELauncher.App.ViewModels;

namespace BVELauncher.Tests;

public sealed class CollectionSynchronizationTests
{
    [Fact]
    public void UnchangedRefreshDoesNotNotifyOrRecreateContainers()
    {
        var entries = Enumerable.Range(0, 1000).Select(_ => new object()).ToArray();
        var collection = new ObservableCollection<object>(entries); int changes = 0;
        collection.CollectionChanged += (_, _) => changes++;
        CollectionSynchronization.Update(collection, entries);
        Assert.Equal(0, changes); Assert.Same(entries[500], collection[500]);
    }
    [Fact]
    public void SearchSortAndCollapseRetainObjectsWithoutResetNotification()
    {
        var entries = Enumerable.Range(0, 100).Select(_ => new object()).ToArray();
        var collection = new ObservableCollection<object>(entries);
        collection.CollectionChanged += (_, change) => Assert.NotEqual(System.Collections.Specialized.NotifyCollectionChangedAction.Reset, change.Action);
        var filtered = entries.Where((_, index) => index % 2 == 0).Reverse().ToArray();
        CollectionSynchronization.Update(collection, filtered); Assert.Equal(filtered, collection);
        CollectionSynchronization.Update(collection, []); Assert.Empty(collection);
        CollectionSynchronization.Update(collection, entries); Assert.Equal(entries, collection);
    }
}
