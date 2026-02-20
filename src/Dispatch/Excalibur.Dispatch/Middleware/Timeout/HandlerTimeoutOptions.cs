// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Middleware.Timeout;

/// <summary>
/// Configuration options for per-handler timeout middleware.
/// </summary>
/// <remarks>
/// Allows configuring a default timeout and per-handler overrides based on the handler type name.
/// This complements the existing <c>TimeoutOptions</c> which applies to message types.
/// </remarks>
public sealed class HandlerTimeoutOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether per-handler timeout is enabled.
	/// </summary>
	/// <value><see langword="true"/> if enabled; otherwise, <see langword="false"/>. Defaults to <see langword="true"/>.</value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the default timeout applied to all handlers unless overridden.
	/// </summary>
	/// <value>The default timeout. Defaults to 30 seconds.</value>
	[Range(typeof(TimeSpan), "00:00:00.001", "01:00:00")]
	public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets per-handler timeout overrides keyed by handler type name.
	/// </summary>
	/// <value>Dictionary mapping handler type names to timeout durations.</value>
	/// <example>
	/// <code>
	/// options.HandlerTimeouts["SlowImportHandler"] = TimeSpan.FromMinutes(5);
	/// options.HandlerTimeouts["QuickPingHandler"] = TimeSpan.FromSeconds(2);
	/// </code>
	/// </example>
	public Dictionary<string, TimeSpan> HandlerTimeouts { get; init; } = [];

	/// <summary>
	/// Gets or sets a value indicating whether to throw on timeout.
	/// If false, returns a failed result instead.
	/// </summary>
	/// <value><see langword="true"/> to throw; <see langword="false"/> to return a failed result. Defaults to <see langword="true"/>.</value>
	public bool ThrowOnTimeout { get; set; } = true;
}
