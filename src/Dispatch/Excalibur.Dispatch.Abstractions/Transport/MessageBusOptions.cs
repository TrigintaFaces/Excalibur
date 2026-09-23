// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Base class for message bus configuration options, providing common settings for message bus instances.
/// </summary>
public abstract class MessageBusOptions
{
	/// <summary>
	/// Gets the name of the message bus instance.
	/// </summary>
	/// <value> The logical name of the bus configuration. </value>
	[Required]
	public string Name { get; init; } = string.Empty;
	/// <summary>
	/// Gets a value indicating whether messages sent through this bus should be retried.
	/// </summary>
	/// <value> <see langword="true" /> when retry should be attempted; otherwise, <see langword="false" />. </value>
	/// <remarks>
	/// This switch decides only whether the bus retries at all. How it retries — the attempt count, the
	/// backoff strategy, the delay and any jitter — is configured once for the pipeline through the
	/// resilience options, so that retry behaviour has a single source of truth rather than a per-bus
	/// copy that would have to win or lose against it.
	/// </remarks>
	public bool EnableRetries { get; init; }

	/// <summary>
	/// Gets the optional URI for remote dispatch (used in forwarding or remote buses).
	/// </summary>
	/// <value> The remote target URI for dispatch. </value>
	public Uri? TargetUri { get; init; }

	/// <summary>
	/// Gets a value indicating whether tracing is enabled for this bus with OpenTelemetry spans.
	/// </summary>
	/// <value> <see langword="true" /> to enable telemetry emission; otherwise, <see langword="false" />. </value>
	public bool EnableTelemetry { get; init; } = true;
}
