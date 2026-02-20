// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Views;

namespace Excalibur.EventSourcing.Tests.Core.Views;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class MaterializedViewOptionsValidatorShould
{
	private readonly MaterializedViewOptionsValidator _sut = new();

	[Fact]
	public void SucceedWithDefaultOptions()
	{
		var result = _sut.Validate(null, new MaterializedViewOptions());
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void SucceedWithValidBatchSize()
	{
		var result = _sut.Validate(null, new MaterializedViewOptions { BatchSize = 500 });
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void FailWhenBatchSizeIsZero()
	{
		var result = _sut.Validate(null, new MaterializedViewOptions { BatchSize = 0 });
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("BatchSize");
	}

	[Fact]
	public void FailWhenBatchSizeExceedsMaximum()
	{
		var result = _sut.Validate(null, new MaterializedViewOptions { BatchSize = 10001 });
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void FailWhenBatchDelayIsNegative()
	{
		var result = _sut.Validate(null, new MaterializedViewOptions { BatchDelay = TimeSpan.FromSeconds(-1) });
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("BatchDelay");
	}

	[Fact]
	public void SucceedWithZeroBatchDelay()
	{
		var result = _sut.Validate(null, new MaterializedViewOptions { BatchDelay = TimeSpan.Zero });
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() => _sut.Validate(null, null!));
	}

	[Fact]
	public void HaveDefaultPropertyValues()
	{
		var options = new MaterializedViewOptions();
		options.CatchUpOnStartup.ShouldBeFalse();
		options.BatchSize.ShouldBe(100);
		options.BatchDelay.ShouldBe(TimeSpan.FromMilliseconds(10));
	}
}
