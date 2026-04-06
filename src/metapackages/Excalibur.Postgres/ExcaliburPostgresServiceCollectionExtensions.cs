// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Excalibur.Postgres;

using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the complete Excalibur PostgreSQL stack.
/// </summary>
public static class ExcaliburPostgresServiceCollectionExtensions
{
	/// <summary>
	/// Adds the complete Excalibur PostgreSQL stack: event sourcing, outbox, inbox,
	/// sagas, leader election, audit logging, compliance, and data access.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">An action to configure the PostgreSQL options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburPostgres(pg =>
	/// {
	///     pg.ConnectionString = connectionString;
	///     pg.UseLeaderElection = true;
	///     pg.ConfigureSaga(saga => saga.SchemaName = "custom");
	///     pg.ConfigureAuditLogging(audit => audit.SchemaName = "audit");
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddExcaliburPostgres(
		this IServiceCollection services,
		Action<ExcaliburPostgresOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new ExcaliburPostgresOptions();
		configure(options);

		return RegisterPostgresServices(services, options);
	}

	/// <summary>
	/// Adds the complete Excalibur PostgreSQL stack using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind options from.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configuration"/> is <see langword="null"/>.</exception>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddExcaliburPostgres(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		var options = new ExcaliburPostgresOptions();
		configuration.Bind(options);

		return RegisterPostgresServices(services, options);
	}

	private static IServiceCollection RegisterPostgresServices(
		IServiceCollection services,
		ExcaliburPostgresOptions options)
	{
		// Core: Dispatch + EventSourcing + Outbox + Hosting (via starter metapackage)
		_ = services.AddDispatchWithPostgres(options.ConnectionString, options.DispatchConfiguration);

		// Inbox
		if (options.UseInbox)
		{
			_ = services.AddPostgresInboxStore(inbox =>
			{
				inbox.ConnectionString = options.ConnectionString;
				options.InboxConfiguration?.Invoke(inbox);
			});
		}

		// Saga
		if (options.UseSaga)
		{
			_ = services.AddPostgresSagaStore(saga =>
			{
				saga.ConnectionString = options.ConnectionString;
				options.SagaConfiguration?.Invoke(saga);
			});
		}

		// Leader Election
		if (options.UseLeaderElection)
		{
			_ = services.AddPostgresLeaderElection(le =>
			{
				le.ConnectionString = options.ConnectionString;
				options.LeaderElectionConfiguration?.Invoke(le);
			});
		}

		// Audit Logging (Dispatch AuditLogging provider)
		if (options.UseAuditLogging)
		{
			_ = services.AddPostgresAuditStore(
				audit =>
				{
					audit.ConnectionString = options.ConnectionString;
					options.AuditLoggingConfiguration?.Invoke(audit);
				});
		}

		// Compliance (Erasure)
		if (options.UseCompliance)
		{
			_ = services.AddPostgresErasureStore(erasure =>
			{
				erasure.ConnectionString = options.ConnectionString;
				options.ErasureConfiguration?.Invoke(erasure);
			});
		}

		// Data Access
		_ = services.AddPostgresDataExecutors(
			() => new Npgsql.NpgsqlConnection(options.ConnectionString));

		return services;
	}
}
