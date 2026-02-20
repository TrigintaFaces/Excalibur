// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Specifications;

namespace Excalibur.Tests.Domain.Specifications;

// ── Test specifications ──

public class IsPositive : Specification<int>
{
    public override bool IsSatisfiedBy(int candidate) => candidate > 0;
}

public class IsEven : Specification<int>
{
    public override bool IsSatisfiedBy(int candidate) => candidate % 2 == 0;
}

public class IsGreaterThan : Specification<int>
{
    private readonly int _threshold;
    public IsGreaterThan(int threshold) => _threshold = threshold;
    public override bool IsSatisfiedBy(int candidate) => candidate > _threshold;
}

[Trait("Category", "Unit")]
public class SpecificationFunctionalShould
{
    [Theory]
    [InlineData(5, true)]
    [InlineData(0, false)]
    [InlineData(-3, false)]
    public void IsSatisfiedBy_ShouldEvaluateCorrectly(int value, bool expected)
    {
        var spec = new IsPositive();
        spec.IsSatisfiedBy(value).ShouldBe(expected);
    }

    [Theory]
    [InlineData(4, true)]   // positive AND even
    [InlineData(3, false)]  // positive but NOT even
    [InlineData(-2, false)] // even but NOT positive
    [InlineData(-3, false)] // neither
    public void And_ShouldCombineWithLogicalAnd(int value, bool expected)
    {
        var spec = new IsPositive().And(new IsEven());
        spec.IsSatisfiedBy(value).ShouldBe(expected);
    }

    [Theory]
    [InlineData(4, true)]   // positive AND even
    [InlineData(3, true)]   // positive OR even = positive
    [InlineData(-2, true)]  // even OR positive = even
    [InlineData(-3, false)] // neither
    public void Or_ShouldCombineWithLogicalOr(int value, bool expected)
    {
        var spec = new IsPositive().Or(new IsEven());
        spec.IsSatisfiedBy(value).ShouldBe(expected);
    }

    [Theory]
    [InlineData(5, false)]  // positive => NOT positive = false
    [InlineData(-3, true)]  // not positive => NOT positive = true
    [InlineData(0, true)]   // not positive (zero) => true
    public void Not_ShouldNegateSpecification(int value, bool expected)
    {
        var spec = new IsPositive().Not();
        spec.IsSatisfiedBy(value).ShouldBe(expected);
    }

    [Fact]
    public void ComplexComposition_ShouldEvaluateCorrectly()
    {
        // (positive AND even) OR > 100
        var spec = new IsPositive()
            .And(new IsEven())
            .Or(new IsGreaterThan(100));

        spec.IsSatisfiedBy(4).ShouldBeTrue();     // positive and even
        spec.IsSatisfiedBy(101).ShouldBeTrue();   // > 100
        spec.IsSatisfiedBy(3).ShouldBeFalse();    // positive but not even, not > 100
        spec.IsSatisfiedBy(-2).ShouldBeFalse();   // even but not positive, not > 100
    }

    [Fact]
    public void DoubleNegation_ShouldReturnOriginal()
    {
        var spec = new IsPositive().Not().Not();

        spec.IsSatisfiedBy(5).ShouldBeTrue();
        spec.IsSatisfiedBy(-5).ShouldBeFalse();
    }

    [Fact]
    public void And_WithNull_ShouldThrow()
    {
        var spec = new IsPositive();
        Should.Throw<ArgumentNullException>(() => spec.And(null!));
    }

    [Fact]
    public void Or_WithNull_ShouldThrow()
    {
        var spec = new IsPositive();
        Should.Throw<ArgumentNullException>(() => spec.Or(null!));
    }

    [Fact]
    public void ChainedAnd_ShouldRequireAll()
    {
        // positive AND even AND > 10
        var spec = new IsPositive()
            .And(new IsEven())
            .And(new IsGreaterThan(10));

        spec.IsSatisfiedBy(12).ShouldBeTrue();
        spec.IsSatisfiedBy(8).ShouldBeFalse();   // positive, even, but not > 10
        spec.IsSatisfiedBy(15).ShouldBeFalse();  // positive, > 10, but not even
    }

    [Fact]
    public void ChainedOr_ShouldRequireAny()
    {
        // positive OR even OR > 100
        var spec = new IsPositive()
            .Or(new IsEven())
            .Or(new IsGreaterThan(100));

        spec.IsSatisfiedBy(5).ShouldBeTrue();    // positive
        spec.IsSatisfiedBy(-4).ShouldBeTrue();   // even
        spec.IsSatisfiedBy(101).ShouldBeTrue();  // > 100
        spec.IsSatisfiedBy(-3).ShouldBeFalse();  // none
    }

    [Fact]
    public void Specification_CanFilterCollections()
    {
        var spec = new IsPositive().And(new IsEven());
        var numbers = new[] { -4, -3, -2, -1, 0, 1, 2, 3, 4, 5, 6 };

        var results = numbers.Where(n => spec.IsSatisfiedBy(n)).ToArray();

        results.ShouldBe(new[] { 2, 4, 6 });
    }
}
