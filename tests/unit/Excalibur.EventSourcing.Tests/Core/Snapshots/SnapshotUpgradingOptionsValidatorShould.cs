// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Snapshots;

namespace Excalibur.EventSourcing.Tests.Core.Snapshots;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class SnapshotUpgradingOptionsValidatorShould
{
	private readonly SnapshotUpgradingOptionsValidator _sut = new();

	[Fact]
	public void SucceedWithDefaultOptions()
	{
		// Arrange
		var options = new SnapshotUpgradingOptions();

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void SucceedWithValidVersion()
	{
		// Arrange
		var options = new SnapshotUpgradingOptions { CurrentSnapshotVersion = 5 };

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void FailWhenVersionIsZero()
	{
		// Arrange
		var options = new SnapshotUpgradingOptions { CurrentSnapshotVersion = 0 };

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("CurrentSnapshotVersion");
	}

	[Fact]
	public void FailWhenVersionIsNegative()
	{
		// Arrange
		var options = new SnapshotUpgradingOptions { CurrentSnapshotVersion = -1 };

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() => _sut.Validate(null, null!));
	}

	[Fact]
	public void HaveDefaultValues()
	{
		// Arrange
		var options = new SnapshotUpgradingOptions();

		// Assert
		options.EnableAutoUpgradeOnLoad.ShouldBeTrue();
		options.CurrentSnapshotVersion.ShouldBe(1);
	}
}
