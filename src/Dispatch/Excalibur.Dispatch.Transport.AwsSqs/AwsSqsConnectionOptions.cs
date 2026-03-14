// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Amazon.Runtime;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Connection and credential options for the AWS SQS provider.
/// </summary>
/// <remarks>
/// Follows the <c>AmazonSQSConfig</c> client configuration pattern of separating credentials from consumer behavior.
/// </remarks>
public sealed class AwsSqsConnectionOptions
{
	/// <summary>
	/// Gets or sets the AWS credentials.
	/// </summary>
	/// <value> The AWS credentials. </value>
	public AWSCredentials? Credentials { get; set; }

	/// <summary>
	/// Gets or sets the service URL (for custom endpoints).
	/// </summary>
	/// <value> The service URL (for custom endpoints). </value>
	public Uri? ServiceUrl { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to use LocalStack for local development.
	/// </summary>
	/// <value> A value indicating whether to use LocalStack for local development. </value>
	public bool UseLocalStack { get; set; }

	/// <summary>
	/// Gets or sets the LocalStack URL.
	/// </summary>
	/// <value> The LocalStack URL. </value>
	public Uri? LocalStackUrl { get; set; } = new("http://localhost:4566");

	/// <summary>
	/// Gets or sets a value indicating whether to validate connectivity on startup.
	/// </summary>
	/// <value> A value indicating whether to validate connectivity on startup. </value>
	public bool ValidateOnStartup { get; set; } = true;
}
