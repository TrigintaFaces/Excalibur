using Excalibur.A3.Authorization;

namespace Excalibur.Tests.A3;

/// <summary>
/// Unit tests for PolicyResult record.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PolicyResultShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasFalseValues()
	{
		// Arrange & Act
		var result = new PolicyResult();

		// Assert
		result.IsAuthorized.ShouldBeFalse();
		result.HasActivityGrant.ShouldBeFalse();
		result.HasResourceGrant.ShouldBeFalse();
	}

	[Fact]
	public void Create_WithIsAuthorizedTrue_ReturnsAuthorized()
	{
		// Arrange & Act
		var result = new PolicyResult { IsAuthorized = true };

		// Assert
		result.IsAuthorized.ShouldBeTrue();
	}

	[Fact]
	public void Create_WithActivityGrant_SetsGrant()
	{
		// Arrange & Act
		var result = new PolicyResult
		{
			IsAuthorized = true,
			HasActivityGrant = true
		};

		// Assert
		result.HasActivityGrant.ShouldBeTrue();
		result.HasResourceGrant.ShouldBeFalse();
	}

	[Fact]
	public void Create_WithResourceGrant_SetsGrant()
	{
		// Arrange & Act
		var result = new PolicyResult
		{
			IsAuthorized = true,
			HasResourceGrant = true
		};

		// Assert
		result.HasResourceGrant.ShouldBeTrue();
		result.HasActivityGrant.ShouldBeFalse();
	}

	[Fact]
	public void Equality_SameValues_AreEqual()
	{
		// Arrange
		var result1 = new PolicyResult
		{
			IsAuthorized = true,
			HasActivityGrant = true,
			HasResourceGrant = false
		};
		var result2 = new PolicyResult
		{
			IsAuthorized = true,
			HasActivityGrant = true,
			HasResourceGrant = false
		};

		// Act & Assert
		result1.ShouldBe(result2);
	}

	[Fact]
	public void Equality_DifferentValues_AreNotEqual()
	{
		// Arrange
		var result1 = new PolicyResult { IsAuthorized = true };
		var result2 = new PolicyResult { IsAuthorized = false };

		// Act & Assert
		result1.ShouldNotBe(result2);
	}

	[Fact]
	public void With_CreatesModifiedCopy()
	{
		// Arrange
		var original = new PolicyResult { IsAuthorized = false };

		// Act
		var modified = original with { IsAuthorized = true };

		// Assert
		original.IsAuthorized.ShouldBeFalse();
		modified.IsAuthorized.ShouldBeTrue();
	}
}
