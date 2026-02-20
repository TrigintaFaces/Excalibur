// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Tests.Caching.AdaptiveTtl;

/// <summary>
/// Unit tests for <see cref="AdaptiveTtlContext"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
[Trait("Feature", "AdaptiveTtl")]
public sealed class AdaptiveTtlContextShould : UnitTestBase
{
	[Fact]
	public void HaveDefaultValues()
	{
		// Act
		var context = new AdaptiveTtlContext();

		// Assert
		context.Key.ShouldBe(string.Empty);
		context.BaseTtl.ShouldBe(default);
		context.AccessFrequency.ShouldBe(0);
		context.HitRate.ShouldBe(0);
		context.LastUpdate.ShouldBe(default);
		context.ContentSize.ShouldBe(0);
		context.MissCost.ShouldBe(default);
		context.SystemLoad.ShouldBe(0);
		context.CurrentTime.ShouldBe(default);
		_ = context.Metadata.ShouldNotBeNull();
		context.Metadata.Count.ShouldBe(0);
	}

	[Fact]
	public void AllowSettingKey()
	{
		// Act
		var context = new AdaptiveTtlContext
		{
			Key = "cache:user:123"
		};

		// Assert
		context.Key.ShouldBe("cache:user:123");
	}

	[Fact]
	public void AllowSettingBaseTtl()
	{
		// Act
		var context = new AdaptiveTtlContext
		{
			BaseTtl = TimeSpan.FromMinutes(5)
		};

		// Assert
		context.BaseTtl.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void AllowSettingAccessFrequency()
	{
		// Act
		var context = new AdaptiveTtlContext
		{
			AccessFrequency = 150.5
		};

		// Assert
		context.AccessFrequency.ShouldBe(150.5);
	}

	[Fact]
	public void AllowSettingHitRate()
	{
		// Act
		var context = new AdaptiveTtlContext
		{
			HitRate = 0.85
		};

		// Assert
		context.HitRate.ShouldBe(0.85);
	}

	[Fact]
	public void AllowSettingLastUpdate()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var context = new AdaptiveTtlContext
		{
			LastUpdate = timestamp
		};

		// Assert
		context.LastUpdate.ShouldBe(timestamp);
	}

	[Fact]
	public void AllowSettingContentSize()
	{
		// Act
		var context = new AdaptiveTtlContext
		{
			ContentSize = 1024 * 1024 // 1 MB
		};

		// Assert
		context.ContentSize.ShouldBe(1024 * 1024);
	}

	[Fact]
	public void AllowSettingMissCost()
	{
		// Act
		var context = new AdaptiveTtlContext
		{
			MissCost = TimeSpan.FromMilliseconds(200)
		};

		// Assert
		context.MissCost.ShouldBe(TimeSpan.FromMilliseconds(200));
	}

	[Fact]
	public void AllowSettingSystemLoad()
	{
		// Act
		var context = new AdaptiveTtlContext
		{
			SystemLoad = 0.75
		};

		// Assert
		context.SystemLoad.ShouldBe(0.75);
	}

	[Fact]
	public void AllowSettingCurrentTime()
	{
		// Arrange
		var currentTime = DateTimeOffset.UtcNow;

		// Act
		var context = new AdaptiveTtlContext
		{
			CurrentTime = currentTime
		};

		// Assert
		context.CurrentTime.ShouldBe(currentTime);
	}

	[Fact]
	public void AllowAddingMetadata()
	{
		// Arrange
		var context = new AdaptiveTtlContext();

		// Act
		context.Metadata["custom-key"] = "custom-value";
		context.Metadata["numeric-key"] = 42;

		// Assert
		context.Metadata.Count.ShouldBe(2);
		context.Metadata["custom-key"].ShouldBe("custom-value");
		context.Metadata["numeric-key"].ShouldBe(42);
	}

	[Fact]
	public void CreateFullyConfiguredContext()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var context = new AdaptiveTtlContext
		{
			Key = "cache:products:all",
			BaseTtl = TimeSpan.FromMinutes(10),
			AccessFrequency = 250,
			HitRate = 0.92,
			LastUpdate = now.AddMinutes(-5),
			ContentSize = 5 * 1024 * 1024, // 5 MB
			MissCost = TimeSpan.FromMilliseconds(500),
			SystemLoad = 0.65,
			CurrentTime = now
		};

		// Assert
		context.Key.ShouldBe("cache:products:all");
		context.BaseTtl.ShouldBe(TimeSpan.FromMinutes(10));
		context.AccessFrequency.ShouldBe(250);
		context.HitRate.ShouldBe(0.92);
		context.LastUpdate.ShouldBe(now.AddMinutes(-5));
		context.ContentSize.ShouldBe(5 * 1024 * 1024);
		context.MissCost.ShouldBe(TimeSpan.FromMilliseconds(500));
		context.SystemLoad.ShouldBe(0.65);
		context.CurrentTime.ShouldBe(now);
	}
}
