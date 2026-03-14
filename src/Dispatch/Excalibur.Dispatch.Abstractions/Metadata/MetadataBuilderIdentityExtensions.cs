// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Claims;

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Extension methods for setting identity and security properties on <see cref="IMessageMetadataBuilder"/>.
/// </summary>
public static class MetadataBuilderIdentityExtensions
{
	/// <summary>
	/// Sets an external identifier from an external system or integration.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="externalId"> The external system identifier. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithExternalId(this IMessageMetadataBuilder builder, string? externalId)
		=> builder.WithProperty(MetadataPropertyKeys.ExternalId, externalId);

	/// <summary>
	/// Sets the user identifier associated with this message.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="userId"> The identifier of the user. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithUserId(this IMessageMetadataBuilder builder, string? userId)
		=> builder.WithProperty(MetadataPropertyKeys.UserId, userId);

	/// <summary>
	/// Sets the tenant identifier for multi-tenant scenarios.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="tenantId"> The identifier of the tenant. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithTenantId(this IMessageMetadataBuilder builder, string? tenantId)
		=> builder.WithProperty(MetadataPropertyKeys.TenantId, tenantId);

	/// <summary>
	/// Sets the W3C trace parent for distributed tracing.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="traceParent"> The W3C trace parent header value. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithTraceParent(this IMessageMetadataBuilder builder, string? traceParent)
		=> builder.WithProperty(MetadataPropertyKeys.TraceParent, traceParent);

	/// <summary>
	/// Sets the W3C trace state for distributed tracing.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="traceState"> The W3C trace state header value. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithTraceState(this IMessageMetadataBuilder builder, string? traceState)
		=> builder.WithProperty(MetadataPropertyKeys.TraceState, traceState);

	/// <summary>
	/// Sets the W3C baggage for distributed tracing context propagation.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="baggage"> The W3C baggage header value. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithBaggage(this IMessageMetadataBuilder builder, string? baggage)
		=> builder.WithProperty(MetadataPropertyKeys.Baggage, baggage);

	/// <summary>
	/// Sets the security roles associated with the message.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="roles"> The collection of role identifiers. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithRoles(this IMessageMetadataBuilder builder, IEnumerable<string>? roles)
		=> builder.WithProperty(MetadataPropertyKeys.Roles, roles);

	/// <summary>
	/// Sets the security claims associated with the message.
	/// </summary>
	/// <param name="builder"> The builder instance. </param>
	/// <param name="claims"> The collection of security claims. </param>
	/// <returns> The builder instance for method chaining. </returns>
	public static IMessageMetadataBuilder WithClaims(this IMessageMetadataBuilder builder, IEnumerable<Claim>? claims)
		=> builder.WithProperty(MetadataPropertyKeys.Claims, claims);
}
