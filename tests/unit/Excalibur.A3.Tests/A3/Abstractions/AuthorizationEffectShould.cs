using Excalibur.A3.Authorization;

namespace Excalibur.Tests.A3.Abstractions;

/// <summary>
/// Unit tests for AuthorizationEffect enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class AuthorizationEffectShould : UnitTestBase
{
	[Fact]
	public void HaveExpectedValues()
	{
		// Assert
		// Deny is the zero value so a default-initialized effect fails closed (39wqia).
		((int)AuthorizationEffect.Deny).ShouldBe(0);
		((int)AuthorizationEffect.Permit).ShouldBe(1);
	}

	[Theory]
	[InlineData(AuthorizationEffect.Permit)]
	[InlineData(AuthorizationEffect.Deny)]
	public void BeDefinedForAllValues(AuthorizationEffect effect)
	{
		// Act & Assert
		Enum.IsDefined(effect).ShouldBeTrue();
	}

	[Fact]
	public void Deny_BeDefaultValue()
	{
		// Arrange & Act
		var defaultEffect = default(AuthorizationEffect);

		// Assert
		// A default-initialized effect MUST fail closed (deny), never accidentally permit (39wqia).
		defaultEffect.ShouldBe(AuthorizationEffect.Deny);
	}

	[Theory]
	[InlineData(0, AuthorizationEffect.Deny)]
	[InlineData(1, AuthorizationEffect.Permit)]
	public void CastFromInt_ReturnsCorrectValue(int value, AuthorizationEffect expected)
	{
		// Act
		var effect = (AuthorizationEffect)value;

		// Assert
		effect.ShouldBe(expected);
	}

	[Fact]
	public void HaveTwoDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<AuthorizationEffect>();

		// Assert
		// Permit and Deny only: an evaluator that cannot decide returns Deny, so there is no third effect.
		values.Length.ShouldBe(2);
		values.Distinct().Count().ShouldBe(2);
	}
}
