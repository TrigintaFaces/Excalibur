// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.GoogleCloud;

using Tests.Shared.Helpers;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

/// <summary>
/// bd-maebum (S859, GCP half of the F-5 cross-provider fold-in) — independent engage-test (author≠impl)
/// for the Google Cloud Functions telemetry honest-log fix. Mirrors the AWS/Azure locks so the honest trail
/// is consistent across all three serverless providers. Non-vacuous: RED on the pre-fix Warning surface.
/// </summary>
[Collection("EnvironmentVariableTests")]
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Trait("Feature", "Telemetry")]
public sealed class GoogleCloudFunctionsTelemetryHonestLogShould : UnitTestBase
{
    private const int CloudTraceEventId = 50409;      // GoogleCloudFunctionsEventId.ConfiguringCloudTrace
    private const int CloudMonitoringEventId = 50410; // GoogleCloudFunctionsEventId.ConfiguringCloudMonitoring

    private static ServerlessHostOptions WithTelemetry(bool tracing, bool metrics)
    {
        var options = new ServerlessHostOptions();
        options.Telemetry.EnableDistributedTracing = tracing;
        options.Telemetry.EnableMetrics = metrics;
        return options;
    }

    [Fact]
    public void LogTelemetryAsPlatformProvisioned_AtInformation_WhenFlagsEnabled()
    {
        var logger = new CapturingLogger<GoogleCloudFunctionsHostProvider>();
        var sut = new GoogleCloudFunctionsHostProvider(logger);

        Should.NotThrow(() => sut.ConfigureServices(new ServiceCollection(), WithTelemetry(tracing: true, metrics: true)));

        var tracing = logger.Entries.Where(e => e.EventId.Id == CloudTraceEventId).ToList();
        tracing.Count.ShouldBe(1);
        tracing[0].Level.ShouldBe(LogLevel.Information);
        tracing[0].Message.ShouldContain("platform-provisioned");
        tracing[0].Message.ShouldContain("in-process exporters");

        var metrics = logger.Entries.Where(e => e.EventId.Id == CloudMonitoringEventId).ToList();
        metrics.Count.ShouldBe(1);
        metrics[0].Level.ShouldBe(LogLevel.Information);
        metrics[0].Message.ShouldContain("platform-provisioned");
        metrics[0].Message.ShouldContain("in-process exporters");
    }

    [Fact]
    public void NeverLogWarning_NorClaimNotYetImplemented_WhenTelemetryEnabled()
    {
        var logger = new CapturingLogger<GoogleCloudFunctionsHostProvider>();
        var sut = new GoogleCloudFunctionsHostProvider(logger);

        sut.ConfigureServices(new ServiceCollection(), WithTelemetry(tracing: true, metrics: true));

        logger.Entries.ShouldNotContain(e => e.Level == LogLevel.Warning);
        logger.Entries.ShouldNotContain(e => e.Message.Contains("not yet implemented", StringComparison.OrdinalIgnoreCase));
        logger.Entries.ShouldNotContain(e => e.Message.Contains("will not be", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NotLogTelemetryNotices_WhenFlagsDisabled()
    {
        var logger = new CapturingLogger<GoogleCloudFunctionsHostProvider>();
        var sut = new GoogleCloudFunctionsHostProvider(logger);

        sut.ConfigureServices(new ServiceCollection(), WithTelemetry(tracing: false, metrics: false));

        logger.Entries.ShouldNotContain(e => e.EventId.Id == CloudTraceEventId);
        logger.Entries.ShouldNotContain(e => e.EventId.Id == CloudMonitoringEventId);
    }
}
