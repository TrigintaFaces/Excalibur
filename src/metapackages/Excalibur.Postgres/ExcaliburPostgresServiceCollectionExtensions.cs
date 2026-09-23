// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Postgres;
using Excalibur.Saga.Postgres.DependencyInjection;

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Middleware.Inbox;

using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the complete Excalibur PostgreSQL stack.
/// </summary>
public static class ExcaliburPostgresServiceCollectionExtensions
{
	// One message per hazard, shared by every entry point that carries it. Two overloads of one method
	// diverging on their declared ahead-of-time behaviour is how the set comes to disagree with itself.
	private const string PipelineTrimRequirement =
		"Registers the reflection-based dispatch pipeline, which requires types that trimming may remove. Use the source-generated handler registration for an ahead-of-time compatible composition.";

	private const string PipelineAotRequirement =
		"Registers the reflection-based dispatch pipeline, which constructs typed invokers at runtime. Use the source-generated handler registration for an ahead-of-time compatible composition.";

	private const string BindingTrimRequirement =
		PipelineTrimRequirement + " Binding the configuration section also reflects over the options type.";

	private const string BindingAotRequirement =
		PipelineAotRequirement + " Binding the configuration section also requires dynamic code.";

	/// <summary>
	/// Adds the Excalibur PostgreSQL stack: event sourcing, inbox, sagas, leader election,
	/// audit logging, compliance, and data access.
	/// </summary>
	/// <remarks>
	/// No outbox is registered. The PostgreSQL outbox store is constructed from an
	/// <c>Excalibur.Data.IDb</c> supplied by the application, so a host that wants one registers its own
	/// <c>IDb</c> and declares the outbox against it with
	/// <c>AddExcalibur(x =&gt; x.AddOutbox(o =&gt; o.UsePostgres(pg =&gt; pg.ConnectionFactory(...))))</c>.
	/// </remarks>
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
	///     pg.ConfigureSaga(saga => saga.SchemaName("custom"));
	///     pg.ConfigureAuditLogging(audit => audit.SchemaName = "audit");
	/// });
	/// </code>
	/// </example>
	[RequiresUnreferencedCode(PipelineTrimRequirement)]
	[RequiresDynamicCode(PipelineAotRequirement)]
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
	/// <remarks>
	/// Carries the same trimming and ahead-of-time requirement as the <see cref="Action{T}"/> overload,
	/// because it registers the same reflection-based dispatch pipeline, and additionally reflects over
	/// the options type to bind the configuration section. No outbox is registered by either overload.
	/// </remarks>
	[RequiresUnreferencedCode(BindingTrimRequirement)]
	[RequiresDynamicCode(BindingAotRequirement)]
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

	[RequiresUnreferencedCode(PipelineTrimRequirement)]
	[RequiresDynamicCode(PipelineAotRequirement)]
	private static IServiceCollection RegisterPostgresServices(
		IServiceCollection services,
		ExcaliburPostgresOptions options)
	{
		// Core: Dispatch + EventSourcing (via the starter metapackage). No outbox: see the remarks on
		// the public entry point above for what a host must supply to get one.
		// UseInbox means the inbox is OPERATIVE, not that a store is in the container: the store is read by
		// the retry drain and the manual IInboxProcessor path, while DEDUPLICATION is performed by the inbox
		// middleware. Registering the store alone left a host that took the default with an option named
		// UseInbox set to true and nothing deduplicating, and no way to tell -- an absence of duplicate
		// suppression looks exactly like an absence of duplicates. So the option places both halves.
		var configureDispatch = options.DispatchConfiguration;
		if (options.UseInbox)
		{
			// Mirrors AddDispatchWithPostgres's own default for a host that supplied no dispatch configuration.
			var handlers = configureDispatch ?? (static dispatch => _ = dispatch.AddHandlersFromEntryAssembly());
			configureDispatch = dispatch =>
			{
				// WithInboxMode selects the durable store-backed inbox over in-memory deduplication, which is
				// the correct mode here because a store is registered below. Pipeline position is decided by
				// the middleware's own declared stage, not by where it is added.
				_ = dispatch.WithInboxMode().UseInbox();
				handlers(dispatch);
			};
		}

		_ = services.AddDispatchWithPostgres(options.ConnectionString, configureDispatch);

		// Inbox (builder API)
		if (options.UseInbox)
		{
			_ = services.AddExcaliburInbox(inbox =>
				inbox.UsePostgres(pg =>
				{
					pg.ConnectionString(options.ConnectionString);
					options.InboxConfiguration?.Invoke(pg);
				}));
		}

		// Saga (builder API) — routed through IExcaliburBuilder bridge so the
		// flat aggregator stays internal composition-root policy.
		if (options.UseSaga)
		{
			_ = services.AddExcalibur(excalibur =>
				excalibur.AddSagas(saga =>
					saga.UsePostgres(pg =>
					{
						pg.ConnectionString(options.ConnectionString);
						options.SagaConfiguration?.Invoke(pg);
					})));
		}

		// Leader Election (builder API) — routed through IExcaliburBuilder
		// bridge so the flat aggregator stays internal .
		if (options.UseLeaderElection)
		{
			_ = services.AddExcalibur(excalibur =>
				excalibur.AddLeaderElection(le =>
					le.UsePostgres(pg =>
					{
						pg.ConnectionString(options.ConnectionString);
						options.LeaderElectionConfiguration?.Invoke(pg);
					})));
		}

		// Audit Logging
		if (options.UseAuditLogging)
		{
			_ = services.AddPostgresAuditStore(audit =>
			{
				audit.ConnectionString = options.ConnectionString;
				options.AuditLoggingConfiguration?.Invoke(audit);
			});
		}

		// Compliance (erasure + legal hold + data inventory)
		//
		// The whole set, not the erasure store alone. Erasure consults legal holds through an OPTIONAL
		// dependency and skips the check when it is absent, so wiring erasure without holds produces a
		// pipeline that destroys data irreversibly and never consults the records that exist to stop it.
		// The provider's own aggregate registers all three stores; AddLegalHoldService supplies the service
		// the erasure path actually asks for, which the stores alone do not.
		if (options.UseCompliance)
		{
			_ = services.AddPostgresCompliance(compliance => compliance.ConnectionString(options.ConnectionString));
			_ = services.AddLegalHoldService();

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
