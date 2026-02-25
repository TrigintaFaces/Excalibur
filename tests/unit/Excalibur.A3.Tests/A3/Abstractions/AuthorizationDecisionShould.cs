using Excalibur.A3.Abstractions.Authorization;

namespace Excalibur.Tests.A3.Abstractions;

/// <summary>
/// Unit tests for AuthorizationDecision record.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AuthorizationDecisionShould : UnitTestBase
{
	[Fact]
	public void Create_WithEffectOnly_HasNullReason()
	{
		// Arrange & Act
		var decision = new AuthorizationDecision(AuthorizationEffect.Permit);

		// Assert
		decision.Effect.ShouldBe(AuthorizationEffect.Permit);
		decision.Reason.ShouldBeNull();
	}

	[Fact]
	public void Create_WithEffectAndReason_SetsValues()
	{
		// Arrange & Act
		var decision = new AuthorizationDecision(AuthorizationEffect.Deny, "Access denied for resource");

		// Assert
		decision.Effect.ShouldBe(AuthorizationEffect.Deny);
		decision.Reason.ShouldBe("Access denied for resource");
	}

	[Fact]
	public void Equality_SameEffectAndReason_AreEqual()
	{
		// Arrange
		var decision1 = new AuthorizationDecision(AuthorizationEffect.Permit, "Allowed");
		var decision2 = new AuthorizationDecision(AuthorizationEffect.Permit, "Allowed");

		// Act & Assert
		decision1.ShouldBe(decision2);
	}

	[Fact]
	public void Equality_DifferentEffect_AreNotEqual()
	{
		// Arrange
		var decision1 = new AuthorizationDecision(AuthorizationEffect.Permit);
		var decision2 = new AuthorizationDecision(AuthorizationEffect.Deny);

		// Act & Assert
		decision1.ShouldNotBe(decision2);
	}

	[Fact]
	public void Equality_DifferentReason_AreNotEqual()
	{
		// Arrange
		var decision1 = new AuthorizationDecision(AuthorizationEffect.Deny, "Reason 1");
		var decision2 = new AuthorizationDecision(AuthorizationEffect.Deny, "Reason 2");

		// Act & Assert
		decision1.ShouldNotBe(decision2);
	}

	[Fact]
	public void With_CreatesModifiedCopy()
	{
		// Arrange
		var original = new AuthorizationDecision(AuthorizationEffect.Permit);

		// Act
		var modified = original with { Effect = AuthorizationEffect.Deny };

		// Assert
		original.Effect.ShouldBe(AuthorizationEffect.Permit);
		modified.Effect.ShouldBe(AuthorizationEffect.Deny);
	}

	[Fact]
	public void Create_WithIndeterminate_SetsCorrectEffect()
	{
		// Arrange & Act
		var decision = new AuthorizationDecision(AuthorizationEffect.Indeterminate, "Missing context");

		// Assert
		decision.Effect.ShouldBe(AuthorizationEffect.Indeterminate);
		decision.Reason.ShouldBe("Missing context");
	}
}
