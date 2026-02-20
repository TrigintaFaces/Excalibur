// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Snapshots.Upgrading;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Core.Snapshots;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class SnapshotUpgraderRegistryShould
{
	private readonly SnapshotUpgraderRegistry _sut;
	private readonly ISnapshotDataSerializer _serializer;

	public SnapshotUpgraderRegistryShould()
	{
		_sut = new SnapshotUpgraderRegistry(NullLogger<SnapshotUpgraderRegistry>.Instance);
		_serializer = A.Fake<ISnapshotDataSerializer>();

		// Default serializer: string round-trip via UTF8
		A.CallTo(() => _serializer.Serialize(A<string>._))
			.ReturnsLazily((string s) => Encoding.UTF8.GetBytes(s ?? ""));
		A.CallTo(() => _serializer.Deserialize<string>(A<byte[]>._))
			.ReturnsLazily((byte[] b) => Encoding.UTF8.GetString(b));
	}

	[Fact]
	public void Register_AddUpgraderSuccessfully()
	{
		// Arrange
		var upgrader = CreateUpgrader("Order", 1, 2);

		// Act & Assert (should not throw)
		_sut.Register(upgrader, _serializer);
	}

	[Fact]
	public void Register_ThrowOnDuplicateRegistration()
	{
		// Arrange
		var upgrader1 = CreateUpgrader("Order", 1, 2);
		var upgrader2 = CreateUpgrader("Order", 1, 2);

		_sut.Register(upgrader1, _serializer);

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => _sut.Register(upgrader2, _serializer));
	}

	[Fact]
	public void Register_ThrowOnNullArgs()
	{
		var upgrader = CreateUpgrader("Order", 1, 2);

		Should.Throw<ArgumentNullException>(() => _sut.Register<string, string>(null!, _serializer));
		Should.Throw<ArgumentNullException>(() => _sut.Register(upgrader, null!));
	}

	[Fact]
	public void CanUpgrade_ReturnTrue_WhenDirectPathExists()
	{
		// Arrange
		_sut.Register(CreateUpgrader("Order", 1, 2), _serializer);

		// Act & Assert
		_sut.CanUpgrade("Order", 1, 2).ShouldBeTrue();
	}

	[Fact]
	public void CanUpgrade_ReturnTrue_WhenSameVersion()
	{
		_sut.CanUpgrade("Order", 5, 5).ShouldBeTrue();
	}

	[Fact]
	public void CanUpgrade_ReturnFalse_WhenNoUpgradersRegistered()
	{
		_sut.CanUpgrade("Order", 1, 2).ShouldBeFalse();
	}

	[Fact]
	public void CanUpgrade_ReturnTrue_WhenMultiStepPathExists()
	{
		// Arrange: v1 -> v2 -> v3
		_sut.Register(CreateUpgrader("Order", 1, 2), _serializer);
		_sut.Register(CreateUpgrader("Order", 2, 3), _serializer);

		// Act & Assert
		_sut.CanUpgrade("Order", 1, 3).ShouldBeTrue();
	}

	[Fact]
	public void CanUpgrade_ReturnFalse_WhenNoPathExists()
	{
		// Arrange: v1 -> v2 (no path to v4)
		_sut.Register(CreateUpgrader("Order", 1, 2), _serializer);

		// Act & Assert
		_sut.CanUpgrade("Order", 1, 4).ShouldBeFalse();
	}

	[Fact]
	public void Upgrade_ReturnSameData_WhenSameVersion()
	{
		// Arrange
		var data = new byte[] { 1, 2, 3 };

		// Act
		var result = _sut.Upgrade("Order", data, 5, 5);

		// Assert
		result.ShouldBeSameAs(data);
	}

	[Fact]
	public void Upgrade_ThrowOnNoUpgradersRegistered()
	{
		Should.Throw<InvalidOperationException>(
			() => _sut.Upgrade("Order", [1, 2], 1, 2));
	}

	[Fact]
	public void Upgrade_ThrowOnNoPathFound()
	{
		// Arrange
		_sut.Register(CreateUpgrader("Order", 1, 2), _serializer);

		// Act & Assert
		Should.Throw<InvalidOperationException>(
			() => _sut.Upgrade("Order", [1, 2], 1, 5));
	}

	[Fact]
	public void Upgrade_ApplyChainOfUpgraders()
	{
		// Arrange: v1 -> v2 -> v3 using real transform
		var v1Data = Encoding.UTF8.GetBytes("v1");

		var upgrader12 = CreateUpgraderWithTransform("Order", 1, 2, "v2");
		var upgrader23 = CreateUpgraderWithTransform("Order", 2, 3, "v3");

		_sut.Register(upgrader12, _serializer);
		_sut.Register(upgrader23, _serializer);

		// Act
		var result = _sut.Upgrade("Order", v1Data, 1, 3);

		// Assert
		Encoding.UTF8.GetString(result).ShouldBe("v3");
	}

	[Fact]
	public void Upgrade_ThrowOnNullArgs()
	{
		Should.Throw<ArgumentException>(() => _sut.Upgrade(null!, [1], 1, 2));
		Should.Throw<ArgumentException>(() => _sut.Upgrade("", [1], 1, 2));
		Should.Throw<ArgumentNullException>(() => _sut.Upgrade("Order", null!, 1, 2));
	}

	private static ISnapshotUpgrader<string, string> CreateUpgrader(string aggregateType, int from, int to)
	{
		var upgrader = A.Fake<ISnapshotUpgrader<string, string>>();
		A.CallTo(() => upgrader.AggregateType).Returns(aggregateType);
		A.CallTo(() => upgrader.FromVersion).Returns(from);
		A.CallTo(() => upgrader.ToVersion).Returns(to);
		A.CallTo(() => upgrader.Upgrade(A<string>._)).ReturnsLazily((string s) => s);
		return upgrader;
	}

	private static ISnapshotUpgrader<string, string> CreateUpgraderWithTransform(
		string aggregateType, int from, int to, string outputValue)
	{
		var upgrader = A.Fake<ISnapshotUpgrader<string, string>>();
		A.CallTo(() => upgrader.AggregateType).Returns(aggregateType);
		A.CallTo(() => upgrader.FromVersion).Returns(from);
		A.CallTo(() => upgrader.ToVersion).Returns(to);
		A.CallTo(() => upgrader.Upgrade(A<string>._)).Returns(outputValue);
		return upgrader;
	}
}
