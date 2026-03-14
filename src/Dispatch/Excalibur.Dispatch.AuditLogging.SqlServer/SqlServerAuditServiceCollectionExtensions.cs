// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.AuditLogging.SqlServer;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring SQL Server audit logging services.
/// </summary>
public static class SqlServerAuditServiceCollectionExtensions
{
	/// <summary>
	/// Adds SQL Server audit logging services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">An action to configure the SQL Server audit options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or configure is null.</exception>
	/// <example>
	/// <code>
	/// services.AddSqlServerAuditStore(options =>
	/// {
	///     options.ConnectionString = configuration.GetConnectionString("AuditDb");
	///     options.SchemaName = "audit";
	///     options.Retention.RetentionPeriod = TimeSpan.FromDays(7 * 365); // 7 years for SOC2
	///     options.EnableHashChain = true;
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddSqlServerAuditStore(
		this IServiceCollection services,
		Action<SqlServerAuditOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);
		services.TryAddSingleton<SqlServerAuditStore>();
		services.TryAddSingleton<IAuditStore>(sp => sp.GetRequiredService<SqlServerAuditStore>());

		return services;
	}

	/// <summary>
	/// Adds SQL Server audit logging services with a pre-configured options instance.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="options">The pre-configured options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or options is null.</exception>
	public static IServiceCollection AddSqlServerAuditStore(
		this IServiceCollection services,
		SqlServerAuditOptions options)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(options);

		return services.AddSqlServerAuditStore(o =>
		{
			o.ConnectionString = options.ConnectionString;
			o.SchemaName = options.SchemaName;
			o.TableName = options.TableName;
			o.BatchInsertSize = options.BatchInsertSize;
			o.Retention.RetentionPeriod = options.Retention.RetentionPeriod;
			o.Retention.EnableRetentionEnforcement = options.Retention.EnableRetentionEnforcement;
			o.Retention.CleanupInterval = options.Retention.CleanupInterval;
			o.Retention.CleanupBatchSize = options.Retention.CleanupBatchSize;
			o.CommandTimeoutSeconds = options.CommandTimeoutSeconds;
			o.UsePartitioning = options.UsePartitioning;
			o.EnableHashChain = options.EnableHashChain;
			o.EnableDetailedTelemetry = options.EnableDetailedTelemetry;
		});
	}
}
