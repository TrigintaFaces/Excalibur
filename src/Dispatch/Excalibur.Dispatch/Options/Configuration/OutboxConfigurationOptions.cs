// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Configuration;

/// <summary>
/// Configuration options for the outbox staging middleware.
/// </summary>
/// <remarks>
/// This type controls only whether outbox staging participates in the dispatch pipeline. Processor
/// behaviour - batch size, polling interval, retry policy, and store selection - belongs to the outbox
/// processor package and is configured there; this package does not reference it and cannot influence it.
/// </remarks>
public sealed class OutboxConfigurationOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to enable the outbox pattern.
	/// </summary>
	/// <value>Default is <see langword="false"/> (disabled). Call <c>AddOutbox&lt;T&gt;()</c> to enable with a registered <c>IOutboxStore</c>.</value>
	public bool Enabled { get; set; }
}
