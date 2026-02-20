using Excalibur.Dispatch.Compliance.Restriction;

namespace Excalibur.Dispatch.Compliance.Tests.Restriction;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class RestrictionReasonShould
{
	[Theory]
	[InlineData(RestrictionReason.AccuracyContested, 0)]
	[InlineData(RestrictionReason.UnlawfulProcessing, 1)]
	[InlineData(RestrictionReason.ErasureObjected, 2)]
	[InlineData(RestrictionReason.LegalClaim, 3)]
	public void Have_expected_integer_values(RestrictionReason reason, int expectedValue)
	{
		((int)reason).ShouldBe(expectedValue);
	}

	[Fact]
	public void Have_exactly_four_values()
	{
		var values = Enum.GetValues<RestrictionReason>();

		values.Length.ShouldBe(4);
	}
}
