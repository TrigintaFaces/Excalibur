// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Options.Middleware;

/// <summary>
/// Configuration options for audit logging middleware.
/// </summary>
public sealed class AuditLoggingOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to log message payloads.
	/// </summary>
	/// <value> Default is false for security and performance reasons. </value>
	public bool LogMessagePayload { get; set; }

	/// <summary>
	/// Gets or sets the maximum size of message payload to log (in characters).
	/// </summary>
	/// <value> Default is 10,000 characters. </value>
	[Range(1, int.MaxValue)]
	public int MaxPayloadSize { get; set; } = 10_000;

	/// <summary>
	/// Gets or sets the maximum depth when serializing message payload.
	/// </summary>
	/// <value> Default is 5. </value>
	[Range(1, int.MaxValue)]
	public int MaxPayloadDepth { get; set; } = 5;

	/// <summary>
	/// Gets or sets a function to extract the user ID from the message context.
	/// </summary>
	/// <value>The current <see cref="UserIdExtractor"/> value.</value>
	public Func<IMessageContext, string?>? UserIdExtractor { get; set; }

	/// <summary>
	/// Gets or sets a function to extract the correlation ID from the message context.
	/// </summary>
	/// <value>The current <see cref="CorrelationIdExtractor"/> value.</value>
	public Func<IMessageContext, string?>? CorrelationIdExtractor { get; set; }

	/// <summary>
	/// Gets or sets a function to determine whether to log the payload for a specific message.
	/// </summary>
	/// <remarks> If null, payload logging is controlled by the LogMessagePayload property. </remarks>
	/// <value>The current <see cref="PayloadFilter"/> value.</value>
	public Func<IDispatchMessage, bool>? PayloadFilter { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to include sensitive data in audit logs.
	/// </summary>
	/// <value> Default is false. </value>
	public bool IncludeSensitiveData { get; set; }
}
