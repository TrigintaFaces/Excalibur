// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Snapshots;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Core.Snapshots;

/// <summary>
/// Depth coverage tests for <see cref="SnapshotVersionManager"/>.
/// Covers BFS path finding, multi-hop upgrades, duplicate detection, and edge cases.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SnapshotVersionManagerDepthShould
{
	private readonly ILogger<SnapshotVersionManager> _logger = NullLogger<SnapshotVersionManager>.Instance;

	[Fact]
	public void Constructor_RegistersAllUpgraders()
	{
		// Arrange
		var upgraders = new ISnapshotUpgrader[]
		{
			new TestUpgrader("OrderAggregate", 1, 2),
			new TestUpgrader("OrderAggregate", 2, 3),
		};

		// Act
		var manager = new SnapshotVersionManager(upgraders, _logger);

		// Assert
		manager.GetRegisteredAggregateTypes().ShouldContain("OrderAggregate");
		manager.GetUpgradersForAggregateType("OrderAggregate").Count().ShouldBe(2);
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new SnapshotVersionManager([], null!));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenUpgradersIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new SnapshotVersionManager(null!, _logger));
	}

	[Fact]
	public void RegisterUpgrader_ThrowsArgumentNullException_WhenNull()
	{
		var manager = new SnapshotVersionManager([], _logger);
		Should.Throw<ArgumentNullException>(() => manager.RegisterUpgrader(null!));
	}

	[Fact]
	public void RegisterUpgrader_ThrowsInvalidOperationException_WhenDuplicate()
	{
		// Arrange
		var manager = new SnapshotVersionManager(
			[new TestUpgrader("Order", 1, 2)], _logger);

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			manager.RegisterUpgrader(new TestUpgrader("Order", 1, 2)));
	}

	[Fact]
	public void CanUpgrade_ReturnsTrueForSameVersion()
	{
		var manager = new SnapshotVersionManager([], _logger);
		manager.CanUpgrade("AnyType", 1, 1).ShouldBeTrue();
	}

	[Fact]
	public void CanUpgrade_ReturnsFalse_WhenNoUpgradersRegistered()
	{
		var manager = new SnapshotVersionManager([], _logger);
		manager.CanUpgrade("UnknownType", 1, 2).ShouldBeFalse();
	}

	[Fact]
	public void CanUpgrade_ReturnsTrue_ForDirectPath()
	{
		var manager = new SnapshotVersionManager(
			[new TestUpgrader("Order", 1, 2)], _logger);

		manager.CanUpgrade("Order", 1, 2).ShouldBeTrue();
	}

	[Fact]
	public void CanUpgrade_ReturnsTrue_ForMultiHopPath()
	{
		var manager = new SnapshotVersionManager(new ISnapshotUpgrader[]
		{
			new TestUpgrader("Order", 1, 2),
			new TestUpgrader("Order", 2, 3),
			new TestUpgrader("Order", 3, 4),
		}, _logger);

		manager.CanUpgrade("Order", 1, 4).ShouldBeTrue();
	}

	[Fact]
	public void CanUpgrade_ReturnsFalse_WhenNoPath()
	{
		var manager = new SnapshotVersionManager(
			[new TestUpgrader("Order", 1, 2)], _logger);

		manager.CanUpgrade("Order", 1, 5).ShouldBeFalse();
	}

	[Fact]
	public void UpgradeSnapshot_ReturnsSameData_WhenSameVersion()
	{
		var manager = new SnapshotVersionManager([], _logger);
		var data = new byte[] { 1, 2, 3 };

		var result = manager.UpgradeSnapshot("Order", data, 1, 1);

		result.ShouldBeSameAs(data);
	}

	[Fact]
	public void UpgradeSnapshot_ThrowsArgumentException_WhenAggregateTypeIsNull()
	{
		var manager = new SnapshotVersionManager([], _logger);
		Should.Throw<ArgumentException>(() =>
			manager.UpgradeSnapshot(null!, [1], 1, 2));
	}

	[Fact]
	public void UpgradeSnapshot_ThrowsArgumentNullException_WhenDataIsNull()
	{
		var manager = new SnapshotVersionManager(
			[new TestUpgrader("Order", 1, 2)], _logger);

		Should.Throw<ArgumentNullException>(() =>
			manager.UpgradeSnapshot("Order", null!, 1, 2));
	}

	[Fact]
	public void UpgradeSnapshot_ThrowsInvalidOperationException_WhenNoUpgraders()
	{
		var manager = new SnapshotVersionManager([], _logger);

		Should.Throw<InvalidOperationException>(() =>
			manager.UpgradeSnapshot("Order", [1], 1, 2));
	}

	[Fact]
	public void UpgradeSnapshot_ThrowsInvalidOperationException_WhenNoPath()
	{
		var manager = new SnapshotVersionManager(
			[new TestUpgrader("Order", 1, 2)], _logger);

		Should.Throw<InvalidOperationException>(() =>
			manager.UpgradeSnapshot("Order", [1], 1, 5));
	}

	[Fact]
	public void UpgradeSnapshot_PerformsDirectUpgrade()
	{
		// Arrange
		var upgrader = new TestUpgrader("Order", 1, 2);
		var manager = new SnapshotVersionManager([upgrader], _logger);

		// Act
		var result = manager.UpgradeSnapshot("Order", [0x01], 1, 2);

		// Assert
		result.ShouldNotBeNull();
		result.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void UpgradeSnapshot_PerformsMultiHopUpgrade()
	{
		// Arrange — chain: 1 → 2 → 3
		var manager = new SnapshotVersionManager(new ISnapshotUpgrader[]
		{
			new TestUpgrader("Order", 1, 2),
			new TestUpgrader("Order", 2, 3),
		}, _logger);

		// Act
		var result = manager.UpgradeSnapshot("Order", [0x01], 1, 3);

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public void GetRegisteredAggregateTypes_ReturnsEmpty_WhenNoneRegistered()
	{
		var manager = new SnapshotVersionManager([], _logger);
		manager.GetRegisteredAggregateTypes().ShouldBeEmpty();
	}

	[Fact]
	public void GetUpgradersForAggregateType_ReturnsEmpty_WhenTypeNotFound()
	{
		var manager = new SnapshotVersionManager([], _logger);
		manager.GetUpgradersForAggregateType("Unknown").ShouldBeEmpty();
	}

	private sealed class TestUpgrader : ISnapshotUpgrader
	{
		public TestUpgrader(string aggregateType, int fromVersion, int toVersion)
		{
			AggregateType = aggregateType;
			FromVersion = fromVersion;
			ToVersion = toVersion;
		}

		public string AggregateType { get; }
		public int FromVersion { get; }
		public int ToVersion { get; }

		public bool CanUpgrade(string aggregateType, int fromVersion)
		{
			return AggregateType == aggregateType && FromVersion == fromVersion;
		}

		public byte[] Upgrade(byte[] snapshotData)
		{
			// Simple transformation: append version byte
			var result = new byte[snapshotData.Length + 1];
			snapshotData.CopyTo(result, 0);
			result[^1] = (byte)ToVersion;
			return result;
		}
	}
}
