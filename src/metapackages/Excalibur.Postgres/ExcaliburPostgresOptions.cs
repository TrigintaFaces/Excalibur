// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.Postgres.Erasure;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.AuditLogging.Postgres;
using Excalibur.Inbox.Postgres;
using Excalibur.LeaderElection.Postgres;
using Excalibur.Saga.Postgres;

namespace Excalibur.Postgres;

/// <summary>
/// Options for configuring the complete Excalibur PostgreSQL stack.
/// </summary>
public sealed class ExcaliburPostgresOptions
{
	/// <summary>
	/// Gets or sets the PostgreSQL connection string shared by all components.
	/// </summary>
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
	public ExcaliburPostgresOptions ConfigureDispatch(Action<IDispatchBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		DispatchConfiguration = configure;
		return this;
	}

	/// <summary>
	/// Configures inbox store options (schema name, table name, concurrency).
	/// </summary>
	/// <param name="configure">A delegate to configure inbox options.</param>
	/// <returns>This options instance for chaining.</returns>
	public ExcaliburPostgresOptions ConfigureInbox(Action<PostgresInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		InboxConfiguration = configure;
		return this;
	}

	/// <summary>
	/// Configures saga store options (schema name, table names).
	/// </summary>
	/// <param name="configure">A delegate to configure saga options.</param>
	/// <returns>This options instance for chaining.</returns>
	public ExcaliburPostgresOptions ConfigureSaga(Action<PostgresSagaOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		SagaConfiguration = configure;
		return this;
	}

	/// <summary>
	/// Configures leader election options (lock key, lease duration).
	/// </summary>
	/// <param name="configure">A delegate to configure leader election options.</param>
	/// <returns>This options instance for chaining.</returns>
	public ExcaliburPostgresOptions ConfigureLeaderElection(Action<PostgresLeaderElectionOptions> configure)
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

	internal Action<PostgresInboxOptions>? InboxConfiguration { get; private set; }

	internal Action<PostgresSagaOptions>? SagaConfiguration { get; private set; }

	internal Action<PostgresLeaderElectionOptions>? LeaderElectionConfiguration { get; private set; }

	internal Action<PostgresAuditOptions>? AuditLoggingConfiguration { get; private set; }

	internal Action<PostgresErasureStoreOptions>? ErasureConfiguration { get; private set; }
}
