using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TracingOptionsShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        var options = new TracingOptions();

        options.Enabled.ShouldBeFalse();
        options.SamplingRatio.ShouldBe(1.0);
        options.IncludeSensitiveData.ShouldBeFalse();
    }

    [Fact]
    public void AllowSettingAllProperties()
    {
        var options = new TracingOptions
        {
            Enabled = true,
            SamplingRatio = 0.5,
            IncludeSensitiveData = true,
        };

        options.Enabled.ShouldBeTrue();
        options.SamplingRatio.ShouldBe(0.5);
        options.IncludeSensitiveData.ShouldBeTrue();
    }

    [Fact]
    public void AllowZeroSamplingRatio()
    {
        var options = new TracingOptions { SamplingRatio = 0.0 };
        options.SamplingRatio.ShouldBe(0.0);
    }

    [Fact]
    public void AllowFullSamplingRatio()
    {
        var options = new TracingOptions { SamplingRatio = 1.0 };
        options.SamplingRatio.ShouldBe(1.0);
    }
}
