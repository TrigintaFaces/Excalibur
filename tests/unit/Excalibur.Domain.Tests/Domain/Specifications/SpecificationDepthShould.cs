using Excalibur.Domain.Specifications;

namespace Excalibur.Tests.Domain.Specifications;

[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class SpecificationDepthShould
{
    [Theory]
    [InlineData(6, true)]
    [InlineData(10, true)]
    [InlineData(100, true)]
    [InlineData(5, false)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void IsSatisfiedBy_EvaluateCorrectly(int value, bool expected)
    {
        // Arrange
        var spec = new IsPositiveAndGreaterThan5();

        // Act
        var result = spec.IsSatisfiedBy(value);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void And_ChainMultipleSpecifications()
    {
        // Arrange
        var gt5 = new GreaterThanSpec(5);
        var lt20 = new LessThanSpec(20);
        var even = new EvenSpec();

        // Act
        var combined = gt5.And(lt20).And(even);

        // Assert
        combined.IsSatisfiedBy(8).ShouldBeTrue();
        combined.IsSatisfiedBy(7).ShouldBeFalse(); // odd
        combined.IsSatisfiedBy(22).ShouldBeFalse(); // > 20
        combined.IsSatisfiedBy(2).ShouldBeFalse(); // < 5
    }

    [Fact]
    public void Or_ChainMultipleSpecifications()
    {
        // Arrange
        var lt0 = new LessThanSpec(0);
        var gt100 = new GreaterThanSpec(100);
        var isZero = new EqualSpec(0);

        // Act
        var combined = lt0.Or(gt100).Or(isZero);

        // Assert
        combined.IsSatisfiedBy(-5).ShouldBeTrue();
        combined.IsSatisfiedBy(200).ShouldBeTrue();
        combined.IsSatisfiedBy(0).ShouldBeTrue();
        combined.IsSatisfiedBy(50).ShouldBeFalse();
    }

    [Fact]
    public void Not_OfAnd_WorksLikeDeMorgans()
    {
        // Arrange: NOT(A AND B) should equal (NOT A) OR (NOT B)
        var gt5 = new GreaterThanSpec(5);
        var lt10 = new LessThanSpec(10);

        var notOfAnd = gt5.And(lt10).Not();

        // Act & Assert
        notOfAnd.IsSatisfiedBy(7).ShouldBeFalse(); // 7 is in range [6,9]
        notOfAnd.IsSatisfiedBy(3).ShouldBeTrue();   // 3 is not > 5
        notOfAnd.IsSatisfiedBy(15).ShouldBeTrue();  // 15 is not < 10
    }

    [Fact]
    public void Not_OfOr_WorksLikeDeMorgans()
    {
        // Arrange: NOT(A OR B) should be true only when both are false
        var gt10 = new GreaterThanSpec(10);
        var lt0 = new LessThanSpec(0);

        var notOfOr = gt10.Or(lt0).Not();

        // Act & Assert
        notOfOr.IsSatisfiedBy(5).ShouldBeTrue(); // 5 is NOT > 10 and NOT < 0
        notOfOr.IsSatisfiedBy(15).ShouldBeFalse(); // 15 > 10
        notOfOr.IsSatisfiedBy(-1).ShouldBeFalse(); // -1 < 0
    }

    [Fact]
    public void TripleNot_EqualsNot()
    {
        // Arrange
        var gt5 = new GreaterThanSpec(5);
        var tripleNot = gt5.Not().Not().Not();

        // Act & Assert
        tripleNot.IsSatisfiedBy(3).ShouldBeTrue(); // NOT(> 5) = true for 3
        tripleNot.IsSatisfiedBy(10).ShouldBeFalse(); // NOT(> 5) = false for 10
    }

    [Fact]
    public void ComplexChain_And_Or_Not_Composition()
    {
        // Arrange: (x > 0 AND x < 100) AND NOT(x == 50)
        var gt0 = new GreaterThanSpec(0);
        var lt100 = new LessThanSpec(100);
        var eq50 = new EqualSpec(50);

        var combined = gt0.And(lt100).And(eq50.Not());

        // Act & Assert
        combined.IsSatisfiedBy(25).ShouldBeTrue();
        combined.IsSatisfiedBy(75).ShouldBeTrue();
        combined.IsSatisfiedBy(50).ShouldBeFalse(); // excluded
        combined.IsSatisfiedBy(0).ShouldBeFalse(); // not > 0
        combined.IsSatisfiedBy(150).ShouldBeFalse(); // not < 100
    }

    private sealed class IsPositiveAndGreaterThan5 : Specification<int>
    {
        public override bool IsSatisfiedBy(int candidate) => candidate > 5;
    }

    private sealed class GreaterThanSpec : Specification<int>
    {
        private readonly int _threshold;
        public GreaterThanSpec(int threshold) => _threshold = threshold;
        public override bool IsSatisfiedBy(int candidate) => candidate > _threshold;
    }

    private sealed class LessThanSpec : Specification<int>
    {
        private readonly int _threshold;
        public LessThanSpec(int threshold) => _threshold = threshold;
        public override bool IsSatisfiedBy(int candidate) => candidate < _threshold;
    }

    private sealed class EvenSpec : Specification<int>
    {
        public override bool IsSatisfiedBy(int candidate) => candidate % 2 == 0;
    }

    private sealed class EqualSpec : Specification<int>
    {
        private readonly int _value;
        public EqualSpec(int value) => _value = value;
        public override bool IsSatisfiedBy(int candidate) => candidate == _value;
    }
}
