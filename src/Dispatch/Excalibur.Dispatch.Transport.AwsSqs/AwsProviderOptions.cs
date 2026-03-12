// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Configuration options for AWS messaging provider.
/// </summary>
/// <remarks>
/// <para>
/// Connection/credential properties are in <see cref="Connection"/> and consumer behavior properties are in <see cref="Consumer"/>.
/// This follows the <c>AmazonSQSConfig</c> pattern of separating client configuration from consumer behavior.
/// </para>
/// </remarks>
public class AwsProviderOptions : ProviderOptions
{
	/// <summary>
	/// Gets or sets the AWS region.
	/// </summary>
	/// <value> The AWS region. </value>
	[Required]
	public new string Region { get; set; } = "us-east-1";

	/// <summary>
	/// Gets or sets the maximum number of retries.
	/// </summary>
	/// <value> The maximum number of retries. </value>
	[Range(0, 100)]
	public int MaxRetries { get; set; } = 3;

	/// <summary>
	/// Gets or sets the request timeout.
	/// </summary>
	/// <value> The request timeout. </value>
	public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets a value indicating whether to enable message deduplication.
	/// </summary>
	/// <value> A value indicating whether to enable message deduplication. </value>
	public bool EnableDeduplication { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable server-side encryption.
	/// </summary>
	/// <value> A value indicating whether to enable server-side encryption. </value>
	public bool EnableEncryption { get; set; }

	/// <summary>
	/// Gets or sets the KMS key ID for encryption.
	/// </summary>
	/// <value> The KMS key ID for encryption. </value>
	public string? KmsKeyId { get; set; }

	/// <summary>
	/// Gets or sets the connection and credential options.
	/// </summary>
	/// <value> The AWS SQS connection options. </value>
	public AwsSqsConnectionOptions Connection { get; set; } = new();

	/// <summary>
	/// Gets or sets the consumer/receiver options.
	/// </summary>
	/// <value> The AWS SQS consumer options. </value>
	public AwsSqsConsumerOptions Consumer { get; set; } = new();
}
