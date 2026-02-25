// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Context information available during message validation.
/// </summary>
/// <remarks> Creates a new message validation context. </remarks>
public sealed class MessageValidationContext(
	IDispatchMessage message,
	IMessageContext context,
	string? tenantId = null,
	string? userId = null,
	string? correlationId = null)
{
	/// <summary>
	/// Gets the message being validated.
	/// </summary>
	/// <value>
	/// The message being validated.
	/// </value>
	public IDispatchMessage Message { get; } = message ?? throw new ArgumentNullException(nameof(message));

	/// <summary>
	/// Gets the message context.
	/// </summary>
	/// <value>
	/// The message context.
	/// </value>
	public IMessageContext Context { get; } = context ?? throw new ArgumentNullException(nameof(context));

	/// <summary>
	/// Gets the tenant identifier, if available.
	/// </summary>
	/// <value>The current <see cref="TenantId"/> value.</value>
	public string? TenantId { get; } = tenantId;

	/// <summary>
	/// Gets the user identifier, if available.
	/// </summary>
	/// <value>The current <see cref="UserId"/> value.</value>
	public string? UserId { get; } = userId;

	/// <summary>
	/// Gets the correlation identifier, if available.
	/// </summary>
	/// <value>The current <see cref="CorrelationId"/> value.</value>
	public string? CorrelationId { get; } = correlationId;
}
