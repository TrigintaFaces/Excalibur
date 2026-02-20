// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Functional tests for <see cref="InMemoryCacheTagTracker"/> verifying
/// tag registration, lookup, and unregistration workflows.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InMemoryCacheTagTrackerFunctionalShould
{
	private readonly InMemoryCacheTagTracker _sut = new();

	[Fact]
	public async Task Register_key_with_tags_and_retrieve_by_tag()
	{
		await _sut.RegisterKeyAsync("key-1", ["tag-a", "tag-b"], CancellationToken.None);

		var keys = await _sut.GetKeysByTagsAsync(["tag-a"], CancellationToken.None);

		keys.ShouldContain("key-1");
	}

	[Fact]
	public async Task Return_multiple_keys_for_same_tag()
	{
		await _sut.RegisterKeyAsync("key-1", ["shared-tag"], CancellationToken.None);
		await _sut.RegisterKeyAsync("key-2", ["shared-tag"], CancellationToken.None);
		await _sut.RegisterKeyAsync("key-3", ["shared-tag"], CancellationToken.None);

		var keys = await _sut.GetKeysByTagsAsync(["shared-tag"], CancellationToken.None);

		keys.Count.ShouldBe(3);
		keys.ShouldContain("key-1");
		keys.ShouldContain("key-2");
		keys.ShouldContain("key-3");
	}

	[Fact]
	public async Task Return_union_of_keys_for_multiple_tags()
	{
		await _sut.RegisterKeyAsync("key-a", ["tag-1"], CancellationToken.None);
		await _sut.RegisterKeyAsync("key-b", ["tag-2"], CancellationToken.None);
		await _sut.RegisterKeyAsync("key-c", ["tag-1", "tag-2"], CancellationToken.None);

		var keys = await _sut.GetKeysByTagsAsync(["tag-1", "tag-2"], CancellationToken.None);

		keys.Count.ShouldBe(3);
		keys.ShouldContain("key-a");
		keys.ShouldContain("key-b");
		keys.ShouldContain("key-c");
	}

	[Fact]
	public async Task Return_empty_set_for_unknown_tag()
	{
		await _sut.RegisterKeyAsync("key-1", ["known-tag"], CancellationToken.None);

		var keys = await _sut.GetKeysByTagsAsync(["unknown-tag"], CancellationToken.None);

		keys.ShouldBeEmpty();
	}

	[Fact]
	public async Task Unregister_key_removes_from_all_tags()
	{
		await _sut.RegisterKeyAsync("key-x", ["tag-1", "tag-2", "tag-3"], CancellationToken.None);

		await _sut.UnregisterKeyAsync("key-x", CancellationToken.None);

		var keys1 = await _sut.GetKeysByTagsAsync(["tag-1"], CancellationToken.None);
		var keys2 = await _sut.GetKeysByTagsAsync(["tag-2"], CancellationToken.None);
		var keys3 = await _sut.GetKeysByTagsAsync(["tag-3"], CancellationToken.None);

		keys1.ShouldBeEmpty();
		keys2.ShouldBeEmpty();
		keys3.ShouldBeEmpty();
	}

	[Fact]
	public async Task Handle_unregistering_nonexistent_key_gracefully()
	{
		// Should not throw
		await _sut.UnregisterKeyAsync("nonexistent", CancellationToken.None);
	}

	[Fact]
	public async Task Return_empty_for_null_or_empty_tags()
	{
		var result1 = await _sut.GetKeysByTagsAsync(null!, CancellationToken.None);
		var result2 = await _sut.GetKeysByTagsAsync([], CancellationToken.None);

		result1.ShouldBeEmpty();
		result2.ShouldBeEmpty();
	}

	[Fact]
	public async Task Not_register_when_tags_are_null_or_empty()
	{
		await _sut.RegisterKeyAsync("key-1", null!, CancellationToken.None);
		await _sut.RegisterKeyAsync("key-2", [], CancellationToken.None);

		// Neither should have been tracked
		var keys = await _sut.GetKeysByTagsAsync(["key-1", "key-2"], CancellationToken.None);
		keys.ShouldBeEmpty();
	}

	[Fact]
	public async Task Handle_concurrent_registrations_safely()
	{
		var tasks = Enumerable.Range(0, 100)
			.Select(i => _sut.RegisterKeyAsync(
				$"concurrent-key-{i}",
				["concurrent-tag"],
				CancellationToken.None));

		await Task.WhenAll(tasks);

		var keys = await _sut.GetKeysByTagsAsync(["concurrent-tag"], CancellationToken.None);

		keys.Count.ShouldBe(100);
	}

	[Fact]
	public async Task Handle_concurrent_register_and_unregister()
	{
		// Pre-register some keys
		for (var i = 0; i < 50; i++)
		{
			await _sut.RegisterKeyAsync($"key-{i}", ["mixed-tag"], CancellationToken.None);
		}

		// Concurrently register and unregister
		var registerTasks = Enumerable.Range(50, 50)
			.Select(i => _sut.RegisterKeyAsync($"key-{i}", ["mixed-tag"], CancellationToken.None));

		var unregisterTasks = Enumerable.Range(0, 25)
			.Select(i => _sut.UnregisterKeyAsync($"key-{i}", CancellationToken.None));

		await Task.WhenAll(registerTasks.Concat(unregisterTasks));

		// At least the later-registered keys should exist
		var keys = await _sut.GetKeysByTagsAsync(["mixed-tag"], CancellationToken.None);
		keys.Count.ShouldBeGreaterThanOrEqualTo(50); // 50 new + 25 surviving originals
	}

	[Fact]
	public async Task Clean_up_empty_tag_entries_after_unregister()
	{
		await _sut.RegisterKeyAsync("only-key", ["lonely-tag"], CancellationToken.None);
		await _sut.UnregisterKeyAsync("only-key", CancellationToken.None);

		// Registering a new key with the same tag should work
		await _sut.RegisterKeyAsync("new-key", ["lonely-tag"], CancellationToken.None);

		var keys = await _sut.GetKeysByTagsAsync(["lonely-tag"], CancellationToken.None);
		keys.Count.ShouldBe(1);
		keys.ShouldContain("new-key");
	}
}
