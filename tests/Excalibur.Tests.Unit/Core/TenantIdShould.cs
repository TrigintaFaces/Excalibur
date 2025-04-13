using Excalibur.Core;

using Shouldly;

namespace Excalibur.Tests.Unit.Core;

public class TenantIdShould
{
	[Fact]
	public void ConstructWithNullDefaultsToEmpty()
	{
		// Act
		var tenantId = new TenantId(null);

		// Assert
		tenantId.Value.ShouldBe(string.Empty);
	}

	[Fact]
	public void ConstructWithDefaultConstructorSetsEmpty()
	{
		var tenantId = new TenantId();

		tenantId.Value.ShouldBe(string.Empty);
	}

	[Fact]
	public void ConstructWithEmptyString()
	{
		// Act
		var tenantId = new TenantId(string.Empty);

		// Assert
		tenantId.Value.ShouldBe(string.Empty);
	}

	[Fact]
	public void ConstructWithNonNullValue()
	{
		// Act
		var tenantId = new TenantId("tenant-123");

		// Assert
		tenantId.Value.ShouldBe("tenant-123");
	}

	[Fact]
	public void UpdateValueCorrectly()
	{
		// Arrange
		var tenantId = new TenantId();

		// Act
		tenantId.Value = "updated-tenant";

		// Assert
		tenantId.Value.ShouldBe("updated-tenant");
	}

	[Fact]
	public void ReturnEmptyStringWhenDefaultConstructorIsUsed()
	{
		// Act
		var tenantId = new TenantId();

		// Assert
		tenantId.Value.ShouldBe(string.Empty);
	}

	[Fact]
	public void ReturnCorrectStringRepresentation()
	{
		// Arrange
		var tenantId = new TenantId("tenant-abc");

		// Act
		var result = tenantId.ToString();

		// Assert
		result.ShouldBe("tenant-abc");
	}

	[Fact]
	public void EqualsReturnsTrueForSameReference()
	{
		var tenantId = new TenantId("abc");

		tenantId.Equals(tenantId).ShouldBeTrue();
	}

	[Fact]
	public void EqualsReturnsFalseForNull()
	{
		var tenantId = new TenantId("abc");

#pragma warning disable CA1508 // Avoid dead conditional code
		tenantId.Equals(null).ShouldBeFalse();
#pragma warning restore CA1508 // Avoid dead conditional code
	}

	[Fact]
	public void SupportEqualityBasedOnValue()
	{
		// Arrange
		var tenantId1 = new TenantId("tenant-123");
		var tenantId2 = new TenantId("tenant-123");
		var tenantId3 = new TenantId("tenant-456");

		// Assert
		tenantId1.ShouldBe(tenantId2);
		tenantId1.ShouldNotBe(tenantId3);
	}

	[Fact]
	public void EqualsReturnsTrueForSameValue()
	{
		var a = new TenantId("abc");
		var b = new TenantId("abc");

		a.Equals(b).ShouldBeTrue();
		a.Equals((object)b).ShouldBeTrue();
	}

	[Fact]
	public void EqualsReturnsFalseForDifferentValue()
	{
		var a = new TenantId("abc");
		var b = new TenantId("xyz");

		a.Equals(b).ShouldBeFalse();
		a.Equals((object)b).ShouldBeFalse();
	}

	[Fact]
	public void GetHashCodeIsCaseInsensitive()
	{
		var lower = new TenantId("tenant");
		var upper = new TenantId("TENANT");

		lower.GetHashCode().ShouldBe(upper.GetHashCode());
	}
}
