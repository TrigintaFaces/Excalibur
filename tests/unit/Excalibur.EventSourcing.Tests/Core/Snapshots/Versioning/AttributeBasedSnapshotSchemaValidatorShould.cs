// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Snapshots.Versioning;

namespace Excalibur.EventSourcing.Tests.Core.Snapshots.Versioning;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class AttributeBasedSnapshotSchemaValidatorShould
{
	private readonly AttributeBasedSnapshotSchemaValidator _sut = new();

	[Fact]
	public async Task ReturnCompatible_WhenStoredVersionMatchesCurrent()
	{
		// Arrange
		_sut.Register("OrderAggregate", typeof(OrderSnapshotV2));

		// Act
		var result = await _sut.ValidateAsync("OrderAggregate", 2, CancellationToken.None);

		// Assert
		result.IsCompatible.ShouldBeTrue();
		result.StoredVersion.ShouldBe(2);
		result.CurrentVersion.ShouldBe(2);
	}

	[Fact]
	public async Task ReturnIncompatible_WhenStoredVersionIsOlder()
	{
		// Arrange
		_sut.Register("OrderAggregate", typeof(OrderSnapshotV2));

		// Act
		var result = await _sut.ValidateAsync("OrderAggregate", 1, CancellationToken.None);

		// Assert
		result.IsCompatible.ShouldBeFalse();
		result.StoredVersion.ShouldBe(1);
		result.CurrentVersion.ShouldBe(2);
		result.Reason.ShouldContain("older");
	}

	[Fact]
	public async Task ReturnIncompatible_WhenStoredVersionIsNewer()
	{
		// Arrange
		_sut.Register("OrderAggregate", typeof(OrderSnapshotV2));

		// Act
		var result = await _sut.ValidateAsync("OrderAggregate", 5, CancellationToken.None);

		// Assert
		result.IsCompatible.ShouldBeFalse();
		result.StoredVersion.ShouldBe(5);
		result.CurrentVersion.ShouldBe(2);
		result.Reason.ShouldContain("newer");
	}

	[Fact]
	public async Task ReturnCompatible_WhenAggregateTypeNotRegistered()
	{
		// Act
		var result = await _sut.ValidateAsync("UnknownAggregate", 3, CancellationToken.None);

		// Assert
		result.IsCompatible.ShouldBeTrue();
		result.StoredVersion.ShouldBe(3);
	}

	[Fact]
	public void GetCurrentVersion_ReturnVersion_WhenRegistered()
	{
		// Arrange
		_sut.Register("OrderAggregate", typeof(OrderSnapshotV2));

		// Act
		var version = _sut.GetCurrentVersion("OrderAggregate");

		// Assert
		version.ShouldBe(2);
	}

	[Fact]
	public void GetCurrentVersion_ReturnNull_WhenNotRegistered()
	{
		var version = _sut.GetCurrentVersion("Unknown");
		version.ShouldBeNull();
	}

	[Fact]
	public void ThrowOnRegisterTypeWithoutAttribute()
	{
		Should.Throw<ArgumentException>(
			() => _sut.Register("Test", typeof(SnapshotWithoutAttribute)));
	}

	[Fact]
	public void ThrowOnNullOrEmptyArgs()
	{
		Should.Throw<ArgumentException>(() => _sut.Register(null!, typeof(OrderSnapshotV2)));
		Should.Throw<ArgumentException>(() => _sut.Register("", typeof(OrderSnapshotV2)));
		Should.Throw<ArgumentNullException>(() => _sut.Register("Test", null!));
		Should.Throw<ArgumentException>(() => _sut.GetCurrentVersion(null!));
	}

	[SnapshotSchemaVersion(2)]
	private sealed class OrderSnapshotV2;

	private sealed class SnapshotWithoutAttribute;
}
