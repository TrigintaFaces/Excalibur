// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;
using Excalibur.Compliance.Encryption;

namespace Excalibur.Compliance.Tests.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "Concurrency")]
public sealed class KeyCachePerformanceContractShould
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Return_faulted_tasks_for_invalid_arguments_and_disposed_cache(bool explicitTtl)
	{
		using var cache = new KeyCache();
		var invalidKey = GetAsync(cache, null!, Factory, explicitTtl, CancellationToken.None);
		var invalidFactory = GetAsync(cache, "key", null!, explicitTtl, CancellationToken.None);
		invalidKey.IsFaulted.ShouldBeTrue();
		invalidFactory.IsFaulted.ShouldBeTrue();
		await Should.ThrowAsync<ArgumentNullException>(() => invalidKey);
		await Should.ThrowAsync<ArgumentNullException>(() => invalidFactory);
		cache.Dispose();
		var disposed = GetAsync(cache, "key", Factory, explicitTtl, CancellationToken.None);
		disposed.IsFaulted.ShouldBeTrue();
		await Should.ThrowAsync<ObjectDisposedException>(() => disposed);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Preserve_synchronous_and_asynchronous_factory_failures(bool explicitTtl)
	{
		using var cache = new KeyCache();
		var failure = new InvalidOperationException("factory failure");
		var synchronous = GetAsync(cache, "sync", (_, _) => throw failure, explicitTtl, CancellationToken.None);
		synchronous.IsFaulted.ShouldBeTrue();
		(await Should.ThrowAsync<InvalidOperationException>(() => synchronous)).ShouldBeSameAs(failure);
		var asynchronous = GetAsync(cache, "async", (_, _) => Task.FromException<KeyMetadata?>(failure), explicitTtl, CancellationToken.None);
		(await Should.ThrowAsync<InvalidOperationException>(() => asynchronous)).ShouldBeSameAs(failure);
		cache.Count.ShouldBe(0);
		(await GetAsync(cache, "sync", Factory, explicitTtl, CancellationToken.None)).ShouldNotBeNull();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Preserve_factory_failure_after_suspension_and_allow_retry(bool explicitTtl)
	{
		using var cache = new KeyCache();
		var release = new TaskCompletionSource<KeyMetadata?>(TaskCreationOptions.RunContinuationsAsynchronously);
		var failure = new InvalidOperationException("delayed factory failure");
		var pending = GetAsync(cache, "delayed", (_, _) => release.Task, explicitTtl, CancellationToken.None);
		var wasPending = !pending.IsCompleted;
		release.SetException(failure);
		(await Should.ThrowAsync<InvalidOperationException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)))).ShouldBeSameAs(failure);
		wasPending.ShouldBeTrue();
		cache.Count.ShouldBe(0);
		(await GetAsync(cache, "delayed", Factory, explicitTtl, CancellationToken.None)).ShouldNotBeNull();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Preserve_canceled_miss_token_but_allow_a_cached_hit(bool explicitTtl)
	{
		using var cache = new KeyCache();
		using var cancellation = new CancellationTokenSource();
		await cancellation.CancelAsync();
		var metadata = CreateMetadata("hit");
		cache.Set(metadata);
		var calls = 0;
		Task<KeyMetadata?> UnexpectedFactory(string _, CancellationToken __)
		{
			calls++;
			throw new InvalidOperationException("Canceled miss and cached hit must bypass factory.");
		}

		(await GetAsync(cache, "hit", UnexpectedFactory, explicitTtl, cancellation.Token)).ShouldBeSameAs(metadata);
		var miss = GetAsync(cache, "miss", UnexpectedFactory, explicitTtl, cancellation.Token);
		var exception = await Should.ThrowAsync<OperationCanceledException>(() => miss);
		miss.IsCanceled.ShouldBeTrue();
		exception.CancellationToken.ShouldBe(cancellation.Token);
		calls.ShouldBe(0);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Cancel_a_waiter_without_canceling_the_factory_owner(bool explicitTtl)
	{
		using var cache = new KeyCache();
		using var cancellation = new CancellationTokenSource();
		var release = new TaskCompletionSource<KeyMetadata?>(TaskCreationOptions.RunContinuationsAsynchronously);
		var calls = 0;
		Task<KeyMetadata?> HeldFactory(string _, CancellationToken __)
		{
			calls++;
			return release.Task;
		}

		var owner = GetAsync(cache, "shared", HeldFactory, explicitTtl, TestContext.Current.CancellationToken);
		var waiter = GetAsync(cache, "shared", HeldFactory, explicitTtl, cancellation.Token);
		Exception? assertionFailure = null;
		try
		{
			await cancellation.CancelAsync();
			var exception = await Should.ThrowAsync<OperationCanceledException>(() => waiter.WaitAsync(TimeSpan.FromSeconds(5)));
			exception.CancellationToken.ShouldBe(cancellation.Token);
			waiter.IsCanceled.ShouldBeTrue();
			owner.IsCompleted.ShouldBeFalse();
			calls.ShouldBe(1);
		}
		catch (Exception exception)
		{
			assertionFailure = exception;
			throw;
		}
		finally
		{
			_ = release.TrySetResult(CreateMetadata("shared"));
			try
			{
				_ = await owner.WaitAsync(TimeSpan.FromSeconds(5));
			}
			catch (Exception) when (assertionFailure is not null)
			{
				// Keep the primary assertion if bounded cleanup also fails.
			}
		}

		(await GetAsync(cache, "shared", HeldFactory, explicitTtl, CancellationToken.None)).ShouldBeSameAs(await owner);
		calls.ShouldBe(1);
	}

	[Fact]
	public void Reject_null_metadata_identifiers_without_overwriting_the_empty_identifier()
	{
		using var cache = new KeyCache();
		var empty = CreateMetadata(string.Empty);
		cache.Set(empty);
		Should.Throw<ArgumentNullException>(() => cache.Set(CreateMetadata(null!)));
		Should.Throw<ArgumentNullException>(() => cache.Set(CreateMetadata(null!), TimeSpan.FromMinutes(1)));
		cache.TryGet(string.Empty).ShouldBeSameAs(empty);
	}

	[Fact]
	public async Task Publish_factory_metadata_under_the_requested_key_even_if_metadata_id_is_null()
	{
		using var cache = new KeyCache();
		var metadata = CreateMetadata(null!);
		(await cache.GetOrAddAsync("requested", (_, _) => Task.FromResult<KeyMetadata?>(metadata), CancellationToken.None)).ShouldBeSameAs(metadata);
		cache.TryGet("requested").ShouldBeSameAs(metadata);
		cache.TryGet(string.Empty).ShouldBeNull();
	}

	private static Task<KeyMetadata?> GetAsync(KeyCache cache, string keyId,
		Func<string, CancellationToken, Task<KeyMetadata?>> factory, bool explicitTtl, CancellationToken token) =>
		explicitTtl ? cache.GetOrAddAsync(keyId, TimeSpan.FromMinutes(5), factory, token) : cache.GetOrAddAsync(keyId, factory, token);

	private static Task<KeyMetadata?> Factory(string keyId, CancellationToken _) => Task.FromResult<KeyMetadata?>(CreateMetadata(keyId));

	private static KeyMetadata CreateMetadata(string keyId) => new()
	{
		KeyId = keyId,
		Version = 1,
		Status = KeyStatus.Active,
		Algorithm = EncryptionAlgorithm.Aes256Gcm,
		CreatedAt = DateTimeOffset.UtcNow,
	};
}
