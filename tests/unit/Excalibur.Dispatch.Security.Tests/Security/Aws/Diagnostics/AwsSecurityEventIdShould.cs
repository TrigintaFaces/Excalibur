// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security.Aws;

namespace Excalibur.Dispatch.Security.Tests.Security.Aws.Diagnostics;

/// <summary>
/// Unit tests for <see cref="AwsSecurityEventId"/>.
/// Verifies event ID values, uniqueness, and range compliance.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Security.Aws")]
[Trait("Priority", "0")]
public sealed class AwsSecurityEventIdShould : UnitTestBase
{
	#region AWS Secrets Manager Event ID Tests (70920-70939)

	[Fact]
	public void HaveAwsSecretsManagerCredentialStoreCreatedInExpectedRange()
	{
		AwsSecurityEventId.AwsSecretsManagerCredentialStoreCreated.ShouldBe(70901);
	}

	[Fact]
	public void HaveAwsSecretsManagerRetrievingInExpectedRange()
	{
		AwsSecurityEventId.AwsSecretsManagerRetrieving.ShouldBe(70920);
	}

	[Fact]
	public void HaveAwsSecretsManagerSecretNotFoundInExpectedRange()
	{
		AwsSecurityEventId.AwsSecretsManagerSecretNotFound.ShouldBe(70921);
	}

	[Fact]
	public void HaveAwsSecretsManagerRetrievedInExpectedRange()
	{
		AwsSecurityEventId.AwsSecretsManagerRetrieved.ShouldBe(70922);
	}

	[Fact]
	public void HaveAwsSecretsManagerRequestFailedInExpectedRange()
	{
		AwsSecurityEventId.AwsSecretsManagerRequestFailed.ShouldBe(70923);
	}

	[Fact]
	public void HaveAwsSecretsManagerRetrieveFailedInExpectedRange()
	{
		AwsSecurityEventId.AwsSecretsManagerRetrieveFailed.ShouldBe(70924);
	}

	[Fact]
	public void HaveAwsSecretsManagerStoringInExpectedRange()
	{
		AwsSecurityEventId.AwsSecretsManagerStoring.ShouldBe(70925);
	}

	[Fact]
	public void HaveAwsSecretsManagerStoredInExpectedRange()
	{
		AwsSecurityEventId.AwsSecretsManagerStored.ShouldBe(70926);
	}

	[Fact]
	public void HaveAllEventIdsInCloudCredentialStoresRange()
	{
		// AWS Security event IDs are in the Cloud Credential Stores range (70900-70999)
		AwsSecurityEventId.AwsSecretsManagerCredentialStoreCreated.ShouldBeInRange(70900, 70999);
		AwsSecurityEventId.AwsSecretsManagerRetrieving.ShouldBeInRange(70900, 70999);
		AwsSecurityEventId.AwsSecretsManagerSecretNotFound.ShouldBeInRange(70900, 70999);
		AwsSecurityEventId.AwsSecretsManagerRetrieved.ShouldBeInRange(70900, 70999);
		AwsSecurityEventId.AwsSecretsManagerRequestFailed.ShouldBeInRange(70900, 70999);
		AwsSecurityEventId.AwsSecretsManagerRetrieveFailed.ShouldBeInRange(70900, 70999);
		AwsSecurityEventId.AwsSecretsManagerStoring.ShouldBeInRange(70900, 70999);
		AwsSecurityEventId.AwsSecretsManagerStored.ShouldBeInRange(70900, 70999);
	}

	#endregion

	#region Uniqueness Tests

	[Fact]
	public void HaveUniqueEventIds()
	{
		var allEventIds = GetAllAwsSecurityEventIds();
		allEventIds.ShouldBeUnique();
	}

	[Fact]
	public void HaveCorrectNumberOfEventIds()
	{
		var allEventIds = GetAllAwsSecurityEventIds();
		allEventIds.Length.ShouldBeGreaterThan(5);
	}

	#endregion

	#region Helper Methods

	private static int[] GetAllAwsSecurityEventIds()
	{
		return
		[
			AwsSecurityEventId.AwsSecretsManagerCredentialStoreCreated,
			AwsSecurityEventId.AwsSecretsManagerRetrieving,
			AwsSecurityEventId.AwsSecretsManagerSecretNotFound,
			AwsSecurityEventId.AwsSecretsManagerRetrieved,
			AwsSecurityEventId.AwsSecretsManagerRequestFailed,
			AwsSecurityEventId.AwsSecretsManagerRetrieveFailed,
			AwsSecurityEventId.AwsSecretsManagerStoring,
			AwsSecurityEventId.AwsSecretsManagerStored
		];
	}

	#endregion
}
