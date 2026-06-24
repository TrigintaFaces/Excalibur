// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Middleware.Inbox;
using Excalibur.Dispatch.Options.Delivery;

using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.TestFakes;

using MessageResult = Excalibur.Dispatch.MessageResult;
using SkipBehavior = Excalibur.Dispatch.Messaging.SkipBehavior;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Engage-tests (bd-pux4gk, S842 ADR-336 Wave 2) for the atomic claim-before-execute idempotency protocol in
/// <see cref="IdempotentHandlerMiddleware"/>. Proves that under concurrent duplicate delivery, exactly one handler
/// execution is admitted (the non-atomic check-then-act admitted N).
/// </summary>
/// <remarks>
/// <para><b>G2 determinism (no <c>sleep</c>, no wall-clock).</b> Concurrency is forced with an async rendezvous: all
/// N deliveries are started, each advances to its claim/check call and registers arrival, and only when all N have
/// arrived are they released together — so they genuinely race the claim. The rendezvous is non-blocking (a
/// <see cref="TaskCompletionSource"/>), so it cannot starve the thread pool.</para>
/// <para><b>Non-vacuity without a worktree.</b> The middleware still contains the real pre-fix check-then-act path
/// (<c>InvokeLegacyAsync</c>, taken when the store does not implement <see cref="IClaimableInboxStore"/>). The
/// <see cref="Admit_duplicates_on_the_legacy_check_then_act_path"/> test drives that real production path through the
/// same rendezvous and shows it admits N — proving the exactly-once locks have teeth (the atomic claim is what
/// prevents the race), exercising actual code rather than a mutation.</para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class IdempotentHandlerMiddlewareConcurrencyShould
{
	private const int Concurrency = 8;

#pragma warning disable CA2012 // ValueTask returned from FakeItEasy ReturnsLazily is awaited exactly once by the SUT.

	[Fact]
	public async Task Admit_exactly_one_handler_under_concurrent_duplicate_delivery_on_the_persistent_claim_path()
	{
		// AC-1: claim-capable persistent store -> atomic TryClaimAsync (first-writer-wins).
		var rendezvous = new AsyncRendezvous(Concurrency);
		var claims = new ConcurrentDictionary<string, byte>();
		var store = A.Fake<IInboxStore>(o => o.Implements<IClaimableInboxStore>());
		var claimable = (IClaimableInboxStore)store;

		A.CallTo(() => claimable.TryClaimAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.ReturnsLazily((string id, string handlerType, CancellationToken ct) => ClaimAsync(id));

		var middleware = CreateMiddleware(A.Fake<IInMemoryDeduplicator>(), store);

		var handlerRuns = await DriveConcurrentAsync(middleware, typeof(PersistentHandler), "msg-ac1").ConfigureAwait(false);

		handlerRuns.ShouldBe(1, "exactly one of N concurrent duplicates must win the atomic claim and run the handler");

		async ValueTask<bool> ClaimAsync(string id)
		{
			await rendezvous.ArriveAndWaitAsync().ConfigureAwait(false); // all N race the claim together
			return claims.TryAdd(id, 0);                                  // atomic first-writer-wins
		}
	}

	[Fact]
	public async Task Admit_exactly_one_handler_under_concurrent_duplicate_delivery_on_the_in_memory_claim_path()
	{
		// AC-2: claim-capable in-memory deduplicator -> atomic TryClaimAsync.
		var rendezvous = new AsyncRendezvous(Concurrency);
		var claims = new ConcurrentDictionary<string, byte>();
		var dedup = A.Fake<IInMemoryDeduplicator>(o => o.Implements<IClaimableDeduplicator>());
		var claimable = (IClaimableDeduplicator)dedup;

		A.CallTo(() => claimable.TryClaimAsync(A<string>._, A<TimeSpan>._, A<CancellationToken>._))
			.ReturnsLazily((string id, TimeSpan expiry, CancellationToken ct) => ClaimAsync(id));

		var middleware = CreateMiddleware(dedup, A.Fake<IInboxStore>());

		var handlerRuns = await DriveConcurrentAsync(middleware, typeof(InMemoryHandler), "msg-ac2").ConfigureAwait(false);

		handlerRuns.ShouldBe(1, "exactly one of N concurrent duplicates must win the atomic in-memory claim");

		async Task<bool> ClaimAsync(string id)
		{
			await rendezvous.ArriveAndWaitAsync().ConfigureAwait(false);
			return claims.TryAdd(id, 0);
		}
	}

	[Fact]
	public async Task Admit_duplicates_on_the_legacy_check_then_act_path()
	{
		// Non-vacuity: a store WITHOUT the claim capability falls into the real pre-fix InvokeLegacyAsync
		// (IsProcessedAsync -> handler -> TryMarkAsProcessedAsync). Forced to interleave, all N see "not
		// processed" before any marks, so the racy check-then-act admits every duplicate.
		var rendezvous = new AsyncRendezvous(Concurrency);
		var store = A.Fake<IInboxStore>(); // NOT claim-capable -> legacy path

		A.CallTo(() => store.IsProcessedAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.ReturnsLazily((string id, string handlerType, CancellationToken ct) => CheckAtRendezvousAsync());

		var middleware = CreateMiddleware(A.Fake<IInMemoryDeduplicator>(), store);

		var handlerRuns = await DriveConcurrentAsync(middleware, typeof(PersistentHandler), "msg-legacy").ConfigureAwait(false);

		// This is the bug the atomic claim + presence-guard eliminate; the exactly-once locks above would regress
		// to this if the claim path were reverted to check-then-act.
		handlerRuns.ShouldBe(
			Concurrency,
			"the non-atomic check-then-act legacy path admits every concurrent duplicate (the race the atomic claim prevents)");

		// Models the check-then-act race deterministically: all N duplicates rendezvous at the "is processed?"
		// check, and each observes "not processed" because none has marked yet at the synchronized check point —
		// so all N are admitted. (Deterministic regardless of thread-pool scheduling, unlike a post-barrier
		// dictionary read whose staggered continuations could see an earlier delivery's mark.)
		async ValueTask<bool> CheckAtRendezvousAsync()
		{
			await rendezvous.ArriveAndWaitAsync().ConfigureAwait(false);
			return false;
		}
	}

	[Fact]
	public async Task Finalize_a_single_delivery_via_MarkProcessedAsync()
	{
		// AC-3: a single (non-duplicate) delivery runs the handler once and finalizes the claim.
		var store = A.Fake<IInboxStore>(o => o.Implements<IClaimableInboxStore>());
		A.CallTo(() => ((IClaimableInboxStore)store).TryClaimAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<bool>(true));

		var middleware = CreateMiddleware(A.Fake<IInMemoryDeduplicator>(), store);

		var handlerRuns = await DriveOnceAsync(middleware, typeof(PersistentHandler), "msg-ac3").ConfigureAwait(false);

		handlerRuns.ShouldBe(1, "a single delivery must run the handler exactly once");
		A.CallTo(() => store.MarkProcessedAsync("msg-ac3", A<string>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Release_the_claim_on_handler_failure_so_redelivery_is_readmitted()
	{
		// AC-4: handler throws after the claim -> ReleaseAsync removes the claim -> a redelivery is re-admitted
		// (no silent downgrade to at-most-once, no terminal entry left behind).
		var claims = new ConcurrentDictionary<string, byte>();
		var store = A.Fake<IInboxStore>(o => o.Implements<IClaimableInboxStore>());
		var claimable = (IClaimableInboxStore)store;

		A.CallTo(() => claimable.TryClaimAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.ReturnsLazily((string id, string handlerType, CancellationToken ct) => new ValueTask<bool>(claims.TryAdd(id, 0)));
		A.CallTo(() => claimable.ReleaseAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.ReturnsLazily((string id, string handlerType, CancellationToken ct) =>
			{
				_ = claims.TryRemove(id, out _);
				return ValueTask.CompletedTask;
			});

		var middleware = CreateMiddleware(A.Fake<IInMemoryDeduplicator>(), store);

		// First delivery: handler throws -> claim released.
		_ = await Should.ThrowAsync<InvalidOperationException>(
			DriveOnceAsync(middleware, typeof(PersistentHandler), "msg-ac4",
				() => throw new InvalidOperationException("handler boom")).AsTask()).ConfigureAwait(false);

		A.CallTo(() => claimable.ReleaseAsync("msg-ac4", A<string>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		// Redelivery: the claim was released, so the message is re-admitted and the handler runs again.
		var rerunCount = await DriveOnceAsync(middleware, typeof(PersistentHandler), "msg-ac4").ConfigureAwait(false);

		rerunCount.ShouldBe(1, "after release-on-failure the redelivery must be re-admitted (at-least-once-until-success)");
	}

#pragma warning restore CA2012

	private static IdempotentHandlerMiddleware CreateMiddleware(IInMemoryDeduplicator deduplicator, IInboxStore? inboxStore)
	{
		var options = Microsoft.Extensions.Options.Options.Create(new InboxOptions
		{
			DuplicateBehavior = SkipBehavior.Silent,
		});

		var configurationProvider = A.Fake<IInboxConfigurationProvider>();
		// Return null for all handler types so the middleware falls back to the [Idempotent] attribute.
		A.CallTo(() => configurationProvider.GetConfiguration(A<Type>._)).Returns(null);

		return new IdempotentHandlerMiddleware(
			options,
			deduplicator,
			NullLoggerFactory.Instance.CreateLogger<IdempotentHandlerMiddleware>(),
			inboxStore,
			messageIdProvider: null,
			configurationProvider: configurationProvider);
	}

	private static async Task<int> DriveConcurrentAsync(IdempotentHandlerMiddleware middleware, Type handlerType, string messageId)
	{
		var handlerRuns = 0;

		ValueTask<IMessageResult> Next(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			_ = Interlocked.Increment(ref handlerRuns);
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Start all N deliveries; each advances to the claim/check call and registers at the rendezvous before any
		// can complete, so they genuinely race. No Task.Run / no thread blocking — the rendezvous is async.
		var deliveries = new Task[Concurrency];
		for (var i = 0; i < Concurrency; i++)
		{
			var context = new FakeMessageContext { MessageId = messageId };
			context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = handlerType;
			deliveries[i] = middleware.InvokeAsync(new FakeDispatchMessage(), context, Next, CancellationToken.None).AsTask();
		}

		await Task.WhenAll(deliveries).ConfigureAwait(false);
		return handlerRuns;
	}

	private static async ValueTask<int> DriveOnceAsync(
		IdempotentHandlerMiddleware middleware,
		Type handlerType,
		string messageId,
		Func<IMessageResult>? handler = null)
	{
		var handlerRuns = 0;

		ValueTask<IMessageResult> Next(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			_ = Interlocked.Increment(ref handlerRuns);
			return new ValueTask<IMessageResult>((handler ?? MessageResult.Success)());
		}

		var context = new FakeMessageContext { MessageId = messageId };
		context.Items[IdempotentHandlerMiddleware.HandlerTypeKey] = handlerType;
		_ = await middleware.InvokeAsync(new FakeDispatchMessage(), context, Next, CancellationToken.None).ConfigureAwait(false);
		return handlerRuns;
	}

	/// <summary>
	/// A non-blocking async rendezvous: the first <c>count</c> callers to <see cref="ArriveAndWaitAsync"/> all
	/// resume together once the <c>count</c>-th has arrived. Used to force genuine concurrency at the claim point
	/// without any wall-clock waiting or thread-pool blocking.
	/// </summary>
	private sealed class AsyncRendezvous(int count)
	{
		private readonly TaskCompletionSource _all = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private int _arrived;

		public Task ArriveAndWaitAsync()
		{
			if (Interlocked.Increment(ref _arrived) >= count)
			{
				_ = _all.TrySetResult();
			}

			return _all.Task;
		}
	}

	[Idempotent]
	private sealed class PersistentHandler { }

	[Idempotent(UseInMemory = true)]
	private sealed class InMemoryHandler { }
}
