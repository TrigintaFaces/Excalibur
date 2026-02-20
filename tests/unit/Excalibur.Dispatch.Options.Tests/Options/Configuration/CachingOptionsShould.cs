// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Configuration;

namespace Excalibur.Dispatch.Tests.Options.Configuration;

/// <summary>
/// Unit tests for <see cref="CachingOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class CachingOptionsShould
{
	[Fact]
	public void HaveDefaultValues()
	{
		// Arrange & Act
		var options = new CachingOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
		options.DefaultExpiration.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void AllowSettingEnabled()
	{
		// Arrange
		var options = new CachingOptions();

		// Act
		options.Enabled = true;

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingDefaultExpiration()
	{
		// Arrange
		var options = new CachingOptions();
		var newExpiration = TimeSpan.FromHours(1);

		// Act
		options.DefaultExpiration = newExpiration;

		// Assert
		options.DefaultExpiration.ShouldBe(newExpiration);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(60)]
	[InlineData(3600)]
	public void AllowVariousExpirationValues(int seconds)
	{
		// Arrange
		var options = new CachingOptions();

		// Act
		options.DefaultExpiration = TimeSpan.FromSeconds(seconds);

		// Assert
		options.DefaultExpiration.TotalSeconds.ShouldBe(seconds);
	}

	[Fact]
	public void AllowNegativeExpiration()
	{
		// Arrange
		var options = new CachingOptions();

		// Act
		options.DefaultExpiration = TimeSpan.FromSeconds(-1);

		// Assert - No validation at options level, framework handles this
		options.DefaultExpiration.TotalSeconds.ShouldBe(-1);
	}

	[Fact]
	public void SupportConfigurationBinding()
	{
		// Arrange & Act
		var options = new CachingOptions
		{
			Enabled = true,
			DefaultExpiration = TimeSpan.FromMinutes(10)
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.DefaultExpiration.ShouldBe(TimeSpan.FromMinutes(10));
	}
}
