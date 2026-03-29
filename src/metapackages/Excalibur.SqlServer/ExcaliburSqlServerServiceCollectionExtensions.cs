// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.SqlServer;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the complete Excalibur SQL Server stack.
/// </summary>
public static class ExcaliburSqlServerServiceCollectionExtensions
{
	/// <summary>
	/// Adds the complete Excalibur SQL Server stack: event sourcing, outbox, inbox,
	/// sagas, leader election, audit logging, compliance, and data access.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">An action to configure the SQL Server options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburSqlServer(sql =>
	/// {
	///     sql.ConnectionString = connectionString;
	///     sql.UseLeaderElection = true;
	///     sql.ConfigureSaga(saga => saga.SchemaName = "custom");
	///     sql.ConfigureAuditLogging(audit => audit.SchemaName = "audit");
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddExcaliburSqlServer(
		this IServiceCollection services,
		Action<ExcaliburSqlServerOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new ExcaliburSqlServerOptions();
		configure(options);

		// Core: Dispatch + EventSourcing + Outbox + Hosting (via starter metapackage)
		_ = services.AddDispatchWithSqlServer(options.ConnectionString, options.DispatchConfiguration);

		// Inbox
		if (options.UseInbox)
		{
			_ = services.AddExcaliburInbox(inbox =>
				inbox.UseSqlServer(sql =>
				{
					sql.ConnectionString = options.ConnectionString;
					options.InboxConfiguration?.Invoke(sql);
				}));
		}

		// Saga
		if (options.UseSaga)
		{
			_ = services.AddSqlServerSagaStore(saga =>
			{
				saga.ConnectionString = options.ConnectionString;
				options.SagaConfiguration?.Invoke(saga);
			});
		}

		// Leader Election
		if (options.UseLeaderElection)
		{
			_ = services.AddSqlServerLeaderElection(
				options.ConnectionString,
				"excalibur-leader",
				le => options.LeaderElectionConfiguration?.Invoke(le));
		}

		// Audit Logging
		if (options.UseAuditLogging)
		{
			_ = services.AddSqlServerAuditStore(audit =>
			{
				audit.ConnectionString = options.ConnectionString;
				options.AuditLoggingConfiguration?.Invoke(audit);
			});
		}

		// Compliance (Key Escrow + Erasure)
		if (options.UseCompliance)
		{
			_ = services.AddSqlServerKeyEscrow(escrow =>
			{
				escrow.ConnectionString = options.ConnectionString;
				options.KeyEscrowConfiguration?.Invoke(escrow);
			});

			_ = services.AddSqlServerErasureStore(erasure =>
			{
				erasure.ConnectionString = options.ConnectionString;
				options.ErasureConfiguration?.Invoke(erasure);
			});
		}

		// Data Access
		_ = services.AddSqlServerDataExecutors(
			() => new Microsoft.Data.SqlClient.SqlConnection(options.ConnectionString));

		return services;
	}
}
