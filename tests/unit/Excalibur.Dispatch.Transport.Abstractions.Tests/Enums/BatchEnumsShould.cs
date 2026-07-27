using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Enums;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class BatchEnumsShould
{
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
