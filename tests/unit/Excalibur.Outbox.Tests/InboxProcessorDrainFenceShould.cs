// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;
using System.Text.Json;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Serialization;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using DeliveryInboxOptions = Excalibur.Dispatch.Options.Delivery.InboxOptions;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Guarantee lock for the retry drain's single-in-flight property: for one entry, at most one handler
/// invocation is in flight at any instant, across concurrent drains and across hosts.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the drain needs its own arm.</b> The drain is a second, independent consumer of the same rows
/// as the receive path, so the receive path's deduplication does not imply this. The drain's read is a
/// plain query that takes no ownership term, which means two processors legitimately hold the same entry
/// after reading. The only thing separating them is the store's atomic lease acquisition, taken per entry
/// immediately before dispatch — and nothing in the store's own conformance kit can tell whether the drain
/// actually <i>asks</i> for that term. That is what these arms test.
/// </para>
/// <para>
/// <b>Non-vacuity.</b> The arms drive two real <see cref="InboxProcessor"/> instances against one real
/// inbox store obtained through the supported registration — not a mock, because a mock returns what it
/// was told and cannot exhibit the race; the store's refusal semantics are the object under test. The
/// handler holds for <see cref="HandlerDwell"/> so both drains are genuinely in flight together: without
/// it, the first processor could finish and mark the entry before the second one reads, and the safety arm
/// would pass without the race ever occurring.
/// </para>
/// <para>
/// A drain that omits the lease acquisition leaves the entry <see cref="InboxStatus.Failed"/> while it
/// dispatches, so the second processor reads it, dispatches it too, and both idempotent marks report
/// success — two invocations, no conflict visible to any caller ⇒ the safety arm is RED. With the
/// acquisition in place exactly one processor receives a term and the other skips the entry ⇒ GREEN.
/// </para>
/// <para>
/// <see cref="DispatchTheEntry_WhenASingleProcessorDrainsIt"/> is the positive control, and it is
/// required: a drain that dispatches nothing at all satisfies "at most one invocation" perfectly. Safety
/// alone would certify a broken drain, so the pair is the guarantee.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Inbox")]
[Trait("Priority", "1")]
public sealed class InboxProcessorDrainFenceShould
{
	private const string ProbeMessageId = "inbox-drain-fence";

	// Mirrors both production writers: the fully qualified name is persisted as HandlerType, the short
	// name as MessageType.
	private static readonly string HandlerTypeName =
		typeof(DrainFenceProbeMessage).FullName ?? typeof(DrainFenceProbeMessage).Name;

	private static readonly string MessageTypeName = typeof(DrainFenceProbeMessage).Name;

	// Long enough that a second drain starting concurrently reads the entry while the first is still
	// inside its handler, and short enough not to slow the suite. This is what makes the unfenced case
	// deterministically produce two invocations rather than occasionally producing one.
	private static readonly TimeSpan HandlerDwell = TimeSpan.FromMilliseconds(400);

	[Fact]
	public async Task DispatchAnEntryOnlyOnce_WhenTwoProcessorsDrainTheSameFailedEntry()
	{
		await using var harness = await DrainFenceHarness.CreateAsync().ConfigureAwait(false);

		// Two processors over ONE store, drained concurrently -- the shape of two hosts running the drain.
		await Task.WhenAll(
			harness.First.DispatchPendingMessagesAsync(CancellationToken.None),
			harness.Second.DispatchPendingMessagesAsync(CancellationToken.None)).ConfigureAwait(false);

		harness.DispatchCount.ShouldBe(
			1,
			"two concurrent drains of one failed entry must produce exactly one handler invocation. More "
			+ "than one means the drain dispatched without first taking an ownership term on the entry: "
			+ "both processors ran the handler, both marks were idempotent, and no caller saw a conflict.");
	}

	[Fact]
	public async Task DispatchTheEntry_WhenASingleProcessorDrainsIt()
	{
		await using var harness = await DrainFenceHarness.CreateAsync().ConfigureAwait(false);

		_ = await harness.First.DispatchPendingMessagesAsync(CancellationToken.None).ConfigureAwait(false);

		harness.DispatchCount.ShouldBe(
			1,
			"a single drain of a re-admittable failed entry must still dispatch it. This is the positive "
			+ "control for the safety arm: a drain that dispatches nothing satisfies 'at most one "
			+ "invocation' vacuously, so without this arm a completely broken drain would certify.");
	}

	private sealed record DrainFenceProbeMessage(string Id) : IDispatchEvent;

	private sealed class DrainFenceHarness : IAsyncDisposable
	{
		private readonly ServiceProvider _services;
		private readonly Counter _counter;

		private DrainFenceHarness(
			ServiceProvider services, InboxProcessor first, InboxProcessor second, Counter counter)
		{
			_services = services;
			First = first;
			Second = second;
			_counter = counter;
		}

		public InboxProcessor First { get; }

		public InboxProcessor Second { get; }

		public int DispatchCount => _counter.Value;

		public static async Task<DrainFenceHarness> CreateAsync()
		{
			var services = new ServiceCollection();
			_ = services.AddLogging();
			_ = services.AddInMemoryInboxStore();

			MessageTypeRegistry.RegisterType<DrainFenceProbeMessage>();

			var counter = new Counter();

			// One shared dispatcher instance rather than a per-scope fake, so the arms can count what the
			// drain actually reached. A per-scope fake is unobservable from the test.
			var dispatcher = A.Fake<IDispatcher>();
			_ = A.CallTo(() => dispatcher.DispatchAsync(
					A<IDispatchMessage>._,
					A<IMessageContext>._,
					A<CancellationToken>._))
				.ReturnsLazily(_ => DispatchSlowlyAsync(counter));

			_ = services.AddScoped(_ => dispatcher);
			var provider = services.BuildServiceProvider();

			// AddInMemoryInboxStore registers the contract under the provider key, not unkeyed.
			var store = provider.GetRequiredKeyedService<IInboxStore>("inmemory");

			var payload = Encoding.UTF8.GetBytes(
				JsonSerializer.Serialize(new DrainFenceProbeMessage(ProbeMessageId)));

			_ = await store.CreateEntryAsync(
				ProbeMessageId,
				HandlerTypeName,
				MessageTypeName,
				payload,
				new Dictionary<string, object>(StringComparer.Ordinal),
				CancellationToken.None).ConfigureAwait(false);

			// Put the entry into the state the re-admission drain selects: Failed, one attempt spent, and
			// last attempted long enough ago to clear the drain's always-on re-admission floor.
			await store.MarkFailedAsync(
				ProbeMessageId, HandlerTypeName, "seeded failure", CancellationToken.None).ConfigureAwait(false);

			var seeded = await store.GetEntryAsync(ProbeMessageId, HandlerTypeName, CancellationToken.None)
				.ConfigureAwait(false)
				?? throw new InvalidOperationException("Seeded inbox entry was not stored under its own key.");
			seeded.LastAttemptAt = DateTimeOffset.UtcNow.AddHours(-1);

			var first = new InboxProcessor(
				CreateOptions(),
				store,
				provider,
				new DispatchJsonSerializer(),
				NullLogger<InboxProcessor>.Instance);
			first.Init("dispatcher-drain-fence-a");

			// A SECOND processor over the SAME store. The registration is transient precisely so each
			// dispatcher gets its own instance, which is why a singleton lifetime would not fence this --
			// and why the fence has to come from the store.
			var second = new InboxProcessor(
				CreateOptions(),
				store,
				provider,
				new DispatchJsonSerializer(),
				NullLogger<InboxProcessor>.Instance);
			second.Init("dispatcher-drain-fence-b");

			return new DrainFenceHarness(provider, first, second, counter);
		}

		// Counts the invocation, then HOLDS the entry in flight for HandlerDwell so a concurrently starting
		// drain observes it mid-handler. Without the dwell the first processor could finish and mark the
		// entry before the second one reads, and the safety arm would pass without the race occurring.
		private static async Task<IMessageResult> DispatchSlowlyAsync(Counter counter)
		{
			counter.Increment();
			await Task.Delay(HandlerDwell).ConfigureAwait(false);
			return MessageResult.Success();
		}

		private sealed class Counter
		{
			private int _value;

			public int Value => Volatile.Read(ref _value);

			public void Increment() => Interlocked.Increment(ref _value);
		}

		public async ValueTask DisposeAsync()
		{
			await First.DisposeAsync().ConfigureAwait(false);
			await Second.DisposeAsync().ConfigureAwait(false);
			await _services.DisposeAsync().ConfigureAwait(false);
		}

		private static IOptions<DeliveryInboxOptions> CreateOptions() =>
			Options.Create(new DeliveryInboxOptions
			{
				Capacity =
				{
					QueueCapacity = 1,
					ProducerBatchSize = 1,
					ConsumerBatchSize = 1,
					PerRunTotal = 1,
					ParallelProcessingDegree = 2,
				},
				MaxAttempts = 5,
				BatchTuning =
				{
					EnableBatchDatabaseOperations = false,
				},
			});
	}
}
