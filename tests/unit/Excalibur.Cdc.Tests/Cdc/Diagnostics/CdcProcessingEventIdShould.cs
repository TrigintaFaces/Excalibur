// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.Diagnostics;

namespace Excalibur.Tests.Cdc.Diagnostics;

/// <summary>
/// Unit tests for <see cref="CdcProcessingEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Cdc")]
[Trait("Priority", "0")]
public sealed class CdcProcessingEventIdShould : UnitTestBase
{
	#region Event ID Value Tests

	[Fact]
	public void HaveCdcBackgroundServiceDisabledValue()
	{
		CdcProcessingEventId.CdcBackgroundServiceDisabled.ShouldBe(3100);
	}

	[Fact]
	public void HaveCdcBackgroundServiceStartingValue()
	{
		CdcProcessingEventId.CdcBackgroundServiceStarting.ShouldBe(3101);
	}

	[Fact]
	public void HaveCdcBackgroundServiceErrorValue()
	{
		CdcProcessingEventId.CdcBackgroundServiceError.ShouldBe(3102);
	}

	[Fact]
	public void HaveCdcBackgroundServiceStoppedValue()
	{
		CdcProcessingEventId.CdcBackgroundServiceStopped.ShouldBe(3103);
	}

	[Fact]
	public void HaveCdcBackgroundServiceProcessedChangesValue()
	{
		CdcProcessingEventId.CdcBackgroundServiceProcessedChanges.ShouldBe(3104);
	}

	[Fact]
	public void HaveCdcBackgroundServiceDrainTimeoutValue()
	{
		CdcProcessingEventId.CdcBackgroundServiceDrainTimeout.ShouldBe(3105);
	}

	#endregion

	#region Event ID Range Validation Tests

	[Fact]
	public void HaveAllEventIdsInExpectedRange()
	{
		// CDC processing IDs are in range 3100-3149
		CdcProcessingEventId.CdcBackgroundServiceDisabled.ShouldBeInRange(3100, 3149);
		CdcProcessingEventId.CdcBackgroundServiceStarting.ShouldBeInRange(3100, 3149);
		CdcProcessingEventId.CdcBackgroundServiceError.ShouldBeInRange(3100, 3149);
		CdcProcessingEventId.CdcBackgroundServiceStopped.ShouldBeInRange(3100, 3149);
		CdcProcessingEventId.CdcBackgroundServiceProcessedChanges.ShouldBeInRange(3100, 3149);
		CdcProcessingEventId.CdcBackgroundServiceDrainTimeout.ShouldBeInRange(3100, 3149);
	}

	[Fact]
	public void HaveAllEventIdsInExcaliburReservedRange()
	{
		// Excalibur reserved range is 3000-4999
		var allEventIds = new[]
		{
			CdcProcessingEventId.CdcBackgroundServiceDisabled,
			CdcProcessingEventId.CdcBackgroundServiceStarting,
			CdcProcessingEventId.CdcBackgroundServiceError,
			CdcProcessingEventId.CdcBackgroundServiceStopped,
			CdcProcessingEventId.CdcBackgroundServiceProcessedChanges,
			CdcProcessingEventId.CdcBackgroundServiceDrainTimeout
		};

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(3000, 4999,
				$"Event ID {eventId} is outside Excalibur reserved range (3000-4999)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = new[]
		{
			CdcProcessingEventId.CdcBackgroundServiceDisabled,
			CdcProcessingEventId.CdcBackgroundServiceStarting,
			CdcProcessingEventId.CdcBackgroundServiceError,
			CdcProcessingEventId.CdcBackgroundServiceStopped,
			CdcProcessingEventId.CdcBackgroundServiceProcessedChanges,
			CdcProcessingEventId.CdcBackgroundServiceDrainTimeout
		};

		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		// Verify all event IDs are accounted for
		var allEventIds = new[]
		{
			CdcProcessingEventId.CdcBackgroundServiceDisabled,
			CdcProcessingEventId.CdcBackgroundServiceStarting,
			CdcProcessingEventId.CdcBackgroundServiceError,
			CdcProcessingEventId.CdcBackgroundServiceStopped,
			CdcProcessingEventId.CdcBackgroundServiceProcessedChanges,
			CdcProcessingEventId.CdcBackgroundServiceDrainTimeout
		};

		allEventIds.Length.ShouldBe(6);
	}

	#endregion

	#region Sequential ID Tests

	[Fact]
	public void HaveSequentialEventIds()
	{
		// Event IDs should be sequential starting from 3100
		CdcProcessingEventId.CdcBackgroundServiceDisabled.ShouldBe(3100);
		CdcProcessingEventId.CdcBackgroundServiceStarting.ShouldBe(3101);
		CdcProcessingEventId.CdcBackgroundServiceError.ShouldBe(3102);
		CdcProcessingEventId.CdcBackgroundServiceStopped.ShouldBe(3103);
		CdcProcessingEventId.CdcBackgroundServiceProcessedChanges.ShouldBe(3104);
		CdcProcessingEventId.CdcBackgroundServiceDrainTimeout.ShouldBe(3105);
	}

	#endregion
}
