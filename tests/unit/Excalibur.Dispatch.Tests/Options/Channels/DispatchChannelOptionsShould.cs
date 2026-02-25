using System.Threading.Channels;
using Excalibur.Dispatch.Channels;
using Excalibur.Dispatch.Options.Channels;

namespace Excalibur.Dispatch.Tests.Options.Channels;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DispatchChannelOptionsShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        var options = new DispatchChannelOptions();

        options.Mode.ShouldBe(ChannelMode.Unbounded);
        options.Capacity.ShouldBeNull();
        options.FullMode.ShouldBe(BoundedChannelFullMode.Wait);
        options.SingleReader.ShouldBeFalse();
        options.SingleWriter.ShouldBeFalse();
        options.AllowSynchronousContinuations.ShouldBeTrue();
        options.WaitStrategy.ShouldBeNull();
    }

    [Fact]
    public void AllowSettingAllProperties()
    {
        var options = new DispatchChannelOptions
        {
            Mode = ChannelMode.Bounded,
            Capacity = 500,
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = false,
        };

        options.Mode.ShouldBe(ChannelMode.Bounded);
        options.Capacity.ShouldBe(500);
        options.FullMode.ShouldBe(BoundedChannelFullMode.DropOldest);
        options.SingleReader.ShouldBeTrue();
        options.SingleWriter.ShouldBeTrue();
        options.AllowSynchronousContinuations.ShouldBeFalse();
    }
}
