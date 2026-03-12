// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Claims;

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Extension methods for accessing identity and security metadata from <see cref="IMessageMetadata.Properties"/>.
/// </summary>
/// <remarks>
/// These properties were moved from the <see cref="IMessageMetadata"/> interface to the Properties dictionary
/// to keep the core interface minimal. Use these typed extension methods for convenient, type-safe access.
/// </remarks>
public static class MetadataIdentityExtensions
{
	/// <summary>
	/// Gets the external identifier for correlation with external systems.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The external identifier, or null if not set. </returns>
	public static string? GetExternalId(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.ExternalId, out var value) ? value as string : null;

	/// <summary>
	/// Gets the W3C trace context header for distributed tracing integration.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The trace parent header, or null if not set. </returns>
	public static string? GetTraceParent(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.TraceParent, out var value) ? value as string : null;

	/// <summary>
	/// Gets the W3C trace state header for vendor-specific tracing data.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The trace state header, or null if not set. </returns>
	public static string? GetTraceState(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.TraceState, out var value) ? value as string : null;

	/// <summary>
	/// Gets the baggage header for context propagation.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The baggage header, or null if not set. </returns>
	public static string? GetBaggage(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.Baggage, out var value) ? value as string : null;

	/// <summary>
	/// Gets the identifier of the user who initiated this message.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The user identifier, or null if not set. </returns>
	public static string? GetUserId(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.UserId, out var value) ? value as string : null;

	/// <summary>
	/// Gets the roles associated with the user context.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The collection of roles, or an empty collection if not set. </returns>
	public static IReadOnlyCollection<string> GetRoles(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.Roles, out var value) && value is IReadOnlyCollection<string> roles
			? roles
			: Array.Empty<string>();

	/// <summary>
	/// Gets the security claims associated with the message context.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The collection of claims, or an empty collection if not set. </returns>
	public static IReadOnlyCollection<Claim> GetClaims(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.Claims, out var value) && value is IReadOnlyCollection<Claim> claims
			? claims
			: Array.Empty<Claim>();

	/// <summary>
	/// Gets the tenant identifier for multi-tenant message routing and isolation.
	/// </summary>
	/// <param name="metadata"> The message metadata. </param>
	/// <returns> The tenant identifier, or null if not set. </returns>
	public static string? GetTenantId(this IMessageMetadata metadata)
		=> metadata.Properties.TryGetValue(MetadataPropertyKeys.TenantId, out var value) ? value as string : null;
}
