// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Serialization;

// Alias to avoid collision with Excalibur.Outbox.InboxOptions.
using DeliveryInboxOptions = Excalibur.Dispatch.Options.Delivery.InboxOptions;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Sprint 847 / Lane K (bead bivkdh) — author≠impl regression lock for the inbox retry-ceiling
/// reconciliation defect (MS-K).
/// </summary>
/// <remarks>
/// <para>
/// <b>Defect (true pre-fix HEAD <c>301b4aa62</c>):</b> <see cref="InboxProcessor"/>'s private
/// <c>ReserveBatchRecordsAsync</c> re-fetched failed entries via
/// <see cref="IInboxStoreAdmin.GetFailedEntriesAsync(int, System.DateTimeOffset?, int, System.Threading.CancellationToken)"/>
/// with a hardcoded <c>maxRetries</c> literal of <c>3</c>, while the dead-letter branch fires at
/// <c>attempt &gt;= _options.MaxAttempts</c> (default 5). An entry that failed 3 times was therefore
/// excluded from re-fetch yet still below the dead-letter threshold → silently stranded (neither
/// retried nor dead-lettered).
/// </para>
/// <para>
/// <b>Fix (FR-K1/FR-K3):</b> the re-fetch ceiling MUST equal the dead-letter ceiling — pass
/// <c>_options.MaxAttempts</c> as the <c>maxRetries</c> argument, sourced from bound options, not a
/// source literal.
/// </para>
/// <para>
/// <b>Non-vacuity:</b> this lock captures the actual <c>maxRetries</c> argument value passed to the
/// store and asserts it equals the configured <see cref="DeliveryInboxOptions.MaxAttempts"/>. On the
/// pre-fix HEAD the captured value is always the literal <c>3</c>, so the parameterized cases for 5
/// and 10 fail (captured 3 ≠ expected) ⇒ RED. Post-fix all cases pass ⇒ GREEN. The seam is exercised
/// by invoking the private <c>ReserveBatchRecordsAsync(int, CancellationToken)</c> via reflection —
/// the same reflection-against-private-method pattern the sibling <c>InboxProcessorShould</c> uses —
/// so the lock binds the real production call site, not a re-implementation.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Inbox")]
[Trait("Priority", "1")]
public sealed class InboxProcessorRetryCeilingShould : UnitTestBase
{
	private static readonly MethodInfo ReserveBatchRecordsAsyncMethod =
		typeof(InboxProcessor).GetMethod("ReserveBatchRecordsAsync", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException("Expected private InboxProcessor.ReserveBatchRecordsAsync(int, CancellationToken).");

	[Fact]
	public async Task PassConfiguredMaxAttemptsAsTheReFetchCeiling_NotAHardcodedLiteral()
	{
		// Arrange — MaxAttempts deliberately != the pre-fix literal 3.
		const int configuredMaxAttempts = 5;
		var captured = CapturedMaxRetries.Create(out var inboxStore);
		await using var processor = CreateProcessor(configuredMaxAttempts, inboxStore);

		// Act
		await InvokeReserveBatchRecordsAsync(processor).ConfigureAwait(false);

		// Assert — the re-fetch ceiling must equal the configured dead-letter ceiling (FR-K1).
		captured.Value.ShouldBe(
			configuredMaxAttempts,
			"the re-fetch maxRetries ceiling MUST equal _options.MaxAttempts (dead-letter ceiling), " +
			"never a hardcoded literal — else entries that failed the literal count are stranded.");
	}

	[Theory]
	[InlineData(3)]
	[InlineData(5)]
	[InlineData(10)]
	public async Task SourceTheReFetchCeilingFromBoundOptions_ForAnyConfiguredMaxAttempts(int configuredMaxAttempts)
	{
		// Arrange
		var captured = CapturedMaxRetries.Create(out var inboxStore);
		await using var processor = CreateProcessor(configuredMaxAttempts, inboxStore);

		// Act
		await InvokeReserveBatchRecordsAsync(processor).ConfigureAwait(false);

		// Assert — parameterized: the captured argument tracks the configured value (FR-K3 / AC-K2).
		captured.Value.ShouldBe(configuredMaxAttempts);
	}

	private static InboxProcessor CreateProcessor(int maxAttempts, IInboxStore inboxStore)
	{
		var options = Options.Create(new DeliveryInboxOptions
		{
			Capacity =
			{
				QueueCapacity = 500,
				ProducerBatchSize = 100,
				ConsumerBatchSize = 50,
				PerRunTotal = 1000,
				ParallelProcessingDegree = 4,
			},
			MaxAttempts = maxAttempts,
		});

		return new InboxProcessor(
			options,
			inboxStore,
			A.Fake<IServiceProvider>(),
			new DispatchJsonSerializer(),
			NullLogger<InboxProcessor>.Instance);
	}

	private static async Task InvokeReserveBatchRecordsAsync(InboxProcessor processor)
	{
		// ReserveBatchRecordsAsync(int batchSize, CancellationToken cancellationToken) -> Task<IReadOnlyCollection<InboxEntry>>
		var task = (Task)ReserveBatchRecordsAsyncMethod.Invoke(processor, [50, CancellationToken.None])!;
		await task.ConfigureAwait(false);
	}

	/// <summary>
	/// Captures the <c>maxRetries</c> argument passed to the store's <c>GetFailedEntriesAsync</c>.
	/// </summary>
	private sealed class CapturedMaxRetries
	{
		public int Value { get; private set; } = -1;

		public static CapturedMaxRetries Create(out IInboxStore inboxStore)
		{
			var capture = new CapturedMaxRetries();
			var store = A.Fake<IInboxStore>(o => o.Implements<IInboxStoreAdmin>());
			_ = A.CallTo(() => ((IInboxStoreAdmin)store).GetFailedEntriesAsync(
					A<int>._,
					A<DateTimeOffset?>._,
					A<int>._,
					A<CancellationToken>._))
				.Invokes((int maxRetries, DateTimeOffset? _, int _, CancellationToken _) => capture.Value = maxRetries)
				.ReturnsLazily(() => new ValueTask<IEnumerable<InboxEntry>>(Array.Empty<InboxEntry>()));
			inboxStore = store;
			return capture;
		}
	}
}
