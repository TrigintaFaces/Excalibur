// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
}
