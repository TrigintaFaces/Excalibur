// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security.Azure;

namespace Excalibur.Dispatch.Security.Tests.Security.Azure.Diagnostics;

/// <summary>
/// Unit tests for <see cref="AzureSecurityEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Security.Azure")]
[Trait("Priority", "0")]
public sealed class AzureSecurityEventIdShould : UnitTestBase
{
	#region Azure Key Vault Credential Store Event ID Tests (70900-70919)

	[Fact]
	public void HaveAzureKeyVaultCredentialStoreCreatedInExpectedRange()
	{
		AzureSecurityEventId.AzureKeyVaultCredentialStoreCreated.ShouldBe(70900);
	}

	[Fact]
	public void HaveAzureKeyVaultRetrievingInExpectedRange()
	{
		AzureSecurityEventId.AzureKeyVaultRetrieving.ShouldBe(70906);
	}

	[Fact]
	public void HaveAzureKeyVaultSecretNotFoundInExpectedRange()
	{
		AzureSecurityEventId.AzureKeyVaultSecretNotFound.ShouldBe(70907);
	}

	[Fact]
	public void HaveAzureKeyVaultRetrievedInExpectedRange()
	{
		AzureSecurityEventId.AzureKeyVaultRetrieved.ShouldBe(70908);
	}

	[Fact]
	public void HaveAzureKeyVaultRequestFailedInExpectedRange()
	{
		AzureSecurityEventId.AzureKeyVaultRequestFailed.ShouldBe(70909);
	}

	[Fact]
	public void HaveAzureKeyVaultRetrieveFailedInExpectedRange()
	{
		AzureSecurityEventId.AzureKeyVaultRetrieveFailed.ShouldBe(70910);
	}

	[Fact]
	public void HaveAzureKeyVaultStoringInExpectedRange()
	{
		AzureSecurityEventId.AzureKeyVaultStoring.ShouldBe(70911);
	}

	[Fact]
	public void HaveAzureKeyVaultStoredInExpectedRange()
	{
		AzureSecurityEventId.AzureKeyVaultStored.ShouldBe(70912);
	}

	[Fact]
	public void HaveAllEventIdsInCloudCredentialStoresRange()
	{
		// Azure Security event IDs are in the Cloud Credential Stores range (70900-70919)
		AzureSecurityEventId.AzureKeyVaultCredentialStoreCreated.ShouldBeInRange(70900, 70919);
		AzureSecurityEventId.AzureKeyVaultRetrieving.ShouldBeInRange(70900, 70919);
		AzureSecurityEventId.AzureKeyVaultSecretNotFound.ShouldBeInRange(70900, 70919);
		AzureSecurityEventId.AzureKeyVaultRetrieved.ShouldBeInRange(70900, 70919);
		AzureSecurityEventId.AzureKeyVaultRequestFailed.ShouldBeInRange(70900, 70919);
		AzureSecurityEventId.AzureKeyVaultRetrieveFailed.ShouldBeInRange(70900, 70919);
		AzureSecurityEventId.AzureKeyVaultStoring.ShouldBeInRange(70900, 70919);
		AzureSecurityEventId.AzureKeyVaultStored.ShouldBeInRange(70900, 70919);
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllAzureSecurityEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllAzureSecurityEventIds();
		allEventIds.Length.ShouldBeGreaterThan(5);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllAzureSecurityEventIds()
	{
		return
		[
			AzureSecurityEventId.AzureKeyVaultCredentialStoreCreated,
			AzureSecurityEventId.AzureKeyVaultRetrieving,
			AzureSecurityEventId.AzureKeyVaultSecretNotFound,
			AzureSecurityEventId.AzureKeyVaultRetrieved,
			AzureSecurityEventId.AzureKeyVaultRequestFailed,
			AzureSecurityEventId.AzureKeyVaultRetrieveFailed,
			AzureSecurityEventId.AzureKeyVaultStoring,
			AzureSecurityEventId.AzureKeyVaultStored
		];
	}

	#endregion
}
