using Excalibur.A3.Authorization.Grants;

namespace Excalibur.A3.Tests.A3.Authorization;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class GrantScopeDepthShould
{
    [Fact]
    public void CreateWithValidParameters()
    {
        // Act
        var scope = new GrantScope("tenant-1", "Activity", "ReadOrders");

        // Assert
        scope.TenantId.ShouldBe("tenant-1");
        scope.GrantType.ShouldBe("Activity");
        scope.Qualifier.ShouldBe("ReadOrders");
    }

    [Fact]
    public void ThrowOnNullTenantId()
    {
        Should.Throw<ArgumentNullException>(() =>
            new GrantScope(null!, "Activity", "Qualifier"));
    }

    [Fact]
    public void ThrowOnEmptyTenantId()
    {
        Should.Throw<ArgumentNullException>(() =>
            new GrantScope("", "Activity", "Qualifier"));
    }

    [Fact]
    public void ThrowOnNullGrantType()
    {
        Should.Throw<ArgumentNullException>(() =>
            new GrantScope("tenant", null!, "Qualifier"));
    }

    [Fact]
    public void ThrowOnEmptyGrantType()
    {
        Should.Throw<ArgumentNullException>(() =>
            new GrantScope("tenant", "", "Qualifier"));
    }

    [Fact]
    public void ThrowOnNullQualifier()
    {
        Should.Throw<ArgumentNullException>(() =>
            new GrantScope("tenant", "Activity", null!));
    }

    [Fact]
    public void ThrowOnEmptyQualifier()
    {
        Should.Throw<ArgumentNullException>(() =>
            new GrantScope("tenant", "Activity", ""));
    }

    [Fact]
    public void ParseFromString_ValidFormat()
    {
        // Act
        var scope = GrantScope.FromString("tenant-1:Activity:ReadOrders");

        // Assert
        scope.TenantId.ShouldBe("tenant-1");
        scope.GrantType.ShouldBe("Activity");
        scope.Qualifier.ShouldBe("ReadOrders");
    }

    [Fact]
    public void ParseFromString_ThrowOnNull()
    {
        Should.Throw<ArgumentNullException>(() => GrantScope.FromString(null!));
    }

    [Fact]
    public void ParseFromString_ThrowOnInvalidFormat_TooFewParts()
    {
        Should.Throw<ArgumentException>(() => GrantScope.FromString("only:two"));
    }

    [Fact]
    public void ParseFromString_ThrowOnInvalidFormat_SinglePart()
    {
        Should.Throw<ArgumentException>(() => GrantScope.FromString("single"));
    }

    [Fact]
    public void ParseFromString_ThrowOnEmpty()
    {
        Should.Throw<ArgumentException>(() => GrantScope.FromString(""));
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var scope = new GrantScope("tenant-1", "Activity", "ReadOrders");

        // Act
        var result = scope.ToString();

        // Assert
        result.ShouldBe("tenant-1:Activity:ReadOrders");
    }

    [Fact]
    public void RoundTrip_FromStringAndToString()
    {
        // Arrange
        var original = "myTenant:ActivityGroup:AdminGroup";

        // Act
        var scope = GrantScope.FromString(original);
        var roundTripped = scope.ToString();

        // Assert
        roundTripped.ShouldBe(original);
    }

    [Fact]
    public void RecordEquality_SameValues()
    {
        // Arrange
        var scope1 = new GrantScope("t1", "Activity", "Read");
        var scope2 = new GrantScope("t1", "Activity", "Read");

        // Assert
        scope1.ShouldBe(scope2);
    }

    [Fact]
    public void RecordEquality_DifferentValues()
    {
        // Arrange
        var scope1 = new GrantScope("t1", "Activity", "Read");
        var scope2 = new GrantScope("t2", "Activity", "Read");

        // Assert
        scope1.ShouldNotBe(scope2);
    }

    [Fact]
    public void MutableProperties_CanBeChanged()
    {
        // Arrange
        var scope = new GrantScope("t1", "Activity", "Read");

        // Act
        scope.TenantId = "t2";
        scope.GrantType = "ActivityGroup";
        scope.Qualifier = "Admin";

        // Assert
        scope.TenantId.ShouldBe("t2");
        scope.GrantType.ShouldBe("ActivityGroup");
        scope.Qualifier.ShouldBe("Admin");
    }

    [Fact]
    public void ParseFromString_WithColonsInQualifier()
    {
        // The split uses 3 as max count, so extra colons remain in qualifier
        var scope = GrantScope.FromString("tenant:Activity:qualifier:with:colons");

        scope.TenantId.ShouldBe("tenant");
        scope.GrantType.ShouldBe("Activity");
        scope.Qualifier.ShouldBe("qualifier:with:colons");
    }
}
