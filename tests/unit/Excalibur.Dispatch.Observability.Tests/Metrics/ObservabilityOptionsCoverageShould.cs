// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ObservabilityOptionsCoverageShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        // Act
        var options = new ObservabilityOptions();

        // Assert
        options.EnableMetrics.ShouldBeTrue();
        options.EnableTracing.ShouldBeTrue();
        options.EnableLogging.ShouldBeTrue();
        options.ActivitySourceName.ShouldBe("Excalibur.Dispatch");
        options.MeterName.ShouldBe(DispatchMetrics.MeterName);
        options.ServiceName.ShouldBe("Excalibur.Dispatch");
        options.ServiceVersion.ShouldBe("1.0.0");
        options.EnableDetailedTiming.ShouldBeFalse();
        options.IncludeSensitiveData.ShouldBeFalse();
    }

    [Fact]
    public void SetAllProperties()
    {
        // Act
        var options = new ObservabilityOptions
        {
            EnableMetrics = false,
            EnableTracing = false,
            EnableLogging = false,
            ActivitySourceName = "MyApp",
            MeterName = "MyMeter",
            ServiceName = "MyService",
            ServiceVersion = "2.0.0",
            EnableDetailedTiming = true,
            IncludeSensitiveData = true,
        };

        // Assert
        options.EnableMetrics.ShouldBeFalse();
        options.EnableTracing.ShouldBeFalse();
        options.EnableLogging.ShouldBeFalse();
        options.ActivitySourceName.ShouldBe("MyApp");
        options.MeterName.ShouldBe("MyMeter");
        options.ServiceName.ShouldBe("MyService");
        options.ServiceVersion.ShouldBe("2.0.0");
        options.EnableDetailedTiming.ShouldBeTrue();
        options.IncludeSensitiveData.ShouldBeTrue();
    }
}
