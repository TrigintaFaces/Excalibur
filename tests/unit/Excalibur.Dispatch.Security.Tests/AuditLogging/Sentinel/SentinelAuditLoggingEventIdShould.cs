// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.AuditLogging.Sentinel;

namespace Excalibur.Dispatch.Security.Tests.AuditLogging.Sentinel;

/// <summary>
/// Unit tests for <see cref="SentinelAuditLoggingEventId"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "AuditLogging")]
public sealed class SentinelAuditLoggingEventIdShould
{
	#region Event ID Value Tests

	[Fact]
	public void HaveCorrectEventForwardedValue()
	{
		// Assert
		SentinelAuditLoggingEventId.EventForwarded.ShouldBe(93440);
	}

	[Fact]
	public void HaveCorrectBatchForwardedValue()
	{
		// Assert
		SentinelAuditLoggingEventId.BatchForwarded.ShouldBe(93441);
	}

	[Fact]
	public void HaveCorrectForwardFailedStatusValue()
	{
		// Assert
		SentinelAuditLoggingEventId.ForwardFailedStatus.ShouldBe(93445);
	}

	[Fact]
	public void HaveCorrectForwardRetriedValue()
	{
		// Assert
		SentinelAuditLoggingEventId.ForwardRetried.ShouldBe(93446);
	}

	[Fact]
	public void HaveCorrectHealthCheckFailedValue()
	{
		// Assert
		SentinelAuditLoggingEventId.HealthCheckFailed.ShouldBe(93447);
	}

	[Fact]
	public void HaveCorrectForwardFailedHttpErrorValue()
	{
		// Assert
		SentinelAuditLoggingEventId.ForwardFailedHttpError.ShouldBe(93448);
	}

	[Fact]
	public void HaveCorrectForwardFailedTimeoutValue()
	{
		// Assert
		SentinelAuditLoggingEventId.ForwardFailedTimeout.ShouldBe(93449);
	}

	[Fact]
	public void HaveCorrectForwardFailedBatchChunkValue()
	{
		// Assert
		SentinelAuditLoggingEventId.ForwardFailedBatchChunk.ShouldBe(93450);
	}

	#endregion Event ID Value Tests

	#region Event ID Range Tests

	[Fact]
	public void HaveEventIdsInExpectedRange()
	{
		// Assert - Event IDs should be in the 93440-93459 range
		SentinelAuditLoggingEventId.EventForwarded.ShouldBeInRange(93440, 93459);
		SentinelAuditLoggingEventId.BatchForwarded.ShouldBeInRange(93440, 93459);
		SentinelAuditLoggingEventId.ForwardFailedStatus.ShouldBeInRange(93440, 93459);
		SentinelAuditLoggingEventId.ForwardRetried.ShouldBeInRange(93440, 93459);
		SentinelAuditLoggingEventId.HealthCheckFailed.ShouldBeInRange(93440, 93459);
		SentinelAuditLoggingEventId.ForwardFailedHttpError.ShouldBeInRange(93440, 93459);
		SentinelAuditLoggingEventId.ForwardFailedTimeout.ShouldBeInRange(93440, 93459);
		SentinelAuditLoggingEventId.ForwardFailedBatchChunk.ShouldBeInRange(93440, 93459);
	}

	[Fact]
	public void HaveUniqueEventIds()
	{
		// Arrange
		var eventIds = new[]
		{
			SentinelAuditLoggingEventId.EventForwarded,
			SentinelAuditLoggingEventId.BatchForwarded,
			SentinelAuditLoggingEventId.ForwardFailedStatus,
			SentinelAuditLoggingEventId.ForwardRetried,
			SentinelAuditLoggingEventId.HealthCheckFailed,
			SentinelAuditLoggingEventId.ForwardFailedHttpError,
			SentinelAuditLoggingEventId.ForwardFailedTimeout,
			SentinelAuditLoggingEventId.ForwardFailedBatchChunk
		};

		// Assert
		eventIds.ShouldBeUnique();
	}

	#endregion Event ID Range Tests
}
