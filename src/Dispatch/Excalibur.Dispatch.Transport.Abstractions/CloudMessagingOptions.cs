// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Options for cloud messaging configuration.
/// </summary>
public sealed class CloudMessagingOptions
{
	/// <summary>
	/// Gets or sets the default provider name.
	/// </summary>
	/// <value>The current <see cref="DefaultProvider"/> value.</value>
	public string? DefaultProvider { get; set; }

	/// <summary>
	/// Gets the provider-specific configurations.
	/// </summary>
	/// <value>
	/// The provider-specific configurations.
	/// </value>
	public Dictionary<string, ProviderOptions> Providers { get; init; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Gets or sets a value indicating whether to enable distributed tracing.
	/// </summary>
	/// <value>The current <see cref="EnableTracing"/> value.</value>
	public bool EnableTracing { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable metrics collection.
	/// </summary>
	/// <value>The current <see cref="EnableMetrics"/> value.</value>
	public bool EnableMetrics { get; set; } = true;

	/// <summary>
	/// Gets or sets the global timeout for operations.
	/// </summary>
	/// <value>
	/// The global timeout for operations.
	/// </value>
	public TimeSpan GlobalTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
