// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.CloudEvents;

namespace Excalibur.Dispatch.Tests.CloudEvents;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class InMemorySchemaRegistryShould
{
	private static readonly string[] ExpectedVersions = ["1.0", "2.0"];

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

	// --- Registration round-trip ---

	[Fact]
	public async Task RegisterSchemaAsync_ThenGetSchemaAsync_ReturnsRegisteredSchema()
	{
		var registry = new InMemorySchemaRegistry();
		var ct = TestContext.Current.CancellationToken;

		await registry.RegisterSchemaAsync("event.type", "1.0", "{\"type\":\"object\"}", ct);

		(await registry.GetSchemaAsync("event.type", "1.0", ct)).ShouldBe("{\"type\":\"object\"}");
	}

	[Fact]
	public async Task RegisterSchemaAsync_TracksVersions_PerEventType()
	{
		var registry = new InMemorySchemaRegistry();
		var ct = TestContext.Current.CancellationToken;

		await registry.RegisterSchemaAsync("event.type", "1.0", "s1", ct);
		await registry.RegisterSchemaAsync("event.type", "2.0", "s2", ct);
		await registry.RegisterSchemaAsync("event.type", "1.0", "s1-again", ct); // idempotent version tracking

		var versions = await registry.GetVersionsAsync("event.type", ct);

		versions.ShouldBe(ExpectedVersions, ignoreOrder: true);
	}

	[Fact]
	public async Task RegisterSchemaAsync_WithNullOrEmptyArguments_Throws()
	{
		var registry = new InMemorySchemaRegistry();
		var ct = TestContext.Current.CancellationToken;

		await Should.ThrowAsync<ArgumentException>(() => registry.RegisterSchemaAsync("", "1.0", "s", ct));
		await Should.ThrowAsync<ArgumentException>(() => registry.RegisterSchemaAsync("t", "", "s", ct));
		await Should.ThrowAsync<ArgumentNullException>(() => registry.RegisterSchemaAsync("t", "1.0", null!, ct));
	}
}
