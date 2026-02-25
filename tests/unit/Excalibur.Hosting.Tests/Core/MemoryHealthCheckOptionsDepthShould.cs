using Excalibur.Hosting;

namespace Excalibur.Hosting.Tests.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class MemoryHealthCheckOptionsDepthShould
{
    [Fact]
    public void DefaultValues_SetCorrectly()
    {
        // Act
        var options = new MemoryHealthCheckOptions();

        // Assert
        options.AllocatedMemoryThresholdKB.ShouldBe(512 * 1024); // 512 MB
        options.WorkingSetThresholdBytes.ShouldBe(1L * 1024 * 1024 * 1024); // 1 GB
    }

    [Fact]
    public void AllocatedMemoryThresholdKB_CanBeCustomized()
    {
        // Act
        var options = new MemoryHealthCheckOptions
        {
            AllocatedMemoryThresholdKB = 256 * 1024,
        };

        // Assert
        options.AllocatedMemoryThresholdKB.ShouldBe(256 * 1024);
    }

    [Fact]
    public void WorkingSetThresholdBytes_CanBeCustomized()
    {
        // Act
        var options = new MemoryHealthCheckOptions
        {
            WorkingSetThresholdBytes = 2L * 1024 * 1024 * 1024,
        };

        // Assert
        options.WorkingSetThresholdBytes.ShouldBe(2L * 1024 * 1024 * 1024);
    }

    [Fact]
    public void DefaultAllocatedThreshold_Is512MB()
    {
        var options = new MemoryHealthCheckOptions();
        var thresholdMB = options.AllocatedMemoryThresholdKB / 1024;
        thresholdMB.ShouldBe(512);
    }

    [Fact]
    public void DefaultWorkingSetThreshold_Is1GB()
    {
        var options = new MemoryHealthCheckOptions();
        var thresholdGB = options.WorkingSetThresholdBytes / (1024.0 * 1024 * 1024);
        thresholdGB.ShouldBe(1.0);
    }
}
