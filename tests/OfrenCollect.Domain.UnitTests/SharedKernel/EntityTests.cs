using FluentAssertions;
using OfrenCollect.SharedKernel;

namespace OfrenCollect.Domain.UnitTests.SharedKernel;

public class EntityTests
{
    private sealed class SampleEntity : Entity
    {
        public SampleEntity(Guid id) : base(id) { }
    }

    private sealed class OtherEntity : Entity
    {
        public OtherEntity(Guid id) : base(id) { }
    }

    [Fact]
    public void Entities_OfSameTypeWithSameId_AreEqual()
    {
        var id = Guid.NewGuid();

        new SampleEntity(id).Should().Be(new SampleEntity(id));
    }

    [Fact]
    public void Entities_OfSameTypeWithDifferentIds_AreNotEqual()
    {
        new SampleEntity(Guid.NewGuid()).Should().NotBe(new SampleEntity(Guid.NewGuid()));
    }

    [Fact]
    public void Entities_OfDifferentTypesWithSameId_AreNotEqual()
    {
        var id = Guid.NewGuid();

        new SampleEntity(id).Equals(new OtherEntity(id)).Should().BeFalse();
    }

    [Fact]
    public void TransientEntities_WithEmptyId_AreNotEqual()
    {
        new SampleEntity(Guid.Empty).Equals(new SampleEntity(Guid.Empty)).Should().BeFalse();
    }

    [Fact]
    public void Entity_IsNotEqualToNull()
    {
        new SampleEntity(Guid.NewGuid()).Equals(null).Should().BeFalse();
    }

    [Fact]
    public void EqualEntities_ShareHashCode()
    {
        var id = Guid.NewGuid();

        new SampleEntity(id).GetHashCode().Should().Be(new SampleEntity(id).GetHashCode());
    }

    [Fact]
    public void EqualityOperators_ReflectIdentity()
    {
        var id = Guid.NewGuid();
        var a = new SampleEntity(id);
        var b = new SampleEntity(id);
        var c = new SampleEntity(Guid.NewGuid());

        (a == b).Should().BeTrue();
        (a != c).Should().BeTrue();
    }
}
