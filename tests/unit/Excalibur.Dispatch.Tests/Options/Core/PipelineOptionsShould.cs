using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PipelineOptionsShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        var options = new PipelineOptions();

        options.MaxConcurrency.ShouldBe(Environment.ProcessorCount * 2);
        options.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(30));
        options.EnableParallelProcessing.ShouldBeTrue();
        options.StopOnFirstError.ShouldBeFalse();
        options.BufferSize.ShouldBe(1000);
        options.ApplicableMessageKinds.ShouldBe(MessageKinds.Action | MessageKinds.Event | MessageKinds.Document);
    }

    [Fact]
    public void AllowSettingAllProperties()
    {
        var options = new PipelineOptions
        {
            MaxConcurrency = 10,
            DefaultTimeout = TimeSpan.FromMinutes(1),
            EnableParallelProcessing = false,
            StopOnFirstError = true,
            BufferSize = 500,
            ApplicableMessageKinds = MessageKinds.Action,
        };

        options.MaxConcurrency.ShouldBe(10);
        options.DefaultTimeout.ShouldBe(TimeSpan.FromMinutes(1));
        options.EnableParallelProcessing.ShouldBeFalse();
        options.StopOnFirstError.ShouldBeTrue();
        options.BufferSize.ShouldBe(500);
        options.ApplicableMessageKinds.ShouldBe(MessageKinds.Action);
    }

    [Fact]
    public void AllowMinimumConcurrency()
    {
        var options = new PipelineOptions { MaxConcurrency = 1 };
        options.MaxConcurrency.ShouldBe(1);
    }
}
