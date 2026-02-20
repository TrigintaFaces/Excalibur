using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CompressionOptionsShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        var options = new CompressionOptions();

        options.Enabled.ShouldBeFalse();
        options.CompressionType.ShouldBe(CompressionType.Gzip);
        options.CompressionLevel.ShouldBe(6);
        options.MinimumSizeThreshold.ShouldBe(1024);
    }

    [Fact]
    public void AllowSettingAllProperties()
    {
        var options = new CompressionOptions
        {
            Enabled = true,
            CompressionType = CompressionType.Brotli,
            CompressionLevel = 9,
            MinimumSizeThreshold = 512,
        };

        options.Enabled.ShouldBeTrue();
        options.CompressionType.ShouldBe(CompressionType.Brotli);
        options.CompressionLevel.ShouldBe(9);
        options.MinimumSizeThreshold.ShouldBe(512);
    }

    [Fact]
    public void AllowZeroCompressionLevel()
    {
        var options = new CompressionOptions { CompressionLevel = 0 };
        options.CompressionLevel.ShouldBe(0);
    }

    [Fact]
    public void AllowZeroSizeThreshold()
    {
        var options = new CompressionOptions { MinimumSizeThreshold = 0 };
        options.MinimumSizeThreshold.ShouldBe(0);
    }
}
