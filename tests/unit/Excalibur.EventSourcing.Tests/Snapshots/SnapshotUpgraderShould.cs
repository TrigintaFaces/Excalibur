// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Snapshots;

namespace Excalibur.EventSourcing.Tests.Snapshots;

/// <summary>
/// Unit tests for <see cref="SnapshotUpgrader{TFrom, TTo}"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SnapshotUpgraderShould
{
	#region Test Snapshot Types

	private sealed class SnapshotV1
	{
		public string OldField { get; set; } = string.Empty;
	}

	private sealed class SnapshotV2
	{
		public string NewField { get; set; } = string.Empty;
		public string AdditionalField { get; set; } = string.Empty;
	}

	#endregion Test Snapshot Types

	#region Test Upgrader

	private sealed class TestV1ToV2SnapshotUpgrader : SnapshotUpgrader<SnapshotV1, SnapshotV2>
	{
		public TestV1ToV2SnapshotUpgrader(ISnapshotDataSerializer serializer)
			: base(serializer)
		{
		}

		public override string AggregateType => "OrderAggregate";
		public override int FromVersion => 1;
		public override int ToVersion => 2;

		protected override SnapshotV2 UpgradeSnapshot(SnapshotV1 oldSnapshot)
			=> new() { NewField = oldSnapshot.OldField, AdditionalField = "DefaultValue" };
	}

	#endregion Test Upgrader

	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowArgumentNullException_WhenSerializerIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new TestV1ToV2SnapshotUpgrader(null!));
	}

	#endregion Constructor Tests

	#region Properties Tests

	[Fact]
	public void AggregateType_ReturnConfiguredValue()
	{
		// Arrange
		var serializer = A.Fake<ISnapshotDataSerializer>();
		var upgrader = new TestV1ToV2SnapshotUpgrader(serializer);

		// Act & Assert
		upgrader.AggregateType.ShouldBe("OrderAggregate");
	}

	[Fact]
	public void FromVersion_ReturnConfiguredValue()
	{
		// Arrange
		var serializer = A.Fake<ISnapshotDataSerializer>();
		var upgrader = new TestV1ToV2SnapshotUpgrader(serializer);

		// Act & Assert
		upgrader.FromVersion.ShouldBe(1);
	}

	[Fact]
	public void ToVersion_ReturnConfiguredValue()
	{
		// Arrange
		var serializer = A.Fake<ISnapshotDataSerializer>();
		var upgrader = new TestV1ToV2SnapshotUpgrader(serializer);

		// Act & Assert
		upgrader.ToVersion.ShouldBe(2);
	}

	#endregion Properties Tests

	#region CanUpgrade Tests

	[Fact]
	public void CanUpgrade_ReturnTrue_WhenAggregateTypeAndVersionMatch()
	{
		// Arrange
		var serializer = A.Fake<ISnapshotDataSerializer>();
		var upgrader = new TestV1ToV2SnapshotUpgrader(serializer);

		// Act
		var result = upgrader.CanUpgrade("OrderAggregate", 1);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void CanUpgrade_ReturnFalse_WhenAggregateTypeDoesNotMatch()
	{
		// Arrange
		var serializer = A.Fake<ISnapshotDataSerializer>();
		var upgrader = new TestV1ToV2SnapshotUpgrader(serializer);

		// Act
		var result = upgrader.CanUpgrade("DifferentAggregate", 1);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void CanUpgrade_ReturnFalse_WhenVersionDoesNotMatch()
	{
		// Arrange
		var serializer = A.Fake<ISnapshotDataSerializer>();
		var upgrader = new TestV1ToV2SnapshotUpgrader(serializer);

		// Act
		var result = upgrader.CanUpgrade("OrderAggregate", 2);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void CanUpgrade_ReturnFalse_WhenBothAggregateTypeAndVersionDoNotMatch()
	{
		// Arrange
		var serializer = A.Fake<ISnapshotDataSerializer>();
		var upgrader = new TestV1ToV2SnapshotUpgrader(serializer);

		// Act
		var result = upgrader.CanUpgrade("OtherAggregate", 5);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void CanUpgrade_ReturnFalse_WhenAggregateTypeIsNull()
	{
		// Arrange
		var serializer = A.Fake<ISnapshotDataSerializer>();
		var upgrader = new TestV1ToV2SnapshotUpgrader(serializer);

		// Act
		var result = upgrader.CanUpgrade(null!, 1);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void CanUpgrade_IsCaseSensitive()
	{
		// Arrange
		var serializer = A.Fake<ISnapshotDataSerializer>();
		var upgrader = new TestV1ToV2SnapshotUpgrader(serializer);

		// Act
		var result = upgrader.CanUpgrade("orderaggregate", 1); // lowercase

		// Assert
		result.ShouldBeFalse();
	}

	#endregion CanUpgrade Tests

	#region Upgrade Tests

	[Fact]
	public void Upgrade_DeserializeOldDataCallUpgradeSnapshotAndSerializeNewData()
	{
		// Arrange
		var serializer = A.Fake<ISnapshotDataSerializer>();
		var upgrader = new TestV1ToV2SnapshotUpgrader(serializer);

		var oldData = new byte[] { 1, 2, 3 };
		var oldSnapshot = new SnapshotV1 { OldField = "OriginalValue" };
		var expectedSerializedResult = new byte[] { 4, 5, 6 };

		A.CallTo(() => serializer.Deserialize<SnapshotV1>(oldData)).Returns(oldSnapshot);
		A.CallTo(() => serializer.Serialize(A<SnapshotV2>.That.Matches(s =>
			s.NewField == "OriginalValue" && s.AdditionalField == "DefaultValue")))
			.Returns(expectedSerializedResult);

		// Act
		var result = upgrader.Upgrade(oldData);

		// Assert
		result.ShouldBe(expectedSerializedResult);
		A.CallTo(() => serializer.Deserialize<SnapshotV1>(oldData)).MustHaveHappenedOnceExactly();
		A.CallTo(() => serializer.Serialize(A<SnapshotV2>.That.Matches(s =>
			s.NewField == "OriginalValue" && s.AdditionalField == "DefaultValue")))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void Upgrade_ThrowInvalidOperationException_WhenDeserializerReturnsNull()
	{
		// Arrange
		var serializer = A.Fake<ISnapshotDataSerializer>();
		var upgrader = new TestV1ToV2SnapshotUpgrader(serializer);

		var oldData = new byte[] { 1, 2, 3 };
		A.CallTo(() => serializer.Deserialize<SnapshotV1>(oldData)).Returns(null);

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => upgrader.Upgrade(oldData));
		exception.Message.ShouldContain("SnapshotV1");
	}

	[Fact]
	public void Upgrade_ThrowArgumentNullException_WhenOldSnapshotDataIsNull()
	{
		// Arrange
		var serializer = A.Fake<ISnapshotDataSerializer>();
		var upgrader = new TestV1ToV2SnapshotUpgrader(serializer);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => upgrader.Upgrade(null!));
	}

	[Fact]
	public void Upgrade_PreserveDataDuringTransformation()
	{
		// Arrange
		var serializer = A.Fake<ISnapshotDataSerializer>();
		var upgrader = new TestV1ToV2SnapshotUpgrader(serializer);

		var oldData = new byte[] { 10, 20, 30 };
		var oldSnapshot = new SnapshotV1 { OldField = "PreservedValue" };
		var serializedOutput = new byte[] { 40, 50, 60 };

		A.CallTo(() => serializer.Deserialize<SnapshotV1>(oldData)).Returns(oldSnapshot);
		A.CallTo(() => serializer.Serialize(A<SnapshotV2>.Ignored)).Returns(serializedOutput);

		// Act
		var result = upgrader.Upgrade(oldData);

		// Assert
		result.ShouldBe(serializedOutput);

		// Verify the UpgradeSnapshot method received the correct old snapshot
		// by checking the serialized output was called with the transformed data
		A.CallTo(() => serializer.Serialize(A<SnapshotV2>.That.Matches(s =>
			s.NewField == "PreservedValue")))
			.MustHaveHappenedOnceExactly();
	}

	#endregion Upgrade Tests
}
