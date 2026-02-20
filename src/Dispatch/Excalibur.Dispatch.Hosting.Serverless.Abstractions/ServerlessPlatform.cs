// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Hosting.Serverless;

/// <summary>
/// Represents the serverless platform type.
/// </summary>
public enum ServerlessPlatform
{
	/// <summary>
	/// Unknown or local development.
	/// </summary>
	Unknown = 0,

	/// <summary>
	/// AWS Lambda.
	/// </summary>
	AwsLambda = 1,

	/// <summary>
	/// Azure Functions.
	/// </summary>
	AzureFunctions = 2,

	/// <summary>
	/// Google Cloud Functions.
	/// </summary>
	GoogleCloudFunctions = 3,
}
