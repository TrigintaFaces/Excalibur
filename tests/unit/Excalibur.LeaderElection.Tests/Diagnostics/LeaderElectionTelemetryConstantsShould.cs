// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.LeaderElection.Diagnostics;

namespace Excalibur.LeaderElection.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="LeaderElectionTelemetryConstants"/>.
/// Verifies all telemetry constant values follow naming conventions.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Trait("Feature", "LeaderElection")]
public sealed class LeaderElectionTelemetryConstantsShould
{
	// --- Top-level constants ---

	[Fact]
	public void HaveCorrectMeterName()
	{
		// Assert
		LeaderElectionTelemetryConstants.MeterName.ShouldBe("Excalibur.LeaderElection");
	}

	[Fact]
	public void HaveCorrectActivitySourceName()
	{
		// Assert
		LeaderElectionTelemetryConstants.ActivitySourceName.ShouldBe("Excalibur.LeaderElection");
	}

	// --- MetricNames ---

	[Fact]
	public void HaveCorrectAcquisitionsMetricName()
	{
		// Assert
		LeaderElectionTelemetryConstants.MetricNames.Acquisitions
			.ShouldBe("excalibur.leaderelection.acquisitions");
	}

	[Fact]
	public void HaveCorrectLeaseDurationMetricName()
	{
		// Assert
		LeaderElectionTelemetryConstants.MetricNames.LeaseDuration
			.ShouldBe("excalibur.leaderelection.lease_duration");
	}

	[Fact]
	public void HaveCorrectIsLeaderMetricName()
	{
		// Assert
		LeaderElectionTelemetryConstants.MetricNames.IsLeader
			.ShouldBe("excalibur.leaderelection.is_leader");
	}

	// --- Tags ---

	[Fact]
	public void HaveCorrectInstanceTagName()
	{
		// Assert
		LeaderElectionTelemetryConstants.TagNames.Instance
			.ShouldBe("excalibur.leaderelection.instance");
	}

	[Fact]
	public void HaveCorrectResultTagName()
	{
		// Assert
		LeaderElectionTelemetryConstants.TagNames.Result
			.ShouldBe("excalibur.leaderelection.result");
	}

	[Fact]
	public void HaveCorrectProviderTagName()
	{
		// Assert
		LeaderElectionTelemetryConstants.TagNames.Provider
			.ShouldBe("excalibur.leaderelection.provider");
	}

	// --- Naming convention verification ---

	[Fact]
	public void HaveMetricNamesFollowingDotSeparatedLowercase()
	{
		// All metric names should follow OpenTelemetry semantic convention: lowercase, dot-separated
		LeaderElectionTelemetryConstants.MetricNames.Acquisitions.ShouldMatch(@"^[a-z._]+$");
		LeaderElectionTelemetryConstants.MetricNames.LeaseDuration.ShouldMatch(@"^[a-z._]+$");
		LeaderElectionTelemetryConstants.MetricNames.IsLeader.ShouldMatch(@"^[a-z._]+$");
	}

	[Fact]
	public void HaveTagNamesFollowingDotSeparatedLowercase()
	{
		// All tag names should follow OpenTelemetry semantic convention: lowercase, dot-separated
		LeaderElectionTelemetryConstants.TagNames.Instance.ShouldMatch(@"^[a-z._]+$");
		LeaderElectionTelemetryConstants.TagNames.Result.ShouldMatch(@"^[a-z._]+$");
		LeaderElectionTelemetryConstants.TagNames.Provider.ShouldMatch(@"^[a-z._]+$");
	}

	[Fact]
	public void HaveMetricNamesPrefixedWithExcaliburLeaderElection()
	{
		// Assert
		LeaderElectionTelemetryConstants.MetricNames.Acquisitions.ShouldStartWith("excalibur.leaderelection.");
		LeaderElectionTelemetryConstants.MetricNames.LeaseDuration.ShouldStartWith("excalibur.leaderelection.");
		LeaderElectionTelemetryConstants.MetricNames.IsLeader.ShouldStartWith("excalibur.leaderelection.");
	}

	[Fact]
	public void HaveTagNamesPrefixedWithExcaliburLeaderElection()
	{
		// Assert
		LeaderElectionTelemetryConstants.TagNames.Instance.ShouldStartWith("excalibur.leaderelection.");
		LeaderElectionTelemetryConstants.TagNames.Result.ShouldStartWith("excalibur.leaderelection.");
		LeaderElectionTelemetryConstants.TagNames.Provider.ShouldStartWith("excalibur.leaderelection.");
	}
}
