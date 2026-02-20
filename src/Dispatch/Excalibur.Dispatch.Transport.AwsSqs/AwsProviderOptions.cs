// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Amazon.Runtime;


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Configuration options for AWS messaging provider.
/// </summary>
public class AwsProviderOptions : ProviderOptions
{
	/// <summary>
	/// Gets or sets the AWS region.
	/// </summary>
	/// <value>
	/// The AWS region.
	/// </value>
	[Required]
	public new string Region { get; set; } = "us-east-1";

	/// <summary>
	/// Gets or sets the AWS credentials.
	/// </summary>
	/// <value>
	/// The AWS credentials.
	/// </value>
	public AWSCredentials? Credentials { get; set; }

	/// <summary>
	/// Gets or sets the service URL (for custom endpoints).
	/// </summary>
	/// <value>
	/// The service URL (for custom endpoints).
	/// </value>
	public Uri? ServiceUrl { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to use LocalStack for local development.
	/// </summary>
	/// <value>
	/// A value indicating whether to use LocalStack for local development.
	/// </value>
	public bool UseLocalStack { get; set; }

	/// <summary>
	/// Gets or sets the LocalStack URL.
	/// </summary>
	/// <value>
	/// The LocalStack URL.
	/// </value>
	public Uri? LocalStackUrl { get; set; } = new("http://localhost:4566");

	/// <summary>
	/// Gets or sets the maximum number of retries.
	/// </summary>
	/// <value>
	/// The maximum number of retries.
	/// </value>
	[Range(0, 100)]
	public int MaxRetries { get; set; } = 3;

	/// <summary>
	/// Gets or sets the request timeout.
	/// </summary>
	/// <value>
	/// The request timeout.
	/// </value>
	public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets a value indicating whether to validate connectivity on startup.
	/// </summary>
	/// <value>
	/// A value indicating whether to validate connectivity on startup.
	/// </value>
	public bool ValidateOnStartup { get; set; } = true;

	/// <summary>
	/// Gets or sets the visibility timeout for consumed messages.
	/// </summary>
	/// <value>
	/// The visibility timeout for consumed messages.
	/// </value>
	public TimeSpan VisibilityTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the long polling wait time.
	/// </summary>
	/// <value>
	/// The long polling wait time.
	/// </value>
	public TimeSpan WaitTimeSeconds { get; set; } = TimeSpan.FromSeconds(20);

	/// <summary>
	/// Gets or sets the maximum number of messages to receive in a batch.
	/// </summary>
	/// <value>
	/// The maximum number of messages to receive in a batch.
	/// </value>
	[Range(1, 10)]
	public int MaxNumberOfMessages { get; set; } = 10;

	/// <summary>
	/// Gets or sets a value indicating whether to enable message deduplication.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable message deduplication.
	/// </value>
	public bool EnableDeduplication { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable server-side encryption.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable server-side encryption.
	/// </value>
	public bool EnableEncryption { get; set; }

	/// <summary>
	/// Gets or sets the KMS key ID for encryption.
	/// </summary>
	/// <value>
	/// The KMS key ID for encryption.
	/// </value>
	public string? KmsKeyId { get; set; }
}
