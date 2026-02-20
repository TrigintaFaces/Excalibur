// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Hosting.Configuration;

/// <summary>
/// Root configuration options for the Excalibur application framework.
/// </summary>
/// <remarks>
/// <para>
/// This is a lightweight configuration-binding class for <c>appsettings.json</c> scenarios.
/// Each nested options class provides sensible defaults and can be overridden via the
/// <c>AddExcalibur()</c> builder or through configuration binding.
/// </para>
/// <para>
/// For full subsystem configuration including builder-based setup, use the corresponding
/// <c>Add*()</c> methods on <see cref="Builders.IExcaliburBuilder"/>.
/// </para>
/// </remarks>
public sealed class ExcaliburOptions
{
	/// <summary>
	/// Gets or sets event sourcing options.
	/// </summary>
	/// <value>
	/// Event sourcing configuration options.
	/// </value>
	public EventSourcingOptions EventSourcing { get; set; } = new();

	/// <summary>
	/// Gets or sets outbox options.
	/// </summary>
	/// <value>
	/// Outbox configuration options.
	/// </value>
	public OutboxOptions Outbox { get; set; } = new();

	/// <summary>
	/// Gets or sets saga options.
	/// </summary>
	/// <value>
	/// Saga configuration options.
	/// </value>
	public SagaOptions Saga { get; set; } = new();

	/// <summary>
	/// Gets or sets leader election options.
	/// </summary>
	/// <value>
	/// Leader election configuration options.
	/// </value>
	public LeaderElectionOptions LeaderElection { get; set; } = new();

	/// <summary>
	/// Gets or sets change data capture (CDC) options.
	/// </summary>
	/// <value>
	/// CDC configuration options.
	/// </value>
	public CdcOptions Cdc { get; set; } = new();
}

/// <summary>
/// Options for Excalibur event sourcing subsystem.
/// </summary>
/// <remarks>
/// <para>
/// This is a lightweight configuration-binding class for <c>appsettings.json</c> scenarios.
/// For full event sourcing configuration including event store and snapshot providers, use
/// <c>AddEventSourcing()</c> on <see cref="Builders.IExcaliburBuilder"/>.
/// </para>
/// </remarks>
public sealed class EventSourcingOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether event sourcing is enabled.
	/// </summary>
	/// <value><see langword="false"/> by default.</value>
	public bool Enabled { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether snapshots are enabled.
	/// </summary>
	/// <value><see langword="true"/> by default.</value>
	public bool EnableSnapshots { get; set; } = true;

	/// <summary>
	/// Gets or sets the snapshot frequency (number of events between snapshots).
	/// </summary>
	/// <value>100 by default.</value>
	public int SnapshotFrequency { get; set; } = 100;

	/// <summary>
	/// Gets or sets the default stream read batch size.
	/// </summary>
	/// <value>500 by default.</value>
	public int DefaultReadBatchSize { get; set; } = 500;
}

/// <summary>
/// Options for Excalibur outbox subsystem.
/// </summary>
/// <remarks>
/// <para>
/// This is a lightweight configuration-binding class for <c>appsettings.json</c> scenarios.
/// For full outbox configuration including transport and store providers, use
/// <c>AddOutbox()</c> on <see cref="Builders.IExcaliburBuilder"/>.
/// </para>
/// </remarks>
public sealed class OutboxOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether the outbox is enabled.
	/// </summary>
	/// <value><see langword="false"/> by default.</value>
	public bool Enabled { get; set; }

	/// <summary>
	/// Gets or sets the polling interval for outbox message processing.
	/// </summary>
	/// <value>5 seconds by default.</value>
	public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets the maximum batch size for outbox message processing.
	/// </summary>
	/// <value>100 by default.</value>
	public int MaxBatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the maximum number of retry attempts for failed outbox messages.
	/// </summary>
	/// <value>3 by default.</value>
	public int MaxRetryAttempts { get; set; } = 3;
}

/// <summary>
/// Options for Excalibur saga (process manager) subsystem.
/// </summary>
/// <remarks>
/// <para>
/// This is a lightweight configuration-binding class for <c>appsettings.json</c> scenarios.
/// For full saga configuration including saga store and timeout providers, use
/// <c>AddSagas()</c> on <see cref="Builders.IExcaliburBuilder"/>.
/// </para>
/// </remarks>
public sealed class SagaOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether sagas are enabled.
	/// </summary>
	/// <value><see langword="false"/> by default.</value>
	public bool Enabled { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether saga timeouts are enabled.
	/// </summary>
	/// <value><see langword="false"/> by default.</value>
	public bool EnableTimeouts { get; set; }

	/// <summary>
	/// Gets or sets the default saga timeout duration.
	/// </summary>
	/// <value>30 minutes by default.</value>
	public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(30);
}

/// <summary>
/// Options for Excalibur leader election subsystem.
/// </summary>
/// <remarks>
/// <para>
/// This is a lightweight configuration-binding class for <c>appsettings.json</c> scenarios.
/// For full leader election configuration including provider selection, use
/// <c>AddLeaderElection()</c> on <see cref="Builders.IExcaliburBuilder"/>.
/// </para>
/// </remarks>
public sealed class LeaderElectionOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether leader election is enabled.
	/// </summary>
	/// <value><see langword="false"/> by default.</value>
	public bool Enabled { get; set; }

	/// <summary>
	/// Gets or sets the lease duration for leader election.
	/// </summary>
	/// <value>30 seconds by default.</value>
	public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the lease renewal interval.
	/// </summary>
	/// <value>10 seconds by default.</value>
	public TimeSpan RenewInterval { get; set; } = TimeSpan.FromSeconds(10);
}

/// <summary>
/// Options for Excalibur change data capture (CDC) subsystem.
/// </summary>
/// <remarks>
/// <para>
/// This is a lightweight configuration-binding class for <c>appsettings.json</c> scenarios.
/// For full CDC configuration including table tracking and polling, use
/// <c>AddCdc()</c> on <see cref="Builders.IExcaliburBuilder"/>.
/// </para>
/// </remarks>
public sealed class CdcOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether CDC is enabled.
	/// </summary>
	/// <value><see langword="false"/> by default.</value>
	public bool Enabled { get; set; }

	/// <summary>
	/// Gets or sets the polling interval for CDC change detection.
	/// </summary>
	/// <value>10 seconds by default.</value>
	public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(10);

	/// <summary>
	/// Gets or sets the maximum batch size for CDC change processing.
	/// </summary>
	/// <value>200 by default.</value>
	public int MaxBatchSize { get; set; } = 200;
}
