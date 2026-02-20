using Excalibur.Dispatch.Exceptions;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ErrorCategoryShould
{
	[Theory]
	[InlineData(ErrorCategory.Unknown, 0)]
	[InlineData(ErrorCategory.Configuration, 1)]
	[InlineData(ErrorCategory.Validation, 2)]
	[InlineData(ErrorCategory.Messaging, 3)]
	[InlineData(ErrorCategory.Serialization, 4)]
	[InlineData(ErrorCategory.Network, 5)]
	[InlineData(ErrorCategory.Security, 6)]
	[InlineData(ErrorCategory.Data, 7)]
	[InlineData(ErrorCategory.Timeout, 8)]
	[InlineData(ErrorCategory.Resource, 9)]
	[InlineData(ErrorCategory.System, 10)]
	[InlineData(ErrorCategory.Resilience, 11)]
	[InlineData(ErrorCategory.Concurrency, 12)]
	public void HaveExpectedValues(ErrorCategory category, int expected)
	{
		((int)category).ShouldBe(expected);
	}

	[Fact]
	public void HaveThirteenMembers()
	{
		Enum.GetValues<ErrorCategory>().Length.ShouldBe(13);
	}
}
