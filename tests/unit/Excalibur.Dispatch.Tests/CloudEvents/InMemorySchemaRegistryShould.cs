// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.CloudEvents;

namespace Excalibur.Dispatch.Tests.CloudEvents;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemorySchemaRegistryShould
{
	[Fact]
	public async Task GetSchemaAsync_WhenNotRegistered_ReturnsNull()
	{
		// Arrange
		var registry = new InMemorySchemaRegistry();

		// Act
		var schema = await registry.GetSchemaAsync("event.type", "1.0", CancellationToken.None);

		// Assert
		schema.ShouldBeNull();
	}

	[Fact]
	public async Task GetVersionsAsync_WhenNotRegistered_ReturnsEmpty()
	{
		// Arrange
		var registry = new InMemorySchemaRegistry();

		// Act
		var versions = await registry.GetVersionsAsync("event.type", CancellationToken.None);

		// Assert
		versions.ShouldNotBeNull();
		versions.Count.ShouldBe(0);
	}

	// --- IsCompatible ---

	[Theory]
	[InlineData("1.0", "1.2", SchemaCompatibilityMode.Forward, true)]
	[InlineData("1.2", "1.0", SchemaCompatibilityMode.Forward, false)]
	[InlineData("1.2", "1.0", SchemaCompatibilityMode.Backward, true)]
	[InlineData("1.0", "1.2", SchemaCompatibilityMode.Backward, false)]
	[InlineData("1.0", "1.5", SchemaCompatibilityMode.Full, true)]
	[InlineData("1.0", "2.0", SchemaCompatibilityMode.Full, false)]
	[InlineData("1.0", "1.0", SchemaCompatibilityMode.None, false)]
	public void IsCompatible_ReturnsExpectedResult(
		string fromVersion, string toVersion, SchemaCompatibilityMode mode, bool expected)
	{
		// Arrange
		var registry = new InMemorySchemaRegistry();

		// Act
		var result = registry.IsCompatible("event.type", fromVersion, toVersion, mode);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void IsCompatible_WithInvalidVersions_ReturnsFalse()
	{
		// Arrange
		var registry = new InMemorySchemaRegistry();

		// Act & Assert
		registry.IsCompatible("event.type", "invalid", "1.0", SchemaCompatibilityMode.Full).ShouldBeFalse();
		registry.IsCompatible("event.type", "1.0", "invalid", SchemaCompatibilityMode.Full).ShouldBeFalse();
	}

	[Fact]
	public void IsCompatible_SameMajorDifferentMinor_ForwardIsTrue()
	{
		// Arrange
		var registry = new InMemorySchemaRegistry();

		// Act
		var result = registry.IsCompatible("type", "1.0", "1.3", SchemaCompatibilityMode.Forward);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsCompatible_DifferentMajor_ForwardIsFalse()
	{
		// Arrange
		var registry = new InMemorySchemaRegistry();

		// Act
		var result = registry.IsCompatible("type", "1.0", "2.0", SchemaCompatibilityMode.Forward);

		// Assert
		result.ShouldBeFalse();
	}
}
