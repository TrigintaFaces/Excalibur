// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Hosting.Tests;

/// <summary>
/// Unit tests for <see cref="MemoryHealthCheckOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "HealthChecks")]
public sealed class MemoryHealthCheckOptionsShould
{
	[Fact]
	public void HaveDefaultAllocatedMemoryThreshold512MB()
	{
		// Act
		var options = new MemoryHealthCheckOptions();

		// Assert — 512 * 1024 = 524,288 KB
		options.AllocatedMemoryThresholdKB.ShouldBe(512 * 1024);
	}

	[Fact]
	public void HaveDefaultWorkingSetThreshold1GB()
	{
		// Act
		var options = new MemoryHealthCheckOptions();

		// Assert — 1 GB in bytes
		options.WorkingSetThresholdBytes.ShouldBe(1L * 1024 * 1024 * 1024);
	}

	[Fact]
	public void AllowCustomAllocatedMemoryThreshold()
	{
		// Arrange
		var options = new MemoryHealthCheckOptions();

		// Act
		options.AllocatedMemoryThresholdKB = 256 * 1024;

		// Assert
		options.AllocatedMemoryThresholdKB.ShouldBe(256 * 1024);
	}

	[Fact]
	public void AllowCustomWorkingSetThreshold()
	{
		// Arrange
		var options = new MemoryHealthCheckOptions();

		// Act
		options.WorkingSetThresholdBytes = 2L * 1024 * 1024 * 1024;

		// Assert
		options.WorkingSetThresholdBytes.ShouldBe(2L * 1024 * 1024 * 1024);
	}
}
