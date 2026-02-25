// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Options.Configuration;

/// <summary>
/// Nested caching options for <see cref="DispatchOptions"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is a lightweight configuration-binding class for <c>appsettings.json</c> scenarios.
/// For full caching configuration including memory/distributed modes, use
/// <c>AddCaching()</c> on <c>IDispatchBuilder</c>.
/// </para>
/// </remarks>
public sealed class CachingOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether caching is enabled.
	/// </summary>
	/// <value><see langword="false"/> by default.</value>
	public bool Enabled { get; set; }

	/// <summary>
	/// Gets or sets the default expiration for cached items.
	/// </summary>
	/// <value>5 minutes by default.</value>
	public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(5);
}
