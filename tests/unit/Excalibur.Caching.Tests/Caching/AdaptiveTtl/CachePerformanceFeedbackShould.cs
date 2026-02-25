// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Caching.Tests.AdaptiveTtl;

/// <summary>
/// Unit tests for <see cref="CachePerformanceFeedback"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class CachePerformanceFeedbackShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var feedback = new CachePerformanceFeedback();

		// Assert
		feedback.Key.ShouldBe(string.Empty);
		feedback.IsHit.ShouldBeFalse();
		feedback.ResponseTime.ShouldBe(TimeSpan.Zero);
		feedback.Timestamp.ShouldBe(default);
		feedback.CurrentTtl.ShouldBe(TimeSpan.Zero);
		feedback.WasStale.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var feedback = new CachePerformanceFeedback
		{
			Key = "test-key",
			IsHit = true,
			ResponseTime = TimeSpan.FromMilliseconds(42),
			Timestamp = timestamp,
			CurrentTtl = TimeSpan.FromMinutes(5),
			WasStale = true,
		};

		// Assert
		feedback.Key.ShouldBe("test-key");
		feedback.IsHit.ShouldBeTrue();
		feedback.ResponseTime.ShouldBe(TimeSpan.FromMilliseconds(42));
		feedback.Timestamp.ShouldBe(timestamp);
		feedback.CurrentTtl.ShouldBe(TimeSpan.FromMinutes(5));
		feedback.WasStale.ShouldBeTrue();
	}
}
