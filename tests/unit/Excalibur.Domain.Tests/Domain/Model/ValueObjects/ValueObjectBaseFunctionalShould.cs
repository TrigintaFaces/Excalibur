// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model.ValueObjects;

namespace Excalibur.Tests.Domain.Model.ValueObjects;

// ── Test value objects ──

public sealed class EmailAddress : ValueObjectBase
{
    public string User { get; }
    public string Domain { get; }

    public EmailAddress(string user, string domain)
    {
        User = user;
        Domain = domain;
    }

    public override IEnumerable<object?> GetEqualityComponents()
    {
        yield return User?.ToUpperInvariant();
        yield return Domain?.ToUpperInvariant();
    }
}

public sealed class EmptyValueObject : ValueObjectBase
{
    public override IEnumerable<object?> GetEqualityComponents()
    {
        yield break;
    }
}

public sealed class NullComponentValueObject : ValueObjectBase
{
    public string? Value { get; }

    public NullComponentValueObject(string? value) => Value = value;

    public override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}

[Trait("Category", "Unit")]
public class ValueObjectBaseFunctionalShould
{
    [Fact]
    public void Equals_SameComponents_ShouldBeTrue()
    {
        var a = new EmailAddress("john", "example.com");
        var b = new EmailAddress("John", "Example.COM");

        a.Equals(b).ShouldBeTrue();
        (a == b).ShouldBeTrue();
        (a != b).ShouldBeFalse();
    }

    [Fact]
    public void Equals_DifferentComponents_ShouldBeFalse()
    {
        var a = new EmailAddress("john", "example.com");
        var b = new EmailAddress("jane", "example.com");

        a.Equals(b).ShouldBeFalse();
        (a == b).ShouldBeFalse();
        (a != b).ShouldBeTrue();
    }

    [Fact]
    public void Equals_Null_ShouldBeFalse()
    {
        var a = new EmailAddress("john", "example.com");

        a.Equals(null).ShouldBeFalse();
        (a == null).ShouldBeFalse();
        (null == a).ShouldBeFalse();
    }

    [Fact]
    public void Equals_SameReference_ShouldBeTrue()
    {
        var a = new EmailAddress("john", "example.com");

        ReferenceEquals(a, a).ShouldBeTrue();
    }

    [Fact]
    public void Equals_BothNull_ShouldBeTrue()
    {
        EmailAddress? a = null;
        EmailAddress? b = null;

        (a == b).ShouldBeTrue();
    }

    [Fact]
    public void Equals_DifferentType_ShouldBeFalse()
    {
        var email = new EmailAddress("john", "example.com");
        var other = new NullComponentValueObject("john");

        email.Equals(other).ShouldBeFalse();
    }

    [Fact]
    public void GetHashCode_SameComponents_ShouldBeEqual()
    {
        var a = new EmailAddress("john", "example.com");
        var b = new EmailAddress("John", "Example.COM");

        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentComponents_ShouldBeDifferent()
    {
        var a = new EmailAddress("john", "example.com");
        var b = new EmailAddress("jane", "example.com");

        // Not guaranteed to be different but highly likely
        a.GetHashCode().ShouldNotBe(b.GetHashCode());
    }

    [Fact]
    public void GetHashCode_EmptyComponents_ShouldReturnZero()
    {
        var empty = new EmptyValueObject();
        empty.GetHashCode().ShouldBe(0);
    }

    [Fact]
    public void GetHashCode_NullComponent_ShouldReturnZero()
    {
        var nullObj = new NullComponentValueObject(null);
        nullObj.GetHashCode().ShouldBe(0);
    }

    [Fact]
    public void Equals_ObjectOverload_WithNonValueObject_ShouldBeFalse()
    {
        var email = new EmailAddress("john", "example.com");
        email.Equals((object)"not a value object").ShouldBeFalse();
    }

    [Fact]
    public void Equals_ObjectOverload_WithNull_ShouldBeFalse()
    {
        var email = new EmailAddress("john", "example.com");
        email.Equals((object?)null).ShouldBeFalse();
    }

    [Fact]
    public void ValueObject_CanBeUsedAsSetKey()
    {
        var set = new HashSet<EmailAddress>
        {
            new("john", "example.com"),
            new("John", "EXAMPLE.COM"), // duplicate
            new("jane", "example.com"),
        };

        set.Count.ShouldBe(2);
    }

    [Fact]
    public void ValueObject_CanBeUsedAsDictionaryKey()
    {
        var dict = new Dictionary<EmailAddress, int>();
        var key1 = new EmailAddress("john", "example.com");
        var key2 = new EmailAddress("John", "Example.COM"); // same as key1

        dict[key1] = 42;
        dict[key2].ShouldBe(42);
    }
}
