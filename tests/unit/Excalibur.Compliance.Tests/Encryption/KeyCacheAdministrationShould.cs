// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;
using Excalibur.Compliance.Encryption;

namespace Excalibur.Compliance.Tests.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "Concurrency")]
public sealed class KeyCacheAdministrationShould
{
	[Fact]
	public async Task Clear_replaced_entries_without_a_separate_eviction_index()
	{
		for (var lifecycle = 0; lifecycle < 8; lifecycle++)
		{
			using var cache = new KeyCache();
			var metadata = CreateMetadata("replaced");
			for (var replacement = 0; replacement < 256; replacement++)
			{
				cache.Set(metadata);
			}

			// Scheduling opportunity for replacement callbacks in the old implementation;
			// this delay does not prove that those callbacks have drained.
			await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
			cache.TryGet("replaced").ShouldBeSameAs(metadata);
			cache.Clear();
			cache.TryGet("replaced").ShouldBeNull();
			cache.Count.ShouldBe(0);
		}
	}

	[Theory]
	[InlineData("")]
	[InlineData("customer")]
	[InlineData("key:customer:v1")]
	[InlineData("客户")]
	public void Invalidate_base_and_replaced_versions_with_ordinal_prefix_matching(string keyId)
	{
		using var cache = new KeyCache();
		var removed = new[] { keyId, $"{keyId}:v1", $"{keyId}:v2", $"{keyId}:version" };
		var retained = new[] { $"{keyId}:V1", $"{keyId}:other", $"{keyId}neighbor:v1" };
		foreach (var id in removed.Concat(retained))
		{
			var metadata = CreateMetadata(id);
			for (var replacement = 0; replacement < 256; replacement++)
			{
				cache.Set(metadata);
			}
		}

		cache.Invalidate(keyId);

		foreach (var id in removed)
		{
			cache.TryGet(id).ShouldBeNull();
		}

		foreach (var id in retained)
		{
			cache.TryGet(id).ShouldNotBeNull();
		}
	}

	[Fact]
	public void Count_only_admitted_entries_when_capacity_is_exhausted()
	{
		using var cache = new KeyCache(KeyCacheOptions.Default with { MaxEntries = 1 });
		for (var index = 0; index < 2048; index++)
		{
			cache.Set(CreateMetadata($"key-{index}"));
			cache.Count.ShouldBeInRange(0, 1);
		}

		cache.Clear();
		cache.Count.ShouldBe(0);
	}

	[Fact]
	public void Remain_usable_after_repeated_clear()
	{
		using var cache = new KeyCache();
		cache.Set(CreateMetadata("old"));
		cache.Clear();
		cache.Clear();
		var fresh = CreateMetadata("fresh");
		cache.Set(fresh);
		cache.TryGet("old").ShouldBeNull();
		cache.TryGet("fresh").ShouldBeSameAs(fresh);
		cache.Count.ShouldBe(1);
	}

	[Fact]
	public async Task Allow_a_factory_already_in_flight_to_publish_after_clear()
	{
		using var cache = new KeyCache();
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var metadata = CreateMetadata("in-flight");
		var pending = cache.GetOrAddAsync("in-flight", async (_, ct) =>
		{
			entered.SetResult();
			await release.Task.WaitAsync(ct);
			return metadata;
		}, TestContext.Current.CancellationToken);

		Exception? assertionFailure = null;
		try
		{
			await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
			cache.Clear();
			cache.TryGet("in-flight").ShouldBeNull();
		}
		catch (Exception exception)
		{
			assertionFailure = exception;
			throw;
		}
		finally
		{
			_ = release.TrySetResult();
			try
			{
				_ = await pending.WaitAsync(TimeSpan.FromSeconds(5));
			}
			catch (Exception) when (assertionFailure is not null)
			{
				// Preserve the original assertion failure if the bounded cleanup also fails.
			}
		}

		(await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).ShouldBeSameAs(metadata);
		cache.TryGet("in-flight").ShouldBeSameAs(metadata);
	}

	[Fact]
	public async Task Cache_factory_results_under_the_requested_identifier()
	{
		using var cache = new KeyCache();
		var metadata = CreateMetadata("returned-id");
		var result = await cache.GetOrAddAsync("requested-id", (_, _) => Task.FromResult<KeyMetadata?>(metadata),
			TestContext.Current.CancellationToken);

		result.ShouldBeSameAs(metadata);
		cache.TryGet("requested-id").ShouldBeSameAs(metadata);
		cache.TryGet("returned-id").ShouldBeNull();
	}

	[Fact]
	public void Report_zero_and_reject_operations_after_repeated_dispose()
	{
		var cache = new KeyCache();
		cache.Set(CreateMetadata("disposed"));
		cache.Dispose();
		cache.Dispose();
		cache.Count.ShouldBe(0);
		Should.Throw<ObjectDisposedException>(() => cache.TryGet("disposed"));
		Should.Throw<ObjectDisposedException>(() => cache.Clear());
		Should.Throw<ObjectDisposedException>(() => cache.Invalidate("disposed"));
	}

	private static KeyMetadata CreateMetadata(string id) => new()
	{
		KeyId = id,
		Version = 1,
		Status = KeyStatus.Active,
		Algorithm = EncryptionAlgorithm.Aes256Gcm,
		CreatedAt = DateTimeOffset.UtcNow,
	};
}
