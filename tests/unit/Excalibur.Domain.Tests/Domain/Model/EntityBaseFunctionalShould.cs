// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model;

namespace Excalibur.Tests.Domain.Model;

// ── Test entity classes ──

public class ProductEntity : EntityBase<int>
{
    public ProductEntity(int id) => ProductId = id;
    public int ProductId { get; }
    public override int Key => ProductId;
}

public class AnotherIntEntity : EntityBase<int>
{
    public AnotherIntEntity(int id) => Id = id;
    private int Id { get; }
    public override int Key => Id;
}

public class StringEntity : EntityBase
{
    public StringEntity(string key) => StringKey = key;
    private string StringKey { get; }
    public override string Key => StringKey;
}

public class GuidEntity : EntityBase<Guid>
{
    public GuidEntity(Guid id) => Id = id;
    private Guid Id { get; }
    public override Guid Key => Id;
}

[Trait("Category", "Unit")]
public class EntityBaseFunctionalShould
{
    [Fact]
    public void Equals_SameTypeAndKey_ShouldBeTrue()
    {
        var a = new ProductEntity(1);
        var b = new ProductEntity(1);

        a.Equals(b).ShouldBeTrue();
    }

    [Fact]
    public void Equals_SameTypeDifferentKey_ShouldBeFalse()
    {
        var a = new ProductEntity(1);
        var b = new ProductEntity(2);

        a.Equals(b).ShouldBeFalse();
    }

    [Fact]
    public void Equals_DifferentTypeSameKey_ShouldBeFalse()
    {
        var product = new ProductEntity(1);
        var another = new AnotherIntEntity(1);

        product.Equals(another).ShouldBeFalse();
    }

    [Fact]
    public void Equals_Null_ShouldBeFalse()
    {
        var entity = new ProductEntity(1);
        entity.Equals(null).ShouldBeFalse();
    }

    [Fact]
    public void Equals_ObjectOverload_ShouldWork()
    {
        var a = new ProductEntity(1);
        var b = new ProductEntity(1);

        a.Equals((object)b).ShouldBeTrue();
        a.Equals((object?)null).ShouldBeFalse();
    }

    [Fact]
    public void GetHashCode_SameTypeAndKey_ShouldBeEqual()
    {
        var a = new ProductEntity(42);
        var b = new ProductEntity(42);

        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentKey_ShouldDiffer()
    {
        var a = new ProductEntity(1);
        var b = new ProductEntity(2);

        a.GetHashCode().ShouldNotBe(b.GetHashCode());
    }

    [Fact]
    public void StringEntity_ShouldWorkWithStringKeys()
    {
        var a = new StringEntity("abc");
        var b = new StringEntity("abc");
        var c = new StringEntity("def");

        a.Key.ShouldBe("abc");
        a.Equals(b).ShouldBeTrue();
        a.Equals(c).ShouldBeFalse();
    }

    [Fact]
    public void GuidEntity_ShouldWorkWithGuidKeys()
    {
        var guid = Guid.NewGuid();
        var a = new GuidEntity(guid);
        var b = new GuidEntity(guid);
        var c = new GuidEntity(Guid.NewGuid());

        a.Equals(b).ShouldBeTrue();
        a.Equals(c).ShouldBeFalse();
    }

    [Fact]
    public void Entity_CanBeUsedInHashSet()
    {
        var set = new HashSet<ProductEntity>
        {
            new(1),
            new(1), // duplicate
            new(2),
        };

        set.Count.ShouldBe(2);
    }

    [Fact]
    public void Entity_CanBeUsedAsDictionaryKey()
    {
        var dict = new Dictionary<ProductEntity, string>();
        var key1 = new ProductEntity(42);

        dict[key1] = "test";

        var key2 = new ProductEntity(42); // same key
        dict[key2].ShouldBe("test");
    }
}
