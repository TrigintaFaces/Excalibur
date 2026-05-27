// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

using Excalibur.AuditLogging.SqlServer;
using Excalibur.Compliance.SqlServer;
using Excalibur.Compliance.SqlServer.Erasure;
using Excalibur.Dispatch.Configuration;
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
	/// Gets or sets a value indicating whether to register inbox services (default: <see langword="true"/>).
	/// </summary>
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

	internal Action<IDispatchBuilder>? DispatchConfiguration { get; private set; }

	internal Action<ISqlServerInboxBuilder>? InboxConfiguration { get; private set; }

	internal Action<ISqlServerSagaBuilder>? SagaConfiguration { get; private set; }

	internal Action<ISqlServerLeaderElectionBuilder>? LeaderElectionConfiguration { get; private set; }

	internal Action<SqlServerAuditOptions>? AuditLoggingConfiguration { get; private set; }

	internal Action<SqlServerKeyEscrowOptions>? KeyEscrowConfiguration { get; private set; }

	internal Action<SqlServerErasureStoreOptions>? ErasureConfiguration { get; private set; }
}
