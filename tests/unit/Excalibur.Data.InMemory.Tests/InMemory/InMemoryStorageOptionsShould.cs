// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.InMemory;

namespace Excalibur.Data.Tests.InMemory;

/// <summary>
/// Unit tests for <see cref="InMemoryStorageOptions"/>.
/// Verifies defaults, validation logic, and PersistToDisk constraint.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class InMemoryStorageOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveExpectedDefaults()
	{
		// Arrange & Act
		var options = new InMemoryStorageOptions();

		// Assert
		options.EnableMetrics.ShouldBeFalse();
		options.PersistToDisk.ShouldBeFalse();
		options.PersistenceFilePath.ShouldBeNull();
	}

	[Fact]
	public void Validate_WithDefaults_DoesNotThrow()
	{
		// Arrange
		var options = new InMemoryStorageOptions();

		// Act & Assert
		Should.NotThrow(options.Validate);
	}

	[Fact]
	public void Validate_PersistToDiskWithoutPath_ThrowsArgumentException()
	{
		// Arrange
		var options = new InMemoryStorageOptions
		{
			PersistToDisk = true,
			PersistenceFilePath = null
		};

		// Act & Assert
		Should.Throw<ArgumentException>(options.Validate)
			.Message.ShouldContain("PersistenceFilePath");
	}

	[Fact]
	public void Validate_PersistToDiskWithEmptyPath_ThrowsArgumentException()
	{
		// Arrange
		var options = new InMemoryStorageOptions
		{
			PersistToDisk = true,
			PersistenceFilePath = ""
		};

		// Act & Assert
		Should.Throw<ArgumentException>(options.Validate);
	}

	[Fact]
	public void Validate_PersistToDiskWithPath_DoesNotThrow()
	{
		// Arrange
		var options = new InMemoryStorageOptions
		{
			PersistToDisk = true,
			PersistenceFilePath = "/tmp/data.json"
		};

		// Act & Assert
		Should.NotThrow(options.Validate);
	}

	[Fact]
	public void AllowEnablingMetrics()
	{
		// Arrange & Act
		var options = new InMemoryStorageOptions { EnableMetrics = true };

		// Assert
		options.EnableMetrics.ShouldBeTrue();
	}

}
