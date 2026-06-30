// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Security;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the SQL-backed <see cref="ISecurityEventStore"/> bridge.
/// </summary>
public static class SecurityAuditLoggingServiceCollectionExtensions
{
	/// <summary>
	/// Registers an <see cref="ISecurityEventStore"/> that bridges security events onto the
	/// registered <see cref="Excalibur.Compliance.IAuditStore"/>, giving them durable, tamper-evident
	/// SQL persistence without any duplicate storage machinery.
	/// </summary>
	/// <remarks>
	/// The consumer must separately register an <see cref="Excalibur.Compliance.IAuditStore"/>
	/// implementation (e.g. via the SQL Server audit-store registration) for the bridge to resolve.
	/// </remarks>
	/// <param name="services">The service collection.</param>
	/// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
	public static IServiceCollection AddSqlSecurityEventStore(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<ISecurityEventStore, SqlSecurityEventStore>();

		return services;
	}
}
