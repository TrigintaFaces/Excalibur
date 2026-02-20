// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Snapshots;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Core.Snapshots;

/// <summary>
/// Functional tests for <see cref="SnapshotVersionManager"/> covering BFS upgrade paths,
/// multi-hop upgrades, registration validation, and edge cases.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SnapshotVersionManagerFunctionalShould
{
	private sealed class TestSnapshotUpgrader : ISnapshotUpgrader
	{
		private readonly Func<byte[], byte[]> _upgradeFunc;

		public TestSnapshotUpgrader(string aggregateType, int from, int to, Func<byte[], byte[]>? upgradeFunc = null)
		{
			AggregateType = aggregateType;
			FromVersion = from;
			ToVersion = to;
			_upgradeFunc = upgradeFunc ?? (data => Encoding.UTF8.GetBytes($"upgraded-{from}-to-{to}"));
		}

		public string AggregateType { get; }
		public int FromVersion { get; }
		public int ToVersion { get; }
		public bool CanUpgrade(string aggregateType, int fromVersion) =>
			string.Equals(AggregateType, aggregateType, StringComparison.Ordinal) && FromVersion == fromVersion;
		public byte[] Upgrade(byte[] snapshotData) => _upgradeFunc(snapshotData);
	}

	[Fact]
	public void CanUpgrade_WithDirectPath_ShouldReturnTrue()
	{
		// Arrange
		var upgrader = new TestSnapshotUpgrader("Order", 1, 2);
		var sut = new SnapshotVersionManager([upgrader], NullLogger<SnapshotVersionManager>.Instance);

		// Act & Assert
		sut.CanUpgrade("Order", 1, 2).ShouldBeTrue();
	}

	[Fact]
	public void CanUpgrade_WithSameVersion_ShouldReturnTrue()
	{
		// Arrange
		var sut = new SnapshotVersionManager([], NullLogger<SnapshotVersionManager>.Instance);

		// Act & Assert
		sut.CanUpgrade("Order", 1, 1).ShouldBeTrue();
	}

	[Fact]
	public void CanUpgrade_WithNoUpgraders_ShouldReturnFalse()
	{
		// Arrange
		var sut = new SnapshotVersionManager([], NullLogger<SnapshotVersionManager>.Instance);

		// Act & Assert
		sut.CanUpgrade("Order", 1, 2).ShouldBeFalse();
	}

	[Fact]
	public void CanUpgrade_WithMultiHopPath_ShouldReturnTrue()
	{
		// Arrange: 1 -> 2 -> 3 (BFS should find the path)
		var upgraders = new ISnapshotUpgrader[]
		{
			new TestSnapshotUpgrader("Order", 1, 2),
			new TestSnapshotUpgrader("Order", 2, 3),
		};
		var sut = new SnapshotVersionManager(upgraders, NullLogger<SnapshotVersionManager>.Instance);

		// Act & Assert
		sut.CanUpgrade("Order", 1, 3).ShouldBeTrue();
	}

	[Fact]
	public void CanUpgrade_WithNoPath_ShouldReturnFalse()
	{
		// Arrange: only 1->2, no 2->3
		var upgraders = new ISnapshotUpgrader[]
		{
			new TestSnapshotUpgrader("Order", 1, 2),
		};
		var sut = new SnapshotVersionManager(upgraders, NullLogger<SnapshotVersionManager>.Instance);

		// Act & Assert
		sut.CanUpgrade("Order", 1, 3).ShouldBeFalse();
	}

	[Fact]
	public void UpgradeSnapshot_DirectPath_ShouldUpgradeData()
	{
		// Arrange
		var inputData = Encoding.UTF8.GetBytes("v1-data");
		var expectedData = Encoding.UTF8.GetBytes("v2-data");

		var upgrader = new TestSnapshotUpgrader("Order", 1, 2, _ => expectedData);
		var sut = new SnapshotVersionManager([upgrader], NullLogger<SnapshotVersionManager>.Instance);

		// Act
		var result = sut.UpgradeSnapshot("Order", inputData, 1, 2);

		// Assert
		result.ShouldBe(expectedData);
	}

	[Fact]
	public void UpgradeSnapshot_MultiHopPath_ShouldChainUpgrades()
	{
		// Arrange
		var upgraders = new ISnapshotUpgrader[]
		{
			new TestSnapshotUpgrader("Order", 1, 2, data =>
				Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(data) + "+v2")),
			new TestSnapshotUpgrader("Order", 2, 3, data =>
				Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(data) + "+v3")),
		};
		var sut = new SnapshotVersionManager(upgraders, NullLogger<SnapshotVersionManager>.Instance);

		// Act
		var result = sut.UpgradeSnapshot("Order", Encoding.UTF8.GetBytes("v1"), 1, 3);

		// Assert
		Encoding.UTF8.GetString(result).ShouldBe("v1+v2+v3");
	}

	[Fact]
	public void UpgradeSnapshot_SameVersion_ShouldReturnOriginalData()
	{
		// Arrange
		var data = Encoding.UTF8.GetBytes("original");
		var sut = new SnapshotVersionManager([], NullLogger<SnapshotVersionManager>.Instance);

		// Act
		var result = sut.UpgradeSnapshot("Order", data, 1, 1);

		// Assert
		result.ShouldBe(data);
	}

	[Fact]
	public void UpgradeSnapshot_NoUpgradersForAggregate_ShouldThrow()
	{
		// Arrange
		var sut = new SnapshotVersionManager([], NullLogger<SnapshotVersionManager>.Instance);

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			sut.UpgradeSnapshot("Order", [1, 2, 3], 1, 2))
			.Message.ShouldContain("No snapshot upgraders registered");
	}

	[Fact]
	public void UpgradeSnapshot_NoPath_ShouldThrow()
	{
		// Arrange: only 1->2, but requesting 1->5
		var upgraders = new ISnapshotUpgrader[]
		{
			new TestSnapshotUpgrader("Order", 1, 2),
		};
		var sut = new SnapshotVersionManager(upgraders, NullLogger<SnapshotVersionManager>.Instance);

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			sut.UpgradeSnapshot("Order", [1, 2, 3], 1, 5))
			.Message.ShouldContain("No snapshot upgrade path found");
	}

	[Fact]
	public void RegisterUpgrader_Duplicate_ShouldThrow()
	{
		// Arrange
		var upgraders = new ISnapshotUpgrader[]
		{
			new TestSnapshotUpgrader("Order", 1, 2),
			new TestSnapshotUpgrader("Order", 1, 2), // duplicate
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() =>
			new SnapshotVersionManager(upgraders, NullLogger<SnapshotVersionManager>.Instance))
			.Message.ShouldContain("already registered");
	}

	[Fact]
	public void GetRegisteredAggregateTypes_ShouldReturnAllTypes()
	{
		// Arrange
		var upgraders = new ISnapshotUpgrader[]
		{
			new TestSnapshotUpgrader("Order", 1, 2),
			new TestSnapshotUpgrader("Customer", 1, 2),
		};
		var sut = new SnapshotVersionManager(upgraders, NullLogger<SnapshotVersionManager>.Instance);

		// Act
		var types = sut.GetRegisteredAggregateTypes().ToList();

		// Assert
		types.ShouldContain("Order");
		types.ShouldContain("Customer");
		types.Count.ShouldBe(2);
	}

	[Fact]
	public void GetUpgradersForAggregateType_ShouldReturnCorrectUpgraders()
	{
		// Arrange
		var upgraders = new ISnapshotUpgrader[]
		{
			new TestSnapshotUpgrader("Order", 1, 2),
			new TestSnapshotUpgrader("Order", 2, 3),
			new TestSnapshotUpgrader("Customer", 1, 2),
		};
		var sut = new SnapshotVersionManager(upgraders, NullLogger<SnapshotVersionManager>.Instance);

		// Act
		var orderUpgraders = sut.GetUpgradersForAggregateType("Order").ToList();

		// Assert
		orderUpgraders.Count.ShouldBe(2);
	}

	[Fact]
	public void GetUpgradersForAggregateType_Unknown_ShouldReturnEmpty()
	{
		// Arrange
		var sut = new SnapshotVersionManager([], NullLogger<SnapshotVersionManager>.Instance);

		// Act
		var result = sut.GetUpgradersForAggregateType("Unknown").ToList();

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public void UpgradeSnapshot_BFS_ShouldFindShortestPath()
	{
		// Arrange: Two paths: 1->2->4 (short) and 1->2->3->4 (long)
		var callOrder = new List<string>();
		var upgraders = new ISnapshotUpgrader[]
		{
			new TestSnapshotUpgrader("Order", 1, 2, data => { callOrder.Add("1->2"); return data; }),
			new TestSnapshotUpgrader("Order", 2, 3, data => { callOrder.Add("2->3"); return data; }),
			new TestSnapshotUpgrader("Order", 3, 4, data => { callOrder.Add("3->4"); return data; }),
			new TestSnapshotUpgrader("Order", 2, 4, data => { callOrder.Add("2->4"); return data; }),
		};
		var sut = new SnapshotVersionManager(upgraders, NullLogger<SnapshotVersionManager>.Instance);

		// Act
		sut.UpgradeSnapshot("Order", [0], 1, 4);

		// Assert — BFS should find the shortest path: 1->2->4
		callOrder.Count.ShouldBe(2);
		callOrder[0].ShouldBe("1->2");
		callOrder[1].ShouldBe("2->4");
	}
}
