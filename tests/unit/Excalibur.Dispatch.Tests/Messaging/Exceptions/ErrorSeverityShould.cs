using Excalibur.Dispatch.Exceptions;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ErrorSeverityShould
{
	[Theory]
	[InlineData(ErrorSeverity.Information, 0)]
	[InlineData(ErrorSeverity.Warning, 1)]
	[InlineData(ErrorSeverity.Error, 2)]
	[InlineData(ErrorSeverity.Critical, 3)]
	[InlineData(ErrorSeverity.Fatal, 4)]
	public void HaveExpectedValues(ErrorSeverity severity, int expected)
	{
		((int)severity).ShouldBe(expected);
	}

	[Fact]
	public void HaveFiveMembers()
	{
		Enum.GetValues<ErrorSeverity>().Length.ShouldBe(5);
	}
}
