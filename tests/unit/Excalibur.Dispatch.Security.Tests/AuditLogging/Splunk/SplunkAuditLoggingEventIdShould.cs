// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.AuditLogging.Splunk;

namespace Excalibur.Dispatch.Security.Tests.AuditLogging.Splunk;

/// <summary>
/// Unit tests for <see cref="SplunkAuditLoggingEventId"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "AuditLogging")]
public sealed class SplunkAuditLoggingEventIdShould
{
	#region Event ID Value Tests

	[Fact]
	public void HaveCorrectEventForwardedValue()
	{
		// Assert
		SplunkAuditLoggingEventId.EventForwarded.ShouldBe(93401);
	}

	[Fact]
	public void HaveCorrectBatchForwardedValue()
	{
		// Assert
		SplunkAuditLoggingEventId.BatchForwarded.ShouldBe(93402);
	}

	[Fact]
	public void HaveCorrectForwardFailedStatusValue()
	{
		// Assert
		SplunkAuditLoggingEventId.ForwardFailedStatus.ShouldBe(93405);
	}

	[Fact]
	public void HaveCorrectForwardFailedHttpErrorValue()
	{
		// Assert
		SplunkAuditLoggingEventId.ForwardFailedHttpError.ShouldBe(93408);
	}

	[Fact]
	public void HaveCorrectForwardFailedTimeoutValue()
	{
		// Assert
		SplunkAuditLoggingEventId.ForwardFailedTimeout.ShouldBe(93409);
	}

	[Fact]
	public void HaveCorrectForwardFailedBatchChunkValue()
	{
		// Assert
		SplunkAuditLoggingEventId.ForwardFailedBatchChunk.ShouldBe(93410);
	}

	[Fact]
	public void HaveCorrectForwardRetriedValue()
	{
		// Assert
		SplunkAuditLoggingEventId.ForwardRetried.ShouldBe(93406);
	}

	[Fact]
	public void HaveCorrectHealthCheckFailedValue()
	{
		// Assert
		SplunkAuditLoggingEventId.HealthCheckFailed.ShouldBe(93407);
	}

	#endregion Event ID Value Tests

	#region Event ID Range Tests

	[Fact]
	public void HaveEventIdsInExpectedRange()
	{
		// Assert - Event IDs should be in the 93400-93499 range
		SplunkAuditLoggingEventId.EventForwarded.ShouldBeInRange(93400, 93499);
		SplunkAuditLoggingEventId.BatchForwarded.ShouldBeInRange(93400, 93499);
		SplunkAuditLoggingEventId.ForwardFailedStatus.ShouldBeInRange(93400, 93499);
		SplunkAuditLoggingEventId.ForwardFailedHttpError.ShouldBeInRange(93400, 93499);
		SplunkAuditLoggingEventId.ForwardFailedTimeout.ShouldBeInRange(93400, 93499);
		SplunkAuditLoggingEventId.ForwardFailedBatchChunk.ShouldBeInRange(93400, 93499);
		SplunkAuditLoggingEventId.ForwardRetried.ShouldBeInRange(93400, 93499);
		SplunkAuditLoggingEventId.HealthCheckFailed.ShouldBeInRange(93400, 93499);
	}

	[Fact]
	public void HaveUniqueEventIds()
	{
		// Arrange
		var eventIds = new[]
		{
			SplunkAuditLoggingEventId.EventForwarded,
			SplunkAuditLoggingEventId.BatchForwarded,
			SplunkAuditLoggingEventId.ForwardFailedStatus,
			SplunkAuditLoggingEventId.ForwardFailedHttpError,
			SplunkAuditLoggingEventId.ForwardFailedTimeout,
			SplunkAuditLoggingEventId.ForwardFailedBatchChunk,
			SplunkAuditLoggingEventId.ForwardRetried,
			SplunkAuditLoggingEventId.HealthCheckFailed
		};

		// Assert
		eventIds.ShouldBeUnique();
	}

	#endregion Event ID Range Tests
}
