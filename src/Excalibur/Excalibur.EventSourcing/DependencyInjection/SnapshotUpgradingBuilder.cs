// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Snapshots;

namespace Excalibur.EventSourcing.DependencyInjection;

/// <summary>
/// Builder for configuring snapshot upgrading infrastructure.
/// </summary>
/// <remarks>
/// <para>
/// This builder registers <see cref="ISnapshotUpgrader"/> implementations with the
/// <see cref="SnapshotVersionManager"/> for automatic snapshot version migration during
/// aggregate hydration.
/// </para>
/// <para><b>Usage:</b>
/// <code>
/// services.AddExcaliburEventSourcing(builder =&gt; builder
///     .AddSnapshotUpgrading(upgrading =&gt; upgrading
///         .RegisterUpgrader&lt;OrderSnapshotV1, OrderSnapshotV2&gt;(new OrderSnapshotUpgraderV1ToV2(serializer))
///         .SetCurrentVersion("OrderAggregate", 3)));
/// </code>
/// </para>
/// </remarks>
public sealed class SnapshotUpgradingBuilder
{
	private readonly List<ISnapshotUpgrader> _upgraders = [];
	private readonly SnapshotUpgradingOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="SnapshotUpgradingBuilder"/> class.
	/// </summary>
	/// <param name="options">The options to configure.</param>
	internal SnapshotUpgradingBuilder(SnapshotUpgradingOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <summary>
	/// Registers a snapshot upgrader instance.
	/// </summary>
	/// <param name="upgrader">The upgrader to register.</param>
	/// <returns>The builder for fluent configuration.</returns>
	public SnapshotUpgradingBuilder RegisterUpgrader(ISnapshotUpgrader upgrader)
	{
		ArgumentNullException.ThrowIfNull(upgrader);
		_upgraders.Add(upgrader);
		return this;
	}

	/// <summary>
	/// Sets the current snapshot version for all aggregates.
	/// </summary>
	/// <param name="version">The target snapshot version.</param>
	/// <returns>The builder for fluent configuration.</returns>
	public SnapshotUpgradingBuilder SetCurrentVersion(int version)
	{
		_options.CurrentSnapshotVersion = version;
		return this;
	}

	/// <summary>
	/// Enables or disables automatic snapshot upgrading during aggregate hydration.
	/// </summary>
	/// <param name="enable">True to enable; false to disable.</param>
	/// <returns>The builder for fluent configuration.</returns>
	public SnapshotUpgradingBuilder EnableAutoUpgradeOnLoad(bool enable = true)
	{
		_options.EnableAutoUpgradeOnLoad = enable;
		return this;
	}

	/// <summary>
	/// Gets the registered upgraders.
	/// </summary>
	internal IReadOnlyList<ISnapshotUpgrader> Upgraders => _upgraders;
}
