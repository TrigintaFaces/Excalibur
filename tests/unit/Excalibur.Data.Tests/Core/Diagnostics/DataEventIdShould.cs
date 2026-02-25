// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Diagnostics;

namespace Excalibur.Data.Tests.Core.Diagnostics;

/// <summary>
/// Unit tests for <see cref="DataEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Data")]
[Trait("Priority", "0")]
public sealed class DataEventIdShould : UnitTestBase
{
	#region Connection String Provider Event ID Tests (110000-110099)

	[Fact]
	public void HaveConnectionStringSetInConnectionStringRange()
	{
		DataEventId.ConnectionStringSet.ShouldBe(110000);
	}

	[Fact]
	public void HaveAllConnectionStringProviderEventIdsInExpectedRange()
	{
		DataEventId.ConnectionStringSet.ShouldBeInRange(110000, 110099);
		DataEventId.ConnectionStringRemoved.ShouldBeInRange(110000, 110099);
		DataEventId.RefreshingConnectionStrings.ShouldBeInRange(110000, 110099);
		DataEventId.ConnectionStringsRefreshed.ShouldBeInRange(110000, 110099);
		DataEventId.ValidationFailed.ShouldBeInRange(110000, 110099);
		DataEventId.ConnectionStringsLoaded.ShouldBeInRange(110000, 110099);
		DataEventId.ResolvedFromEnvironment.ShouldBeInRange(110000, 110099);
		DataEventId.ReferencesSecretStore.ShouldBeInRange(110000, 110099);
		DataEventId.CheckingExternalSources.ShouldBeInRange(110000, 110099);
	}

	#endregion

	#region Persistence Configuration Event ID Tests (110100-110199)

	[Fact]
	public void HaveConfigurationValidatedInPersistenceConfigRange()
	{
		DataEventId.ConfigurationValidated.ShouldBe(110100);
	}

	[Fact]
	public void HaveAllPersistenceConfigEventIdsInExpectedRange()
	{
		DataEventId.ConfigurationValidated.ShouldBeInRange(110100, 110199);
		DataEventId.ProviderTypeConfigured.ShouldBeInRange(110100, 110199);
		DataEventId.ConfigurationValidationError.ShouldBeInRange(110100, 110199);
		DataEventId.ConfigurationValidationWarning.ShouldBeInRange(110100, 110199);
	}

	#endregion

	#region Health Check Event ID Tests (110200-110299)

	[Fact]
	public void HaveHealthCheckStartedInHealthCheckRange()
	{
		DataEventId.HealthCheckStarted.ShouldBe(110200);
	}

	[Fact]
	public void HaveAllHealthCheckEventIdsInExpectedRange()
	{
		DataEventId.HealthCheckStarted.ShouldBeInRange(110200, 110299);
		DataEventId.HealthCheckCompleted.ShouldBeInRange(110200, 110299);
		DataEventId.HealthCheckFailed.ShouldBeInRange(110200, 110299);
		DataEventId.DetailedHealthCheckFailed.ShouldBeInRange(110200, 110299);
	}

	#endregion

	#region Persistence Provider Factory Event ID Tests (110300-110399)

	[Fact]
	public void HaveProviderCreatedInProviderFactoryRange()
	{
		DataEventId.ProviderCreated.ShouldBe(110300);
	}

	[Fact]
	public void HaveAllProviderFactoryEventIdsInExpectedRange()
	{
		DataEventId.ProviderCreated.ShouldBeInRange(110300, 110399);
		DataEventId.ProviderNotFound.ShouldBeInRange(110300, 110399);
		DataEventId.ProviderCreationFailed.ShouldBeInRange(110300, 110399);
		DataEventId.ProviderRegistered.ShouldBeInRange(110300, 110399);
		DataEventId.ProviderUnregistered.ShouldBeInRange(110300, 110399);
	}

	#endregion

	#region Data Reserved Range Tests

	[Fact]
	public void HaveAllEventIdsInDataReservedRange()
	{
		// Data reserved range is 110000-110999
		var allEventIds = GetAllDataEventIds();

		foreach (var eventId in allEventIds)
		{
			eventId.ShouldBeInRange(110000, 110999,
				$"Event ID {eventId} is outside Data reserved range (110000-110999)");
		}
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllDataEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllDataEventIds();
		allEventIds.Length.ShouldBeGreaterThan(20);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllDataEventIds()
	{
		return
		[
			// Connection String Provider (110000-110099)
			DataEventId.ConnectionStringSet,
			DataEventId.ConnectionStringRemoved,
			DataEventId.RefreshingConnectionStrings,
			DataEventId.ConnectionStringsRefreshed,
			DataEventId.ValidationFailed,
			DataEventId.ConnectionStringsLoaded,
			DataEventId.ResolvedFromEnvironment,
			DataEventId.ReferencesSecretStore,
			DataEventId.CheckingExternalSources,

			// Persistence Configuration (110100-110199)
			DataEventId.ConfigurationValidated,
			DataEventId.ProviderTypeConfigured,
			DataEventId.ConfigurationValidationError,
			DataEventId.ConfigurationValidationWarning,

			// Health Check (110200-110299)
			DataEventId.HealthCheckStarted,
			DataEventId.HealthCheckCompleted,
			DataEventId.HealthCheckFailed,
			DataEventId.DetailedHealthCheckFailed,

			// Persistence Provider Factory (110300-110399)
			DataEventId.ProviderCreated,
			DataEventId.ProviderNotFound,
			DataEventId.ProviderCreationFailed,
			DataEventId.ProviderRegistered,
			DataEventId.ProviderUnregistered
		];
	}

	#endregion
}
