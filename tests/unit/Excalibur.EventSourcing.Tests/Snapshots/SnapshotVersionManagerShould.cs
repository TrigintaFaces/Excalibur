// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Snapshots;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Snapshots;

/// <summary>
/// Unit tests for <see cref="SnapshotVersionManager"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SnapshotVersionManagerShould
{
	private readonly SnapshotVersionManager _manager;

	public SnapshotVersionManagerShould()
	{
		_manager = new SnapshotVersionManager(
			[],
			NullLogger<SnapshotVersionManager>.Instance);
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => new SnapshotVersionManager([], null!));
	}

	[Fact]
	public void Constructor_ThrowArgumentNullException_WhenUpgradersIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => new SnapshotVersionManager(null!, NullLogger<SnapshotVersionManager>.Instance));
	}

	[Fact]
	public void Constructor_RegisterAllProvidedUpgraders()
	{
		// Arrange
		var upgrader1 = CreateMockUpgrader("OrderAggregate", 1, 2);
		var upgrader2 = CreateMockUpgrader("OrderAggregate", 2, 3);

		// Act
		var manager = new SnapshotVersionManager(
			[upgrader1, upgrader2],
			NullLogger<SnapshotVersionManager>.Instance);

		// Assert
		var upgraders = manager.GetUpgradersForAggregateType("OrderAggregate").ToList();
		upgraders.Count.ShouldBe(2);
		upgraders.ShouldContain(upgrader1);
		upgraders.ShouldContain(upgrader2);
	}

	#endregion Constructor Tests

	#region RegisterUpgrader Tests

	[Fact]
	public void RegisterUpgrader_AddUpgraderToRegistry()
	{
		// Arrange
		var upgrader = CreateMockUpgrader("OrderAggregate", 1, 2);

		// Act
		_manager.RegisterUpgrader(upgrader);

		// Assert
		var upgraders = _manager.GetUpgradersForAggregateType("OrderAggregate");
		upgraders.ShouldContain(upgrader);
	}

	[Fact]
	public void RegisterUpgrader_ThrowArgumentNullException_WhenUpgraderIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => _manager.RegisterUpgrader(null!));
	}

	[Fact]
	public void RegisterUpgrader_ThrowInvalidOperationException_WhenDuplicateVersionRange()
	{
		// Arrange
		var upgrader1 = CreateMockUpgrader("OrderAggregate", 1, 2);
		var upgrader2 = CreateMockUpgrader("OrderAggregate", 1, 2);

		_manager.RegisterUpgrader(upgrader1);

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(
			() => _manager.RegisterUpgrader(upgrader2));
		exception.Message.ShouldContain("OrderAggregate");
		exception.Message.ShouldContain("1");
		exception.Message.ShouldContain("2");
	}

	[Fact]
	public void RegisterUpgrader_AllowMultipleUpgradersForSameAggregateType()
	{
		// Arrange
		var upgrader1To2 = CreateMockUpgrader("OrderAggregate", 1, 2);
		var upgrader2To3 = CreateMockUpgrader("OrderAggregate", 2, 3);

		// Act
		_manager.RegisterUpgrader(upgrader1To2);
		_manager.RegisterUpgrader(upgrader2To3);

		// Assert
		var upgraders = _manager.GetUpgradersForAggregateType("OrderAggregate").ToList();
		upgraders.Count.ShouldBe(2);
	}

	[Fact]
	public void RegisterUpgrader_AllowSameVersionRangeForDifferentAggregateTypes()
	{
		// Arrange
		var upgrader1 = CreateMockUpgrader("OrderAggregate", 1, 2);
		var upgrader2 = CreateMockUpgrader("CustomerAggregate", 1, 2);

		// Act & Assert - should not throw
		_manager.RegisterUpgrader(upgrader1);
		_manager.RegisterUpgrader(upgrader2);
	}

	#endregion RegisterUpgrader Tests

	#region UpgradeSnapshot Tests

	[Fact]
	public void UpgradeSnapshot_ReturnSameData_WhenFromVersionEqualsToVersion()
	{
		// Arrange
		var snapshotData = new byte[] { 1, 2, 3 };

		// Act
		var result = _manager.UpgradeSnapshot("OrderAggregate", snapshotData, 1, 1);

		// Assert
		result.ShouldBeSameAs(snapshotData);
	}

	[Fact]
	public void UpgradeSnapshot_ThrowArgumentException_WhenAggregateTypeIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(
			() => _manager.UpgradeSnapshot(null!, new byte[] { 1 }, 1, 2));
	}

	[Fact]
	public void UpgradeSnapshot_ThrowArgumentException_WhenAggregateTypeIsEmpty()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(
			() => _manager.UpgradeSnapshot(string.Empty, new byte[] { 1 }, 1, 2));
	}

	[Fact]
	public void UpgradeSnapshot_ThrowArgumentNullException_WhenSnapshotDataIsNull()
	{
		// Arrange
		var upgrader = CreateMockUpgrader("OrderAggregate", 1, 2);
		_manager.RegisterUpgrader(upgrader);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => _manager.UpgradeSnapshot("OrderAggregate", null!, 1, 2));
	}

	[Fact]
	public void UpgradeSnapshot_ThrowInvalidOperationException_WhenNoUpgradersRegisteredForAggregateType()
	{
		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(
			() => _manager.UpgradeSnapshot("UnknownAggregate", new byte[] { 1 }, 1, 2));
		exception.Message.ShouldContain("UnknownAggregate");
	}

	[Fact]
	public void UpgradeSnapshot_ThrowInvalidOperationException_WhenNoUpgradePathFound()
	{
		// Arrange - Register upgrader from 1 to 2, but ask for 1 to 5
		var upgrader = CreateMockUpgrader("OrderAggregate", 1, 2);
		_manager.RegisterUpgrader(upgrader);

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(
			() => _manager.UpgradeSnapshot("OrderAggregate", new byte[] { 1 }, 1, 5));
		exception.Message.ShouldContain("OrderAggregate");
		exception.Message.ShouldContain("1");
		exception.Message.ShouldContain("5");
	}

	[Fact]
	public void UpgradeSnapshot_FindSingleHopPathAndUpgrade()
	{
		// Arrange
		var originalData = new byte[] { 1, 2, 3 };
		var upgradedData = new byte[] { 4, 5, 6 };

		var upgrader = A.Fake<ISnapshotUpgrader>();
		A.CallTo(() => upgrader.AggregateType).Returns("OrderAggregate");
		A.CallTo(() => upgrader.FromVersion).Returns(1);
		A.CallTo(() => upgrader.ToVersion).Returns(2);
		A.CallTo(() => upgrader.Upgrade(originalData)).Returns(upgradedData);

		_manager.RegisterUpgrader(upgrader);

		// Act
		var result = _manager.UpgradeSnapshot("OrderAggregate", originalData, 1, 2);

		// Assert
		result.ShouldBe(upgradedData);
		A.CallTo(() => upgrader.Upgrade(originalData)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void UpgradeSnapshot_FindMultiHopPathAndUpgradeViaBFS()
	{
		// Arrange
		var v1Data = new byte[] { 1, 2, 3 };
		var v2Data = new byte[] { 4, 5, 6 };
		var v3Data = new byte[] { 7, 8, 9 };

		var upgrader1To2 = A.Fake<ISnapshotUpgrader>();
		A.CallTo(() => upgrader1To2.AggregateType).Returns("OrderAggregate");
		A.CallTo(() => upgrader1To2.FromVersion).Returns(1);
		A.CallTo(() => upgrader1To2.ToVersion).Returns(2);
		A.CallTo(() => upgrader1To2.Upgrade(v1Data)).Returns(v2Data);

		var upgrader2To3 = A.Fake<ISnapshotUpgrader>();
		A.CallTo(() => upgrader2To3.AggregateType).Returns("OrderAggregate");
		A.CallTo(() => upgrader2To3.FromVersion).Returns(2);
		A.CallTo(() => upgrader2To3.ToVersion).Returns(3);
		A.CallTo(() => upgrader2To3.Upgrade(v2Data)).Returns(v3Data);

		_manager.RegisterUpgrader(upgrader1To2);
		_manager.RegisterUpgrader(upgrader2To3);

		// Act
		var result = _manager.UpgradeSnapshot("OrderAggregate", v1Data, 1, 3);

		// Assert
		result.ShouldBe(v3Data);
		A.CallTo(() => upgrader1To2.Upgrade(v1Data)).MustHaveHappenedOnceExactly();
		A.CallTo(() => upgrader2To3.Upgrade(v2Data)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void UpgradeSnapshot_PreferShortestPathViaBFS()
	{
		// Arrange - Register v1->v2->v3 and also v1->v3 (direct)
		var v1Data = new byte[] { 1 };
		var v3DataDirect = new byte[] { 3 };

		var upgrader1To2 = A.Fake<ISnapshotUpgrader>();
		A.CallTo(() => upgrader1To2.AggregateType).Returns("OrderAggregate");
		A.CallTo(() => upgrader1To2.FromVersion).Returns(1);
		A.CallTo(() => upgrader1To2.ToVersion).Returns(2);

		var upgrader2To3 = A.Fake<ISnapshotUpgrader>();
		A.CallTo(() => upgrader2To3.AggregateType).Returns("OrderAggregate");
		A.CallTo(() => upgrader2To3.FromVersion).Returns(2);
		A.CallTo(() => upgrader2To3.ToVersion).Returns(3);

		var upgrader1To3Direct = A.Fake<ISnapshotUpgrader>();
		A.CallTo(() => upgrader1To3Direct.AggregateType).Returns("OrderAggregate");
		A.CallTo(() => upgrader1To3Direct.FromVersion).Returns(1);
		A.CallTo(() => upgrader1To3Direct.ToVersion).Returns(3);
		A.CallTo(() => upgrader1To3Direct.Upgrade(v1Data)).Returns(v3DataDirect);

		_manager.RegisterUpgrader(upgrader1To2);
		_manager.RegisterUpgrader(upgrader2To3);
		_manager.RegisterUpgrader(upgrader1To3Direct);

		// Act
		var result = _manager.UpgradeSnapshot("OrderAggregate", v1Data, 1, 3);

		// Assert - BFS should find v1->v3 direct path (shortest)
		result.ShouldBe(v3DataDirect);
		A.CallTo(() => upgrader1To3Direct.Upgrade(v1Data)).MustHaveHappenedOnceExactly();
		A.CallTo(() => upgrader1To2.Upgrade(A<byte[]>.Ignored)).MustNotHaveHappened();
		A.CallTo(() => upgrader2To3.Upgrade(A<byte[]>.Ignored)).MustNotHaveHappened();
	}

	#endregion UpgradeSnapshot Tests

	#region CanUpgrade Tests

	[Fact]
	public void CanUpgrade_ReturnTrue_WhenFromVersionEqualsToVersion()
	{
		// Act
		var result = _manager.CanUpgrade("OrderAggregate", 1, 1);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void CanUpgrade_ReturnTrue_WhenDirectUpgradePathExists()
	{
		// Arrange
		var upgrader = CreateMockUpgrader("OrderAggregate", 1, 2);
		_manager.RegisterUpgrader(upgrader);

		// Act
		var result = _manager.CanUpgrade("OrderAggregate", 1, 2);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void CanUpgrade_ReturnTrue_WhenMultiHopUpgradePathExists()
	{
		// Arrange
		_manager.RegisterUpgrader(CreateMockUpgrader("OrderAggregate", 1, 2));
		_manager.RegisterUpgrader(CreateMockUpgrader("OrderAggregate", 2, 3));

		// Act
		var result = _manager.CanUpgrade("OrderAggregate", 1, 3);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void CanUpgrade_ReturnFalse_WhenNoUpgradersRegisteredForAggregateType()
	{
		// Act
		var result = _manager.CanUpgrade("UnknownAggregate", 1, 2);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void CanUpgrade_ReturnFalse_WhenNoUpgradePathExists()
	{
		// Arrange - Register v1->v2 but ask for v1->v5
		_manager.RegisterUpgrader(CreateMockUpgrader("OrderAggregate", 1, 2));

		// Act
		var result = _manager.CanUpgrade("OrderAggregate", 1, 5);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion CanUpgrade Tests

	#region GetRegisteredAggregateTypes Tests

	[Fact]
	public void GetRegisteredAggregateTypes_ReturnEmpty_WhenNoUpgradersRegistered()
	{
		// Act
		var aggregateTypes = _manager.GetRegisteredAggregateTypes();

		// Assert
		aggregateTypes.ShouldBeEmpty();
	}

	[Fact]
	public void GetRegisteredAggregateTypes_ReturnAllRegisteredAggregateTypes()
	{
		// Arrange
		_manager.RegisterUpgrader(CreateMockUpgrader("OrderAggregate", 1, 2));
		_manager.RegisterUpgrader(CreateMockUpgrader("CustomerAggregate", 1, 2));

		// Act
		var aggregateTypes = _manager.GetRegisteredAggregateTypes().ToList();

		// Assert
		aggregateTypes.Count.ShouldBe(2);
		aggregateTypes.ShouldContain("OrderAggregate");
		aggregateTypes.ShouldContain("CustomerAggregate");
	}

	[Fact]
	public void GetRegisteredAggregateTypes_NotDuplicateAggregateTypesWithMultipleUpgraders()
	{
		// Arrange
		_manager.RegisterUpgrader(CreateMockUpgrader("OrderAggregate", 1, 2));
		_manager.RegisterUpgrader(CreateMockUpgrader("OrderAggregate", 2, 3));

		// Act
		var aggregateTypes = _manager.GetRegisteredAggregateTypes().ToList();

		// Assert
		aggregateTypes.Count.ShouldBe(1);
		aggregateTypes.ShouldContain("OrderAggregate");
	}

	#endregion GetRegisteredAggregateTypes Tests

	#region GetUpgradersForAggregateType Tests

	[Fact]
	public void GetUpgradersForAggregateType_ReturnEmpty_WhenAggregateTypeNotRegistered()
	{
		// Act
		var upgraders = _manager.GetUpgradersForAggregateType("UnknownAggregate");

		// Assert
		upgraders.ShouldBeEmpty();
	}

	[Fact]
	public void GetUpgradersForAggregateType_ReturnAllUpgradersForGivenAggregateType()
	{
		// Arrange
		var upgrader1To2 = CreateMockUpgrader("OrderAggregate", 1, 2);
		var upgrader2To3 = CreateMockUpgrader("OrderAggregate", 2, 3);

		_manager.RegisterUpgrader(upgrader1To2);
		_manager.RegisterUpgrader(upgrader2To3);

		// Act
		var upgraders = _manager.GetUpgradersForAggregateType("OrderAggregate").ToList();

		// Assert
		upgraders.Count.ShouldBe(2);
		upgraders.ShouldContain(upgrader1To2);
		upgraders.ShouldContain(upgrader2To3);
	}

	[Fact]
	public void GetUpgradersForAggregateType_NotReturnUpgradersForDifferentAggregateType()
	{
		// Arrange
		_manager.RegisterUpgrader(CreateMockUpgrader("OrderAggregate", 1, 2));
		_manager.RegisterUpgrader(CreateMockUpgrader("CustomerAggregate", 1, 2));

		// Act
		var upgraders = _manager.GetUpgradersForAggregateType("OrderAggregate").ToList();

		// Assert
		upgraders.Count.ShouldBe(1);
	}

	#endregion GetUpgradersForAggregateType Tests

	#region Helper Methods

	private static ISnapshotUpgrader CreateMockUpgrader(
		string aggregateType,
		int fromVersion,
		int toVersion)
	{
		var upgrader = A.Fake<ISnapshotUpgrader>();
		A.CallTo(() => upgrader.AggregateType).Returns(aggregateType);
		A.CallTo(() => upgrader.FromVersion).Returns(fromVersion);
		A.CallTo(() => upgrader.ToVersion).Returns(toVersion);
		return upgrader;
	}

	#endregion Helper Methods
}
