// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

using Excalibur.Domain;

using Microsoft.Extensions.Configuration;

namespace Excalibur.A3;

/// <summary>
/// Extension methods for <see cref="IActivityContext" /> to provide convenient access to common context values.
/// </summary>
public static class ActivityContextExtensions
{
	/// <summary>
	/// Retrieves the <see cref="IAccessToken" /> from the activity context.
	/// </summary>
	/// <param name="context">The activity context.</param>
	/// <returns>The access token if present; otherwise, <see langword="null"/>.</returns>
	public static IAccessToken? AccessToken(this IActivityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return context.GetValue<IAccessToken?>("AccessToken", defaultValue: null);
	}

	/// <summary>
	/// Retrieves the user identifier from the activity context if available.
	/// </summary>
	/// <param name="context">The activity context.</param>
	/// <returns>The user ID if present; otherwise, <see langword="null"/>.</returns>
	public static string? UserId(this IActivityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return context.AccessToken()?.UserId;
	}

	/// <summary>
	/// Retrieves the application name from the activity context.
	/// </summary>
	/// <param name="context">The activity context.</param>
	/// <returns>The application name if present; otherwise, <see langword="null"/>.</returns>
	public static string? ApplicationName(this IActivityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		var config = context.GetValue<IConfiguration?>("IConfiguration", defaultValue: null);
		return config?["ApplicationName"];
	}

	/// <summary>
	/// Retrieves the client address from the activity context.
	/// </summary>
	/// <param name="context">The activity context.</param>
	/// <returns>The client address if present; otherwise, <see langword="null"/>.</returns>
	public static string? ClientAddress(this IActivityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		var clientAddress = context.GetValue<IClientAddress?>("ClientAddress", defaultValue: null);
		return clientAddress?.Value;
	}

	/// <summary>
	/// Retrieves the correlation ID from the activity context.
	/// </summary>
	/// <param name="context">The activity context.</param>
	/// <returns>The correlation ID if present; otherwise, <see langword="null"/>.</returns>
	public static Guid? CorrelationId(this IActivityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		var correlationId = context.GetValue<ICorrelationId?>("CorrelationId", defaultValue: null);
		return correlationId?.Value;
	}

	/// <summary>
	/// Retrieves the tenant ID from the activity context.
	/// </summary>
	/// <param name="context">The activity context.</param>
	/// <returns>The tenant ID if present; otherwise, <see langword="null"/>.</returns>
	public static string? TenantId(this IActivityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		var tenantId = context.GetValue<ITenantId?>("TenantId", defaultValue: null);
		return tenantId?.Value;
	}

	/// <summary>
	/// Generic method to retrieve a value from the activity context.
	/// </summary>
	/// <typeparam name="T">The type of the value to retrieve.</typeparam>
	/// <param name="context">The activity context.</param>
	/// <param name="key">The key of the value to retrieve.</param>
	/// <returns>The value if present; otherwise, <see langword="null"/>.</returns>
	public static T? Get<T>(this IActivityContext context, string key)
		where T : class
	{
		ArgumentNullException.ThrowIfNull(context);
		return context.GetValue<T?>(key, defaultValue: null);
	}
}
