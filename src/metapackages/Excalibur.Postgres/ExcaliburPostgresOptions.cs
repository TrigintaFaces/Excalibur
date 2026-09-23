// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.ComponentModel.DataAnnotations;

using Excalibur.AuditLogging.Postgres;
using Excalibur.Compliance.Postgres.Erasure;
using Excalibur.Dispatch.Configuration;
using Excalibur.Inbox.Postgres;
using Excalibur.LeaderElection.Postgres;
using Excalibur.Saga.Postgres;

namespace Excalibur.Postgres;

/// <summary>
/// Options for configuring the complete Excalibur PostgreSQL stack.
/// </summary>
/// <remarks>
/// <para>
/// Subsystem callbacks use builder interfaces (<c>Action&lt;IXxxBuilder&gt;</c>) for
/// consistent composition with the individual packages' builder APIs. The metapackage
/// automatically flows <see cref="ConnectionString"/> into each subsystem builder.
/// </para>
/// </remarks>
public sealed class ExcaliburPostgresOptions
{
	/// <summary>
	/// Gets or sets the PostgreSQL connection string shared by all components.
	/// </summary>
	[Required]
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether the inbox is operative — inbound messages are deduplicated
	/// (default: <see langword="true"/>).
	/// </summary>
	/// <remarks>
	/// <para>
	/// This registers the inbox store <b>and</b> places the inbox middleware in the dispatch pipeline, which is
	/// what performs the deduplication. Taking the default is enough; no further call is required:
	/// </para>
	/// <code>
	/// services.AddExcaliburPostgres(o =&gt; o.ConnectionString = connectionString);
	/// // a redelivered message is now suppressed before it reaches its handler
	/// </code>
	/// <para>
	/// Setting this to <see langword="false"/> registers neither half. A host that wants the store on its own —
	/// for the estate-wide retry drain or the manual <c>IInboxProcessor</c> path, both of which are useful
	/// without deduplication — turns this off and registers the store directly with
	/// <c>services.AddExcaliburInbox(...)</c>.
	/// </para>
	/// </remarks>
	public bool UseInbox { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to register saga services (default: <see langword="true"/>).
	/// </summary>
	public bool UseSaga { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to register leader election services (default: <see langword="true"/>).
	/// </summary>
	public bool UseLeaderElection { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to register audit logging services (default: <see langword="true"/>).
	/// </summary>
	public bool UseAuditLogging { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to register compliance (GDPR/erasure) services (default: <see langword="true"/>).
	/// </summary>
	public bool UseCompliance { get; set; } = true;

	/// <summary>
	/// Configures the Dispatch pipeline (middleware, behaviors, handler registration).
	/// </summary>
	/// <param name="configure">A delegate to configure the dispatch builder.</param>
	/// <returns>This options instance for chaining.</returns>
	public ExcaliburPostgresOptions ConfigureDispatch(Action<IDispatchBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		DispatchConfiguration = configure;
		return this;
	}

	/// <summary>
	/// Configures the Postgres inbox builder (schema, table names, connection overrides).
	/// </summary>
	/// <param name="configure">A delegate to configure the Postgres inbox builder.</param>
	/// <returns>This options instance for chaining.</returns>
	public ExcaliburPostgresOptions ConfigureInbox(Action<IPostgresInboxBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		InboxConfiguration = configure;
		return this;
	}

	/// <summary>
	/// Configures the Postgres saga builder (schema, table names, connection overrides).
	/// </summary>
	/// <param name="configure">A delegate to configure the Postgres saga builder.</param>
	/// <returns>This options instance for chaining.</returns>
	public ExcaliburPostgresOptions ConfigureSaga(Action<IPostgresSagaBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		SagaConfiguration = configure;
		return this;
	}

	/// <summary>
	/// Configures the Postgres leader election builder (lock key, connection overrides).
	/// </summary>
	/// <param name="configure">A delegate to configure the Postgres leader election builder.</param>
	/// <returns>This options instance for chaining.</returns>
	public ExcaliburPostgresOptions ConfigureLeaderElection(Action<IPostgresLeaderElectionBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		LeaderElectionConfiguration = configure;
		return this;
	}

	/// <summary>
	/// Configures audit logging store options (schema name, table name).
	/// </summary>
	/// <param name="configure">A delegate to configure audit options.</param>
	/// <returns>This options instance for chaining.</returns>
	public ExcaliburPostgresOptions ConfigureAuditLogging(Action<PostgresAuditOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		AuditLoggingConfiguration = configure;
		return this;
	}

	/// <summary>
	/// Configures erasure store options (schema name, table name).
	/// </summary>
	/// <param name="configure">A delegate to configure erasure options.</param>
	/// <returns>This options instance for chaining.</returns>
	public ExcaliburPostgresOptions ConfigureErasure(Action<PostgresErasureStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		ErasureConfiguration = configure;
		return this;
	}

	internal Action<IDispatchBuilder>? DispatchConfiguration { get; private set; }

	internal Action<IPostgresInboxBuilder>? InboxConfiguration { get; private set; }

	internal Action<IPostgresSagaBuilder>? SagaConfiguration { get; private set; }

	internal Action<IPostgresLeaderElectionBuilder>? LeaderElectionConfiguration { get; private set; }

	internal Action<PostgresAuditOptions>? AuditLoggingConfiguration { get; private set; }

	internal Action<PostgresErasureStoreOptions>? ErasureConfiguration { get; private set; }
}
