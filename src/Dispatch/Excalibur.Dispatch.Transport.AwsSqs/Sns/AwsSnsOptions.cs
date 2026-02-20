// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Configuration options for AWS SNS.
/// </summary>
public sealed class AwsSnsOptions : AwsProviderOptions
{
	/// <summary>
	/// Gets or sets the default topic ARN.
	/// </summary>
	/// <value>
	/// The default topic ARN.
	/// </value>
	public string TopicArn { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether enables encryption when sending messages.
	/// </summary>
	/// <value>
	/// A value indicating whether enables encryption when sending messages.
	/// </value>
	public new bool EnableEncryption { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable message deduplication.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable message deduplication.
	/// </value>
	public new bool EnableDeduplication { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable content-based deduplication.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable content-based deduplication.
	/// </value>
	public bool ContentBasedDeduplication { get; set; }

	/// <summary>
	/// Gets the message attributes to include.
	/// </summary>
	/// <value>
	/// The message attributes to include.
	/// </value>
	public Dictionary<string, string> DefaultAttributes { get; } = [];

	/// <summary>
	/// Gets or sets a value indicating whether to enable raw message delivery.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable raw message delivery.
	/// </value>
	public bool RawMessageDelivery { get; set; }

	/// <summary>
	/// Gets or sets the display name for the topic.
	/// </summary>
	/// <value>
	/// The display name for the topic.
	/// </value>
	public string DisplayName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the KMS master key ID for encryption.
	/// </summary>
	/// <value>
	/// The KMS master key ID for encryption.
	/// </value>
	public string? KmsMasterKeyId { get; set; }

	/// <summary>
	/// Gets or sets the service URL for the SNS endpoint.
	/// </summary>
	/// <value>
	/// The service URL for the SNS endpoint.
	/// </value>
	public new Uri? ServiceUrl { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of error retries.
	/// </summary>
	/// <value>
	/// The maximum number of error retries.
	/// </value>
	public int MaxErrorRetry { get; set; } = 3;

	/// <summary>
	/// Gets or sets the request timeout.
	/// </summary>
	/// <value>
	/// The request timeout.
	/// </value>
	public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the read/write timeout.
	/// </summary>
	/// <value>
	/// The read/write timeout.
	/// </value>
	public TimeSpan ReadWriteTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets a value indicating whether to use HTTP instead of HTTPS.
	/// </summary>
	/// <value>
	/// A value indicating whether to use HTTP instead of HTTPS.
	/// </value>
	public bool UseHttp { get; set; }

	/// <summary>
	/// Gets or sets the AWS region endpoint.
	/// </summary>
	/// <value>
	/// The AWS region endpoint.
	/// </value>
	public string? RegionEndpoint { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether gets or sets whether to use LocalStack for testing.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether to use LocalStack for testing.
	/// </value>
	public new bool UseLocalStack { get; set; }

	/// <summary>
	/// Gets or sets the AWS access key.
	/// </summary>
	/// <value>
	/// The AWS access key.
	/// </value>
	public string? AccessKey { get; set; }

	/// <summary>
	/// Gets or sets the AWS secret key.
	/// </summary>
	/// <value>
	/// The AWS secret key.
	/// </value>
	public string? SecretKey { get; set; }
}
