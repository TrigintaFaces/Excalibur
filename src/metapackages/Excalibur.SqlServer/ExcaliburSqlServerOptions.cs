// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.ComponentModel.DataAnnotations;

using Excalibur.AuditLogging.SqlServer;
using Excalibur.Compliance.SqlServer;
using Excalibur.Compliance.SqlServer.Erasure;
using Excalibur.Dispatch.Configuration;
using Excalibur.Hosting.Builders;
using Excalibur.Inbox.SqlServer;
using Excalibur.LeaderElection.SqlServer;
using Excalibur.Saga.SqlServer;

namespace Excalibur.SqlServer;

/// <summary>
/// Options for configuring the complete Excalibur SQL Server stack.
/// </summary>
/// <remarks>
/// <para>
/// Subsystem callbacks use builder interfaces (<c>Action&lt;IXxxBuilder&gt;</c>) for
/// consistent composition with the individual packages' builder APIs. The metapackage
/// automatically flows <see cref="ConnectionString"/> into each subsystem builder.
/// </para>
/// </remarks>
public sealed class ExcaliburSqlServerOptions
{
	/// <summary>
	/// Gets or sets the SQL Server connection string shared by all components.
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
	/// services.AddExcaliburSqlServer(o =&gt; o.ConnectionString = connectionString);
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
	/// Configures the framework's own composition seam, for anything this preset does not expose.
	/// </summary>
	/// <param name="configure">A delegate to configure the Excalibur builder.</param>
	/// <returns>This options instance for chaining.</returns>
	/// <remarks>
	/// <para>
	/// The members on this type are a CURATED SUBSET, not the whole framework. They cover the subsystems a
	/// SQL Server backend implements, which leaves several of the builder's capabilities with no expression
	/// here at all: change-data capture, jobs, the identity map, data processing, assembly scanning, tenancy
	/// and the local client address. Tenancy is the one most likely to be wanted and missed.
	/// </para>
	/// <para>
	/// Two further subsystems are REGISTERED by this entry point but not configurable through it — event
	/// sourcing and the outbox, the latter staging messages durably while starting no delivery loop. Turning
	/// background delivery on is therefore a thing this preset registers for you and gives you no way to
	/// adjust.
	/// </para>
	/// <para>
	/// Without this member the only route to any of that was a SECOND <c>AddExcalibur</c> call beside this
	/// one, which works and is what the composition does internally, but leaves the consumer composing two
	/// surfaces with no stated ordering. This runs LAST, after everything the preset declares, so a
	/// declaration made here wins over the preset's — which is the same precedence a separate later call
	/// already had, now written down and reachable from inside the entry point.
	/// </para>
	/// </remarks>
	public ExcaliburSqlServerOptions ConfigureExcalibur(Action<IExcaliburBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		ExcaliburConfiguration = configure;
		return this;
	}

	/// <summary>
	/// Configures the Dispatch pipeline (middleware, behaviors, handler registration).
	/// </summary>
	/// <param name="configure">A delegate to configure the dispatch builder.</param>
	/// <returns>This options instance for chaining.</returns>
	public ExcaliburSqlServerOptions ConfigureDispatch(Action<IDispatchBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		DispatchConfiguration = configure;
		return this;
	}

	/// <summary>
	/// Configures the SQL Server inbox builder (schema, table names, connection overrides).
	/// </summary>
	/// <param name="configure">A delegate to configure the SQL Server inbox builder.</param>
	/// <returns>This options instance for chaining.</returns>
	public ExcaliburSqlServerOptions ConfigureInbox(Action<ISqlServerInboxBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		InboxConfiguration = configure;
		return this;
	}

	/// <summary>
	/// Configures the SQL Server saga builder (schema, table names, connection overrides).
	/// </summary>
	/// <param name="configure">A delegate to configure the SQL Server saga builder.</param>
	/// <returns>This options instance for chaining.</returns>
	public ExcaliburSqlServerOptions ConfigureSaga(Action<ISqlServerSagaBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		SagaConfiguration = configure;
		return this;
	}

	/// <summary>
	/// Configures the SQL Server leader election builder (lock resource, connection overrides).
	/// </summary>
	/// <param name="configure">A delegate to configure the SQL Server leader election builder.</param>
	/// <returns>This options instance for chaining.</returns>
	public ExcaliburSqlServerOptions ConfigureLeaderElection(Action<ISqlServerLeaderElectionBuilder> configure)
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
	public ExcaliburSqlServerOptions ConfigureAuditLogging(Action<SqlServerAuditOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		AuditLoggingConfiguration = configure;
		return this;
	}

	/// <summary>
	/// Configures key escrow store options (schema name, table name).
	/// </summary>
	/// <param name="configure">A delegate to configure key escrow options.</param>
	/// <returns>This options instance for chaining.</returns>
	public ExcaliburSqlServerOptions ConfigureKeyEscrow(Action<SqlServerKeyEscrowOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		KeyEscrowConfiguration = configure;
		return this;
	}

	/// <summary>
	/// Configures erasure store options (schema name, table name).
	/// </summary>
	/// <param name="configure">A delegate to configure erasure options.</param>
	/// <returns>This options instance for chaining.</returns>
	public ExcaliburSqlServerOptions ConfigureErasure(Action<SqlServerErasureStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		ErasureConfiguration = configure;
		return this;
	}

	internal Action<IExcaliburBuilder>? ExcaliburConfiguration { get; private set; }

	internal Action<IDispatchBuilder>? DispatchConfiguration { get; private set; }

	internal Action<ISqlServerInboxBuilder>? InboxConfiguration { get; private set; }

	internal Action<ISqlServerSagaBuilder>? SagaConfiguration { get; private set; }

	internal Action<ISqlServerLeaderElectionBuilder>? LeaderElectionConfiguration { get; private set; }

	internal Action<SqlServerAuditOptions>? AuditLoggingConfiguration { get; private set; }

	internal Action<SqlServerKeyEscrowOptions>? KeyEscrowConfiguration { get; private set; }

	internal Action<SqlServerErasureStoreOptions>? ErasureConfiguration { get; private set; }
}
