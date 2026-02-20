// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.AuditLogging.Datadog;

namespace Excalibur.Dispatch.Security.Tests.AuditLogging.Datadog;

/// <summary>
/// Unit tests for <see cref="DatadogAuditLoggingEventId"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "AuditLogging")]
public sealed class DatadogAuditLoggingEventIdShould
{
	#region Event ID Value Tests

	[Fact]
	public void HaveCorrectEventForwardedValue()
	{
		// Assert
		DatadogAuditLoggingEventId.EventForwarded.ShouldBe(93420);
	}

	[Fact]
	public void HaveCorrectBatchForwardedValue()
	{
		// Assert
		DatadogAuditLoggingEventId.BatchForwarded.ShouldBe(93421);
	}

	[Fact]
	public void HaveCorrectForwardFailedStatusValue()
	{
		// Assert
		DatadogAuditLoggingEventId.ForwardFailedStatus.ShouldBe(93425);
	}

	[Fact]
	public void HaveCorrectForwardRetriedValue()
	{
		// Assert
		DatadogAuditLoggingEventId.ForwardRetried.ShouldBe(93426);
	}

	[Fact]
	public void HaveCorrectHealthCheckFailedValue()
	{
		// Assert
		DatadogAuditLoggingEventId.HealthCheckFailed.ShouldBe(93427);
	}

	[Fact]
	public void HaveCorrectForwardFailedHttpErrorValue()
	{
		// Assert
		DatadogAuditLoggingEventId.ForwardFailedHttpError.ShouldBe(93428);
	}

	[Fact]
	public void HaveCorrectForwardFailedTimeoutValue()
	{
		// Assert
		DatadogAuditLoggingEventId.ForwardFailedTimeout.ShouldBe(93429);
	}

	[Fact]
	public void HaveCorrectForwardFailedBatchChunkValue()
	{
		// Assert
		DatadogAuditLoggingEventId.ForwardFailedBatchChunk.ShouldBe(93430);
	}

	#endregion Event ID Value Tests

	#region Event ID Range Tests

	[Fact]
	public void HaveEventIdsInExpectedRange()
	{
		// Assert - Event IDs should be in the 93420-93439 range
		DatadogAuditLoggingEventId.EventForwarded.ShouldBeInRange(93420, 93439);
		DatadogAuditLoggingEventId.BatchForwarded.ShouldBeInRange(93420, 93439);
		DatadogAuditLoggingEventId.ForwardFailedStatus.ShouldBeInRange(93420, 93439);
		DatadogAuditLoggingEventId.ForwardRetried.ShouldBeInRange(93420, 93439);
		DatadogAuditLoggingEventId.HealthCheckFailed.ShouldBeInRange(93420, 93439);
		DatadogAuditLoggingEventId.ForwardFailedHttpError.ShouldBeInRange(93420, 93439);
		DatadogAuditLoggingEventId.ForwardFailedTimeout.ShouldBeInRange(93420, 93439);
		DatadogAuditLoggingEventId.ForwardFailedBatchChunk.ShouldBeInRange(93420, 93439);
	}

	[Fact]
	public void HaveUniqueEventIds()
	{
		// Arrange
		var eventIds = new[]
		{
			DatadogAuditLoggingEventId.EventForwarded,
			DatadogAuditLoggingEventId.BatchForwarded,
			DatadogAuditLoggingEventId.ForwardFailedStatus,
			DatadogAuditLoggingEventId.ForwardRetried,
			DatadogAuditLoggingEventId.HealthCheckFailed,
			DatadogAuditLoggingEventId.ForwardFailedHttpError,
			DatadogAuditLoggingEventId.ForwardFailedTimeout,
			DatadogAuditLoggingEventId.ForwardFailedBatchChunk
		};

		// Assert
		eventIds.ShouldBeUnique();
	}

	#endregion Event ID Range Tests
}
