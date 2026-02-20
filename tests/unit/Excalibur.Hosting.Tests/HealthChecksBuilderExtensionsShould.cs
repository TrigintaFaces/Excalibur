// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Hosting.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class HealthChecksBuilderExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddMemoryHealthChecksWithDefaults()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();

		// Act
		var result = builder.AddMemoryHealthChecks();

		// Assert
		result.ShouldNotBeNull();
		services.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void AddMemoryHealthChecksWithCustomOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();
		var configured = false;

		// Act
		var result = builder.AddMemoryHealthChecks(options =>
		{
			options.AllocatedMemoryThresholdKB = 2048;
			options.WorkingSetThresholdBytes = 1024L * 1024 * 1024;
			configured = true;
		});

		// Assert
		result.ShouldNotBeNull();
		configured.ShouldBeTrue();
	}

	[Fact]
	public void ThrowWhenHealthChecksBuilderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IHealthChecksBuilder)null!).AddMemoryHealthChecks());
	}

	[Fact]
	public void ThrowWhenHealthChecksBuilderIsNullWithConfigure()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IHealthChecksBuilder)null!).AddMemoryHealthChecks(_ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.AddMemoryHealthChecks(null!));
	}
}
