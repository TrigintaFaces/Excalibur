using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class LoggingOptionsShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        var options = new LoggingOptions();

        options.EnhancedLogging.ShouldBeFalse();
        options.IncludeCorrelationIds.ShouldBeTrue();
        options.IncludeExecutionContext.ShouldBeTrue();
    }

    [Fact]
    public void AllowSettingAllProperties()
    {
        var options = new LoggingOptions
        {
            EnhancedLogging = true,
            IncludeCorrelationIds = false,
            IncludeExecutionContext = false,
        };

        options.EnhancedLogging.ShouldBeTrue();
        options.IncludeCorrelationIds.ShouldBeFalse();
        options.IncludeExecutionContext.ShouldBeFalse();
    }
}
