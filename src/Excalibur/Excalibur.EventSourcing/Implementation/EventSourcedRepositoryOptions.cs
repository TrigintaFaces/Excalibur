// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.Implementation;

/// <summary>
/// Configuration options for <see cref="EventSourcedRepository{TAggregate, TKey}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Consolidates configuration values that were previously individual constructor parameters.
/// Service dependencies (IEventStore, IEventSerializer, etc.) remain as constructor parameters.
/// </para>
/// </remarks>
public sealed class EventSourcedRepositoryOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether automatic upcasting is applied during event replay.
	/// </summary>
	/// <value><see langword="true"/> to enable auto-upcasting; otherwise, <see langword="false"/>. Default is <see langword="false"/>.</value>
	public bool EnableAutoUpcast { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether automatic snapshot upgrading is applied on load.
	/// </summary>
	/// <value><see langword="true"/> to enable auto-upgrading; otherwise, <see langword="false"/>. Default is <see langword="false"/>.</value>
	public bool EnableAutoSnapshotUpgrade { get; set; }

	/// <summary>
	/// Gets or sets the target snapshot version for automatic upgrading.
	/// </summary>
	/// <value>The target snapshot version. Default is 1.</value>
	public int TargetSnapshotVersion { get; set; } = 1;

	/// <summary>
	/// Gets or sets the outbox staging strategy for integration events during aggregate save.
	/// </summary>
	/// <value>
	/// The staging strategy. Default is <see cref="OutboxStagingStrategy.Auto"/>, which selects
	/// the best available strategy based on registered infrastructure.
	/// </value>
	public OutboxStagingStrategy OutboxStagingStrategy { get; set; } = OutboxStagingStrategy.Auto;


	/// <summary>
	/// Gets or sets whether a load verifies that the event stream still reaches the version of the
	/// snapshot it is rehydrating from.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Off by default, because the framework cannot produce the damage this detects: it never removes
	/// events below a snapshot. A stream that stops short of its snapshot comes from outside -- a manual
	/// deletion, a partial restore, or an external retention job trimming the event table. Turn this on if
	/// anything other than this framework deletes from your event store.
	/// </para>
	/// <para>
	/// What it catches: a snapshot at version 50 over a stream whose events stop at version 40 loads zero
	/// events after the snapshot -- indistinguishable from the ordinary case of a snapshot at the head of
	/// the stream -- and would otherwise rehydrate into an aggregate at a version its own stream never
	/// reached. With this on, that load throws instead.
	/// </para>
	/// <para>
	/// What it costs: one indexed maximum-version query per load whose snapshot is already current, which
	/// is the ordinary state of a snapshotted aggregate. Loads that return events cost nothing extra, and
	/// a store that does not provide the version-probe capability is never queried and never verified.
	/// </para>
	/// </remarks>
	/// <value> <see langword="false" /> by default. </value>
	public bool VerifyStreamReachesSnapshot { get; set; }
}
