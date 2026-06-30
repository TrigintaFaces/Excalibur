// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.AwsLambda;

using Tests.Shared.Helpers;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

/// <summary>
/// Independent engage-test (author≠impl) for the AWS Lambda telemetry honest-log fix.
/// </summary>
/// <remarks>
/// Distributed tracing / metrics on Lambda are platform-provisioned (X-Ray + CloudWatch); the provider does
/// NOT wire in-process exporters for those flags, so enabling <c>EnableDistributedTracing</c> /
/// <c>EnableMetrics</c> on this host is a no-op. The provider warns the operator about that no-op with an
/// ACCURATE, NON-APOLOGETIC message — it states the platform-provisioning behavior, never "feature missing /
/// not yet implemented / unwired". Critically, the warn is FAIL-OPEN: enabling telemetry never throws and never
/// breaks host startup. This lock binds: Warning level, truthful "platform-provisioned / no-op" wording, the
/// absence of apologetic "not implemented" phrasing, and the inviolable "simple host starts clean" invariant.
/// It is non-vacuous: RED on any impl that logs at a non-Warning level, that uses apologetic "not implemented"
/// wording, or that omits the telemetry notice entirely.
/// </remarks>
[Collection("EnvironmentVariableTests")]
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Trait("Feature", "Telemetry")]
public sealed class AwsLambdaTelemetryHonestLogShould : UnitTestBase
{
    private const int XRayEventId = 50109;   // AwsLambdaEventId.ConfiguringXRayTracing
    private const int MetricsEventId = 50110; // AwsLambdaEventId.ConfiguringMetrics

    private static ServerlessHostOptions WithTelemetry(bool tracing, bool metrics)
    {
        var options = new ServerlessHostOptions();
        options.Telemetry.EnableDistributedTracing = tracing;
        options.Telemetry.EnableMetrics = metrics;
        return options;
    }

    [Fact]
    public void WarnTelemetryIsPlatformProvisioned_WhenFlagsEnabled()
    {
        // Arrange
        var logger = new CapturingLogger<AwsLambdaHostProvider>();
        var sut = new AwsLambdaHostProvider(logger);

        // Act — enabling both telemetry flags must NOT throw (fail-open) and must warn honestly.
        Should.NotThrow(() => sut.ConfigureServices(new ServiceCollection(), WithTelemetry(tracing: true, metrics: true)));

        // Assert — honest Warning-level entries: truthful "platform-provisioned / no-op" wording stating the
        // toggle does not wire an in-process exporter on this host (behavior, not a work-item ref).
        var xrayEntries = logger.Entries.Where(e => e.EventId.Id == XRayEventId).ToList();
        xrayEntries.Count.ShouldBe(1);
        var xray = xrayEntries[0];
        xray.Level.ShouldBe(LogLevel.Warning);
        xray.Message.ShouldContain("platform-provisioned");
        xray.Message.ShouldContain("X-Ray");
        xray.Message.ShouldContain("no-op");

        var metricsEntries = logger.Entries.Where(e => e.EventId.Id == MetricsEventId).ToList();
        metricsEntries.Count.ShouldBe(1);
        var metrics = metricsEntries[0];
        metrics.Level.ShouldBe(LogLevel.Warning);
        metrics.Message.ShouldContain("platform-provisioned");
        metrics.Message.ShouldContain("CloudWatch");
        metrics.Message.ShouldContain("no-op");
    }

    [Fact]
    public void WarnHonestly_NeverApologetic_NorClaimNotImplemented_WhenTelemetryEnabled()
    {
        // Arrange
        var logger = new CapturingLogger<AwsLambdaHostProvider>();
        var sut = new AwsLambdaHostProvider(logger);

        // Act
        sut.ConfigureServices(new ServiceCollection(), WithTelemetry(tracing: true, metrics: true));

        // Assert — the warn is honest, not apologetic: NO "not implemented / feature missing / unwired" wording.
        // RED on any impl that frames the platform-provisioned no-op as a missing/unwired feature.
        logger.Entries.ShouldNotContain(e => e.Message.Contains("not yet implemented", StringComparison.OrdinalIgnoreCase));
        logger.Entries.ShouldNotContain(e => e.Message.Contains("not implemented", StringComparison.OrdinalIgnoreCase));
        logger.Entries.ShouldNotContain(e => e.Message.Contains("feature missing", StringComparison.OrdinalIgnoreCase));
        logger.Entries.ShouldNotContain(e => e.Message.Contains("unwired", StringComparison.OrdinalIgnoreCase));
        logger.Entries.ShouldNotContain(e => e.Message.Contains("will not be", StringComparison.OrdinalIgnoreCase));

        // And fail-open: no Error-level entry from enabling telemetry.
        logger.HasLoggedError().ShouldBeFalse();
    }

    [Fact]
    public void NotLogTelemetryNotices_WhenFlagsDisabled()
    {
        // Arrange
        var logger = new CapturingLogger<AwsLambdaHostProvider>();
        var sut = new AwsLambdaHostProvider(logger);

        // Act — the no-telemetry config is the common simple-host case; it must emit no telemetry notice.
        sut.ConfigureServices(new ServiceCollection(), WithTelemetry(tracing: false, metrics: false));

        // Assert
        logger.Entries.ShouldNotContain(e => e.EventId.Id == XRayEventId);
        logger.Entries.ShouldNotContain(e => e.EventId.Id == MetricsEventId);
    }
}
