// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Aws;

/// <summary>
/// Event IDs for AWS security components (70920-70939).
/// </summary>
/// <remarks>
/// These event IDs are in the Cloud Credential Stores range defined in Excalibur.Dispatch.Security.
/// </remarks>
public static class AwsSecurityEventId
{
	/// <summary>AWS Secrets Manager credential store created.</summary>
	public const int AwsSecretsManagerCredentialStoreCreated = 70901;

	/// <summary>Retrieving credential from AWS Secrets Manager.</summary>
	public const int AwsSecretsManagerRetrieving = 70920;

	/// <summary>Secret not found in AWS Secrets Manager.</summary>
	public const int AwsSecretsManagerSecretNotFound = 70921;

	/// <summary>Credential retrieved from AWS Secrets Manager.</summary>
	public const int AwsSecretsManagerRetrieved = 70922;

	/// <summary>Request failed for AWS Secrets Manager.</summary>
	public const int AwsSecretsManagerRequestFailed = 70923;

	/// <summary>Failed to retrieve from AWS Secrets Manager.</summary>
	public const int AwsSecretsManagerRetrieveFailed = 70924;

	/// <summary>Storing credential in AWS Secrets Manager.</summary>
	public const int AwsSecretsManagerStoring = 70925;

	/// <summary>Credential stored in AWS Secrets Manager.</summary>
	public const int AwsSecretsManagerStored = 70926;
}
