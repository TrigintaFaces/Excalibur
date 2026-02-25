// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Options;

/// <summary>
/// Options for configuring transport bindings.
/// </summary>
public sealed class TransportBindingOptions
{
	/// <summary>
	/// Gets or sets the priority of the binding (higher values processed first).
	/// </summary>
	/// <value> The relative ordering priority applied during binding selection. </value>
	public int Priority { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the binding is enabled.
	/// </summary>
	/// <value> <see langword="true" /> when the binding is active; otherwise, <see langword="false" />. </value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets custom properties for the binding.
	/// </summary>
	/// <value> The dictionary of custom properties applied to the binding. </value>
	public IDictionary<string, object> Properties { get; init; } = new Dictionary<string, object>(StringComparer.Ordinal);

	/// <summary>
	/// Gets or sets the maximum concurrent messages for this binding.
	/// </summary>
	/// <value> The maximum degree of parallelism allowed for the binding. </value>
	public int MaxConcurrency { get; set; } = Environment.ProcessorCount;

	/// <summary>
	/// Gets or sets the timeout for message processing.
	/// </summary>
	/// <value> The timeout budget for processing a message. </value>
	public TimeSpan ProcessingTimeout { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets a value indicating whether to use the dead-letter queue for failed messages.
	/// </summary>
	/// <value> <see langword="true" /> to reroute failures to the DLQ; otherwise, <see langword="false" />. </value>
	public bool UseDeadLetterQueue { get; set; } = true;
}
