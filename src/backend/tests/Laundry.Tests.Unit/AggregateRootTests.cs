using Laundry.BuildingBlocks.Domain;
using Xunit;

namespace Laundry.Tests.Unit;

public class AggregateRootTests
{
    private sealed record Happened(string Name) : IDomainEvent;

    private sealed class Sample(int id) : AggregateRoot<int>(id)
    {
        public void Do(string name) => Raise(new Happened(name));
    }

    private sealed class Other(int id) : AggregateRoot<int>(id);

    [Fact]
    public void RaiseAddsEventsInOrder()
    {
        var sample = new Sample(1);

        sample.Do("a");
        sample.Do("b");

        Assert.Equal([new Happened("a"), new Happened("b")], sample.DomainEvents);
    }

    [Fact]
    public void ClearDomainEventsRemovesAll()
    {
        var sample = new Sample(1);
        sample.Do("a");

        sample.ClearDomainEvents();

        Assert.Empty(sample.DomainEvents);
    }

    [Fact]
    public void SameTypeAndIdAreEqual()
    {
        var first = new Sample(7);
        var second = new Sample(7);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void DifferentIdsAreNotEqual()
    {
        Assert.NotEqual(new Sample(1), new Sample(2));
    }

    [Fact]
    public void DifferentTypesWithSameIdAreNotEqual()
    {
        Assert.False(new Sample(1).Equals(new Other(1)));
    }

    [Fact]
    public void DefaultIdsAreEqualOnlyByReference()
    {
        var first = new Sample(0);
        var second = new Sample(0);

        Assert.NotEqual(first, second);
        Assert.Equal(first, first);
    }
}
