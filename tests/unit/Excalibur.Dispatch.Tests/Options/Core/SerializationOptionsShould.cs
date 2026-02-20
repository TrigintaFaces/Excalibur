using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SerializationOptionsShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        var options = new SerializationOptions();

        options.EmbedMessageType.ShouldBeTrue();
        options.IncludeAssemblyInfo.ShouldBeFalse();
        options.DefaultBufferSize.ShouldBe(4096);
        options.UseBufferPooling.ShouldBeTrue();
    }

    [Fact]
    public void AllowSettingAllProperties()
    {
        var options = new SerializationOptions
        {
            EmbedMessageType = false,
            IncludeAssemblyInfo = true,
            DefaultBufferSize = 8192,
            UseBufferPooling = false,
        };

        options.EmbedMessageType.ShouldBeFalse();
        options.IncludeAssemblyInfo.ShouldBeTrue();
        options.DefaultBufferSize.ShouldBe(8192);
        options.UseBufferPooling.ShouldBeFalse();
    }

    [Fact]
    public void AllowMinimumBufferSize()
    {
        var options = new SerializationOptions { DefaultBufferSize = 1 };
        options.DefaultBufferSize.ShouldBe(1);
    }
}
