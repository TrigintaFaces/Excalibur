// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Configuration options for AWS message bus services.
/// </summary>
public sealed class AwsMessageBusOptions
{
	/// <summary>
	/// Gets or sets aWS service URL. Used for LocalStack or custom endpoints.
	/// </summary>
	/// <value>
	/// AWS service URL. Used for LocalStack or custom endpoints.
	/// </value>
	public Uri? ServiceUrl { get; set; }

	/// <summary>
	/// Gets or sets aWS region name.
	/// </summary>
	/// <value>
	/// AWS region name.
	/// </value>
	public string Region { get; set; } = "us-east-1";

	/// <summary>
	/// Gets or sets a value indicating whether to use LocalStack for testing.
	/// </summary>
	/// <value>
	/// A value indicating whether to use LocalStack for testing.
	/// </value>
	public bool UseLocalStack { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable SQS service.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable SQS service.
	/// </value>
	public bool EnableSqs { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable SNS service.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable SNS service.
	/// </value>
	public bool EnableSns { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable EventBridge service.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable EventBridge service.
	/// </value>
	public bool EnableEventBridge { get; set; } = true;
}
