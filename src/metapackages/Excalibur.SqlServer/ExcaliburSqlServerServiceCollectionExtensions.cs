// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Saga.SqlServer.DependencyInjection;
using Excalibur.SqlServer;

using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the complete Excalibur SQL Server stack.
/// </summary>
public static class ExcaliburSqlServerServiceCollectionExtensions
{
	// The two sentences a caller needs: what makes this reflective, and what they can do about it.
	// Held as constants because the same requirement is stated on three members and a hand-copied
	// fourth is how the set comes to disagree with itself.
	private const string OutboxTrimRequirement =
		"The outbox registered by this stack serializes message payloads reflectively. Supply JsonSerializerOptions with a source-generated TypeInfoResolver for your message types to satisfy this under trimming.";

	private const string OutboxAotRequirement =
		"The outbox registered by this stack serializes message payloads reflectively, which requires dynamic code generation. There is no ahead-of-time-safe alternative entry point for this stack.";

	private const string BindingTrimRequirement =
		OutboxTrimRequirement + " Binding the configuration section also reflects over the options type.";

	private const string BindingAotRequirement =
		OutboxAotRequirement + " Binding the configuration section also requires dynamic code.";

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
	///     sql.ConfigureSaga(saga => saga.SchemaName("custom"));
	///     sql.ConfigureAuditLogging(audit => audit.SchemaName = "audit");
	/// });
	/// </code>
	/// </example>
	/// <remarks>
	/// Not usable under Native AOT: the outbox this stack registers serializes message payloads
	/// reflectively and there is no ahead-of-time-safe alternative entry point. Under trimming it is
	/// usable, but you must supply <c>JsonSerializerOptions</c> with a source-generated
	/// <c>TypeInfoResolver</c> covering your message types.
	/// </remarks>
	[RequiresUnreferencedCode(OutboxTrimRequirement)]
	[RequiresDynamicCode(OutboxAotRequirement)]
	public static IServiceCollection AddExcaliburSqlServer(
		this IServiceCollection services,
		Action<ExcaliburSqlServerOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new ExcaliburSqlServerOptions();
		configure(options);

		return RegisterSqlServerServices(services, options);
	}

	/// <summary>
	/// Adds the complete Excalibur SQL Server stack using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind options from.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configuration"/> is <see langword="null"/>.</exception>
	/// <remarks>
	/// Not usable under Native AOT: the outbox this stack registers serializes message payloads
	/// reflectively and there is no ahead-of-time-safe alternative entry point. Under trimming it is
	/// usable, but you must supply <c>JsonSerializerOptions</c> with a source-generated
	/// <c>TypeInfoResolver</c> covering your message types.
	/// </remarks>
	[RequiresUnreferencedCode(BindingTrimRequirement)]
	[RequiresDynamicCode(BindingAotRequirement)]
	public static IServiceCollection AddExcaliburSqlServer(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		var options = new ExcaliburSqlServerOptions();
		configuration.Bind(options);

		return RegisterSqlServerServices(services, options);
	}

	[RequiresUnreferencedCode(OutboxTrimRequirement)]
	[RequiresDynamicCode(OutboxAotRequirement)]
	private static IServiceCollection RegisterSqlServerServices(
		IServiceCollection services,
		ExcaliburSqlServerOptions options)
	{
		// Core: Dispatch + EventSourcing + Outbox (via the starter metapackage). That call stages
		// outbox messages durably but starts no delivery loop; background drain stays opt-in here too.
		_ = services.AddDispatchWithSqlServer(options.ConnectionString, options.DispatchConfiguration);

		// Inbox (builder API)
		if (options.UseInbox)
		{
			_ = services.AddExcaliburInbox(inbox =>
				inbox.UseSqlServer(sql =>
				{
					sql.ConnectionString(options.ConnectionString);
					options.InboxConfiguration?.Invoke(sql);
				}));
		}

		// Saga (builder API) — routed through IExcaliburBuilder bridge so the
		// flat aggregator stays internal composition-root policy.
		if (options.UseSaga)
		{
			_ = services.AddExcalibur(excalibur =>
				excalibur.AddSagas(saga =>
					saga.UseSqlServer(sql =>
					{
						sql.ConnectionString(options.ConnectionString);
						options.SagaConfiguration?.Invoke(sql);
					})));
		}

		// Leader Election (builder API) — routed through IExcaliburBuilder
		// bridge so the flat aggregator stays internal .
		if (options.UseLeaderElection)
		{
			_ = services.AddExcalibur(excalibur =>
				excalibur.AddLeaderElection(le =>
					le.UseSqlServer(sql =>
					{
						sql.ConnectionString(options.ConnectionString)
						   .LockResource("excalibur-leader");
						options.LeaderElectionConfiguration?.Invoke(sql);
					})));
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
		_ = services.AddSqlServerDataExecutors(() => new Data.SqlClient.SqlConnection(options.ConnectionString));

		return services;
	}
}
