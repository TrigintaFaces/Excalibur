// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Options.Delivery;

/// <summary>
/// Options for message envelope pool.
/// </summary>
public sealed class MessageEnvelopePoolOptions
{
	/// <summary>
	/// Gets or sets the size of thread-local cache.
	/// </summary>
	/// <value>The current <see cref="ThreadLocalCacheSize"/> value.</value>
	[Range(1, int.MaxValue)]
	public int ThreadLocalCacheSize { get; set; } = 16;

	/// <summary>
	/// Gets or sets a value indicating whether to enable telemetry.
	/// </summary>
	/// <value>The current <see cref="EnableTelemetry"/> value.</value>
	public bool EnableTelemetry { get; set; }
}
