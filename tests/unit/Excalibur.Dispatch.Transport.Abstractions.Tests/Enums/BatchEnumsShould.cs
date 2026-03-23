using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Enums;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class BatchEnumsShould
{
    [Theory]
    [InlineData(BatchCompletionStrategy.Size, 0)]
    [InlineData(BatchCompletionStrategy.Time, 1)]
    [InlineData(BatchCompletionStrategy.SizeOrTime, 2)]
    [InlineData(BatchCompletionStrategy.Dynamic, 3)]
    [InlineData(BatchCompletionStrategy.ContentBased, 4)]
    public void BatchCompletionStrategy_Should_Have_Correct_Values(BatchCompletionStrategy strategy, int expected)
    {
        ((int)strategy).ShouldBe(expected);
    }

    [Theory]
    [InlineData(BatchPriority.Low, 0)]
    [InlineData(BatchPriority.Normal, 1)]
    [InlineData(BatchPriority.High, 2)]
    [InlineData(BatchPriority.Critical, 3)]
    public void BatchPriority_Should_Have_Correct_Values(BatchPriority priority, int expected)
    {
        ((int)priority).ShouldBe(expected);
    }

    [Theory]
    [InlineData(BatchErrorSeverity.Info, 0)]
    [InlineData(BatchErrorSeverity.Warning, 1)]
    [InlineData(BatchErrorSeverity.Error, 2)]
    [InlineData(BatchErrorSeverity.Critical, 3)]
    public void BatchErrorSeverity_Should_Have_Correct_Values(BatchErrorSeverity severity, int expected)
    {
        ((int)severity).ShouldBe(expected);
    }
}
