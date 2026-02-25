// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Views;

namespace Excalibur.EventSourcing.Tests.Core.Views;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MaterializedViewOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Act
		var options = new MaterializedViewOptions();

		// Assert
		options.CatchUpOnStartup.ShouldBeFalse();
		options.BatchSize.ShouldBe(100);
		options.BatchDelay.ShouldBe(TimeSpan.FromMilliseconds(10));
	}

	[Fact]
	public void AllowSettingCatchUpOnStartup()
	{
		// Act
		var options = new MaterializedViewOptions { CatchUpOnStartup = true };

		// Assert
		options.CatchUpOnStartup.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingBatchSize()
	{
		// Act
		var options = new MaterializedViewOptions { BatchSize = 500 };

		// Assert
		options.BatchSize.ShouldBe(500);
	}

	[Fact]
	public void AllowSettingBatchDelay()
	{
		// Act
		var options = new MaterializedViewOptions { BatchDelay = TimeSpan.FromSeconds(1) };

		// Assert
		options.BatchDelay.ShouldBe(TimeSpan.FromSeconds(1));
	}
}
