// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance.Azure;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Azure.Diagnostics;

/// <summary>
/// Unit tests for <see cref="AzureKeyVaultEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance.Azure")]
[Trait("Priority", "0")]
public sealed class AzureKeyVaultEventIdShould : UnitTestBase
{
	#region Azure Key Vault Event ID Tests (92610-92629)

	[Fact]
	public void HaveProviderInitializedInExpectedRange()
	{
		AzureKeyVaultEventId.ProviderInitialized.ShouldBe(92610);
	}

	[Fact]
	public void HaveKeyNotFoundInExpectedRange()
	{
		AzureKeyVaultEventId.KeyNotFound.ShouldBe(92611);
	}

	[Fact]
	public void HaveKeyVersionNotFoundInExpectedRange()
	{
		AzureKeyVaultEventId.KeyVersionNotFound.ShouldBe(92612);
	}

	[Fact]
	public void HaveKeyRotatedInExpectedRange()
	{
		AzureKeyVaultEventId.KeyRotated.ShouldBe(92613);
	}

	[Fact]
	public void HaveKeyCreatedInExpectedRange()
	{
		AzureKeyVaultEventId.KeyCreated.ShouldBe(92614);
	}

	[Fact]
	public void HaveKeyRotationFailedInExpectedRange()
	{
		AzureKeyVaultEventId.KeyRotationFailed.ShouldBe(92615);
	}

	[Fact]
	public void HaveKeyRotationUnexpectedErrorInExpectedRange()
	{
		AzureKeyVaultEventId.KeyRotationUnexpectedError.ShouldBe(92616);
	}

	[Fact]
	public void HaveKeyScheduledForDeletionInExpectedRange()
	{
		AzureKeyVaultEventId.KeyScheduledForDeletion.ShouldBe(92617);
	}

	[Fact]
	public void HaveKeyNotFoundForDeletionInExpectedRange()
	{
		AzureKeyVaultEventId.KeyNotFoundForDeletion.ShouldBe(92618);
	}

	[Fact]
	public void HaveKeySuspendedInExpectedRange()
	{
		AzureKeyVaultEventId.KeySuspended.ShouldBe(92619);
	}

	[Fact]
	public void HaveKeyNotFoundForSuspensionInExpectedRange()
	{
		AzureKeyVaultEventId.KeyNotFoundForSuspension.ShouldBe(92620);
	}

	[Fact]
	public void HaveProviderDisposedInExpectedRange()
	{
		AzureKeyVaultEventId.ProviderDisposed.ShouldBe(92621);
	}

	[Fact]
	public void HaveStandardTierWarningInExpectedRange()
	{
		AzureKeyVaultEventId.StandardTierWarning.ShouldBe(92622);
	}

	[Fact]
	public void HaveAllEventIdsInExpectedRange()
	{
		AzureKeyVaultEventId.ProviderInitialized.ShouldBeInRange(92610, 92629);
		AzureKeyVaultEventId.KeyNotFound.ShouldBeInRange(92610, 92629);
		AzureKeyVaultEventId.KeyVersionNotFound.ShouldBeInRange(92610, 92629);
		AzureKeyVaultEventId.KeyRotated.ShouldBeInRange(92610, 92629);
		AzureKeyVaultEventId.KeyCreated.ShouldBeInRange(92610, 92629);
		AzureKeyVaultEventId.KeyRotationFailed.ShouldBeInRange(92610, 92629);
		AzureKeyVaultEventId.KeyRotationUnexpectedError.ShouldBeInRange(92610, 92629);
		AzureKeyVaultEventId.KeyScheduledForDeletion.ShouldBeInRange(92610, 92629);
		AzureKeyVaultEventId.KeyNotFoundForDeletion.ShouldBeInRange(92610, 92629);
		AzureKeyVaultEventId.KeySuspended.ShouldBeInRange(92610, 92629);
		AzureKeyVaultEventId.KeyNotFoundForSuspension.ShouldBeInRange(92610, 92629);
		AzureKeyVaultEventId.ProviderDisposed.ShouldBeInRange(92610, 92629);
		AzureKeyVaultEventId.StandardTierWarning.ShouldBeInRange(92610, 92629);
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllAzureKeyVaultEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllAzureKeyVaultEventIds();
		allEventIds.Length.ShouldBeGreaterThan(10);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllAzureKeyVaultEventIds()
	{
		return
		[
			AzureKeyVaultEventId.ProviderInitialized,
			AzureKeyVaultEventId.KeyNotFound,
			AzureKeyVaultEventId.KeyVersionNotFound,
			AzureKeyVaultEventId.KeyRotated,
			AzureKeyVaultEventId.KeyCreated,
			AzureKeyVaultEventId.KeyRotationFailed,
			AzureKeyVaultEventId.KeyRotationUnexpectedError,
			AzureKeyVaultEventId.KeyScheduledForDeletion,
			AzureKeyVaultEventId.KeyNotFoundForDeletion,
			AzureKeyVaultEventId.KeySuspended,
			AzureKeyVaultEventId.KeyNotFoundForSuspension,
			AzureKeyVaultEventId.ProviderDisposed,
			AzureKeyVaultEventId.StandardTierWarning
		];
	}

	#endregion
}
