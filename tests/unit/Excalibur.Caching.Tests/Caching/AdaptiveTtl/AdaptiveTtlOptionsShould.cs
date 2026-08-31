// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Tests.Caching.AdaptiveTtl;

/// <summary>
/// Unit tests for <see cref="AdaptiveTtlOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
[Trait("Feature", "AdaptiveTtl")]
public sealed class AdaptiveTtlOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveDefaultMinTtl()
	{
		// Act
		var options = new AdaptiveTtlOptions();

		// Assert
		options.MinTtl.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void HaveDefaultMaxTtl()
	{
		// Act
		var options = new AdaptiveTtlOptions();

		// Assert
		options.MaxTtl.ShouldBe(TimeSpan.FromHours(24));
	}

	[Fact]
	public void HaveDefaultLoadThresholds()
	{
		// Act
		var options = new AdaptiveTtlOptions();

		// Assert
		options.Thresholds.HighLoadThreshold.ShouldBe(0.8);
		options.Thresholds.LowLoadThreshold.ShouldBe(0.3);
	}

	[Fact]
	public void AllowCustomConfiguration()
	{
		// Act
		var options = new AdaptiveTtlOptions
		{
			MinTtl = TimeSpan.FromSeconds(10),
			MaxTtl = TimeSpan.FromHours(12),
			Thresholds = { HighLoadThreshold = 0.9, LowLoadThreshold = 0.2 },
		};

		// Assert
		options.MinTtl.ShouldBe(TimeSpan.FromSeconds(10));
		options.MaxTtl.ShouldBe(TimeSpan.FromHours(12));
		options.Thresholds.HighLoadThreshold.ShouldBe(0.9);
		options.Thresholds.LowLoadThreshold.ShouldBe(0.2);
	}

}
