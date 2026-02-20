using Excalibur.A3.Abstractions.Authorization;

namespace Excalibur.Tests.A3.Abstractions;

/// <summary>
/// Unit tests for AuthorizationEffect enum.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AuthorizationEffectShould : UnitTestBase
{
	[Fact]
	public void HaveExpectedValues()
	{
		// Assert
		((int)AuthorizationEffect.Permit).ShouldBe(0);
		((int)AuthorizationEffect.Deny).ShouldBe(1);
		((int)AuthorizationEffect.Indeterminate).ShouldBe(2);
	}

	[Theory]
	[InlineData(AuthorizationEffect.Permit)]
	[InlineData(AuthorizationEffect.Deny)]
	[InlineData(AuthorizationEffect.Indeterminate)]
	public void BeDefinedForAllValues(AuthorizationEffect effect)
	{
		// Act & Assert
		Enum.IsDefined(effect).ShouldBeTrue();
	}

	[Fact]
	public void Permit_BeDefaultValue()
	{
		// Arrange & Act
		var defaultEffect = default(AuthorizationEffect);

		// Assert
		defaultEffect.ShouldBe(AuthorizationEffect.Permit);
	}

	[Theory]
	[InlineData(0, AuthorizationEffect.Permit)]
	[InlineData(1, AuthorizationEffect.Deny)]
	[InlineData(2, AuthorizationEffect.Indeterminate)]
	public void CastFromInt_ReturnsCorrectValue(int value, AuthorizationEffect expected)
	{
		// Act
		var effect = (AuthorizationEffect)value;

		// Assert
		effect.ShouldBe(expected);
	}

	[Fact]
	public void HaveThreeDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<AuthorizationEffect>();

		// Assert
		values.Length.ShouldBe(3);
		values.Distinct().Count().ShouldBe(3);
	}
}
