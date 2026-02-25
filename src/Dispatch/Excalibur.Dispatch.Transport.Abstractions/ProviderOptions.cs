// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Base configuration options for cloud providers.
/// </summary>
public class ProviderOptions
{
	/// <summary>
	/// Gets or sets the cloud provider type.
	/// </summary>
	/// <value>The current <see cref="Provider"/> value.</value>
	public CloudProviderType Provider { get; set; }

	/// <summary>
	/// Gets or sets the region or location for the cloud provider.
	/// </summary>
	/// <value>The current <see cref="Region"/> value.</value>
	public string Region { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the connection string or endpoint.
	/// </summary>
	/// <value>The current <see cref="ConnectionString"/> value.</value>
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the default timeout for operations in milliseconds.
	/// </summary>
	/// <value>The current <see cref="DefaultTimeoutMs"/> value.</value>
	[Range(1, int.MaxValue)]
	public int DefaultTimeoutMs { get; set; } = 30000;

	/// <summary>
	/// Gets or sets a value indicating whether to enable detailed logging.
	/// </summary>
	/// <value>The current <see cref="EnableDetailedLogging"/> value.</value>
	public bool EnableDetailedLogging { get; set; }

	/// <summary>
	/// Gets or sets the retry policy configuration.
	/// </summary>
	/// <value>
	/// The retry policy configuration.
	/// </value>
	public RetryPolicyOptions RetryPolicy { get; set; } = new();

	/// <summary>
	/// Gets custom metadata for the provider.
	/// </summary>
	/// <value>The current <see cref="Metadata"/> value.</value>
	public Dictionary<string, string> Metadata { get; init; } = [];
}
