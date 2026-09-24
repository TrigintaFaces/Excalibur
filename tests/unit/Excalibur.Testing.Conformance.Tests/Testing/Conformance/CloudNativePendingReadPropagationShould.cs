// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.CloudNative;
using Excalibur.Testing.Conformance;

namespace Excalibur.Testing.Conformance.Tests.Testing.Conformance;

/// <summary>
/// A store whose primary is strongly consistent but whose pending read is ordered through an index that
/// propagates late — the shape <c>ICloudNativeOutboxStore.GetPendingAsync</c> documents, and the one a
/// provider backed by a global secondary index actually exhibits.
/// </summary>
/// <remarks>
/// Writes land on the primary immediately: <see cref="MarkAsPublishedAsync"/> can find and publish a
/// message that the pending read cannot yet see. That asymmetry is the whole point — it is what lets an
/// arm that reads once observe an absence caused by propagation and mistake it for an effect.
/// </remarks>
internal sealed class PropagationDelayingOutboxStore(TimeSpan propagationDelay) : ICloudNativeOutboxStore
{
	private readonly Dictionary<string, Entry> _primary = [];
	private readonly Lock _gate = new();

	public CloudPersistenceProviderType ProviderType => CloudPersistenceProviderType.DynamoDb;

	/// <summary>Counts pending reads that returned the given message, so a test can prove it was ever seen.</summary>
	public int TimesSeenPending { get; private set; }

	public Task<CloudOperationResult<CloudOutboxMessage>> AddAsync(
		CloudOutboxMessage message,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken)
	{
		// Persist the reserved untenanted sentinel rather than a bare null, as the contract requires: this
		// fake is meant to be a CONFORMING store whose only deviation is a late-propagating read index,
		// so that a failing arm indicts the propagation handling and nothing else.
		var stored = message with { TenantId = message.TenantId ?? TenantScope.UntenantedSentinel };

		lock (_gate)
		{
			_primary[stored.MessageId] = new Entry(stored, TimeProvider.System.GetUtcNow() + propagationDelay, false);
		}

		return Task.FromResult(new CloudOperationResult<CloudOutboxMessage>(true, 201, 1.0, stored));
	}

	public Task<CloudQueryResult<CloudOutboxMessage>> GetPendingAsync(
		IPartitionKey partitionKey,
		int batchSize,
		CancellationToken cancellationToken)
	{
		List<CloudOutboxMessage> visible;
		var now = TimeProvider.System.GetUtcNow();

		lock (_gate)
		{
			visible = _primary.Values
				.Where(e => !e.Published
					&& e.VisibleAt <= now
					&& string.Equals(e.Message.PartitionKeyValue, partitionKey.Value, StringComparison.Ordinal))
				.OrderBy(static e => e.Message.CreatedAt)
				.Select(static e => e.Message)
				.Take(batchSize)
				.ToList();

			TimesSeenPending += visible.Count;
		}

		return Task.FromResult(new CloudQueryResult<CloudOutboxMessage>(visible, 1.0));
	}

	public Task<CloudOperationResult> MarkAsPublishedAsync(
		string messageId,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken)
	{
		lock (_gate)
		{
			// The primary is strongly consistent: publishing finds the message even while the pending
			// read cannot yet see it.
			if (_primary.TryGetValue(messageId, out var entry))
			{
				_primary[messageId] = entry with { Published = true };
				return Task.FromResult(new CloudOperationResult(true, 200, 1.0));
			}
		}

		return Task.FromResult(new CloudOperationResult(false, 404, 1.0, errorMessage: "no such message"));
	}

	public Task<IChangeFeedSubscription<CloudOutboxMessage>> SubscribeToNewMessagesAsync(
		IChangeFeedOptions? options,
		CancellationToken cancellationToken) => throw new NotSupportedException();

	private sealed record Entry(CloudOutboxMessage Message, DateTimeOffset VisibleAt, bool Published);
}

/// <summary>
/// Binds the cloud-native outbox kit to a store whose pending-read index propagates later than a single
/// immediate read would wait, and proves the kit's arms hold anyway.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
// A conformance kit arm carries no runner attribute, so an arm this suite never wraps does not run,
// cannot fail, and reads in the results exactly like one that passed. This suite exists to hold the
// kit's arms against a store that delays read-after-write visibility; it is not a second full
// conformance run of the cloud-native outbox contract, and the provider suites that ARE that run
// each wire all twenty arms against real infrastructure.
// Declared, so the omission is a recorded decision rather than silence:
// conformance-partial-suite: full coverage in CosmosDbCloudNativeOutboxStoreKitConformanceShould
public sealed class CloudNativePendingReadPropagationShould : CloudNativeOutboxStoreConformanceTestKit
{
	private static readonly TimeSpan Propagation = TimeSpan.FromMilliseconds(400);

	private readonly PropagationDelayingOutboxStore _store = new(Propagation);

	protected override Task<ICloudNativeOutboxStore> CreateStoreAsync() => Task.FromResult<ICloudNativeOutboxStore>(_store);

	[Fact]
	public Task ReturnTheStagedMessage_AlthoughTheIndexPropagatesAfterAnImmediateReadWouldHaveGivenUp()
		=> AddAsync_ThenGetPending_ReturnsTheStagedMessage();

	[Fact]
	public Task ReturnEveryTenantsMessage_UnderTheSamePropagationDelay()
		=> GetPendingAsync_MustReturnMessagesFromEveryTenant();

	[Fact]
	public Task RoundTripEveryCanonicalField_UnderTheSamePropagationDelay()
		=> AddAsync_PreservesCanonicalFields_OnRoundTrip();

	[Fact]
	public Task RoundTripEveryContractMember_UnderTheSamePropagationDelay()
		=> AddAsync_RoundTripsEveryContractMember_OnThePendingRead();

	[Fact]
	public Task ReadBackInFifoOrder_OnlyOnceEveryStagedMessageIsVisible()
		=> GetPendingAsync_ReturnsMessagesInFifoOrder();

	[Fact]
	public Task RoundTripTheUntenantedPartition_UnderTheSamePropagationDelay()
		=> UntenantedPartition_MustRoundTripItsOwnMessage();

	/// <summary>
	/// The arm that carried the vacuity: it must now establish the message IS pending before publishing
	/// it, so the absence it finally asserts is evidence of exclusion rather than of propagation.
	/// </summary>
	[Fact]
	public async Task ExcludeAPublishedMessage_HavingFirstObservedThatItWasPending()
	{
		await MarkAsPublishedAsync_ThenGetPending_ExcludesTheMessage().ConfigureAwait(false);

		// NON-VACUITY: the arm is only meaningful if the message was actually seen pending at some point.
		// Were it never seen, the closing "not present" assertion would hold for a reason that has
		// nothing to do with publishing, which is exactly the defect this arm was rewritten to remove.
		_store.TimesSeenPending.ShouldBeGreaterThan(
			0,
			"the arm asserted a published message was absent from the pending read without ever having "
			+ "observed it present, so it proved nothing about publishing");
	}

	[Fact]
	public Task StillReadAnUntouchedPartitionAsEmpty_WithoutWaitingForAnythingToPropagate()
		=> GetPendingAsync_EmptyPartition_ReturnsEmpty();
}

/// <summary>
/// The RED control. Proves the bounded poll can still FAIL: a store slower than the arm's budget must
/// make the arm red, or the change would be an unconditional pass wearing the shape of a check.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
// Same reasoning as the suite above, and more pointed here: this one exists to prove the staged-read
// arm FAILS when propagation exceeds its budget, so wrapping the remaining arms would only re-run a
// contract it is not the subject of.
// Declared, so the omission is a recorded decision rather than silence:
// conformance-partial-suite: full coverage in CosmosDbCloudNativeOutboxStoreKitConformanceShould
public sealed class CloudNativePendingReadBeyondTheBudgetShould : CloudNativeOutboxStoreConformanceTestKit
{
	private readonly PropagationDelayingOutboxStore _store = new(TimeSpan.FromSeconds(30));

	/// <summary>A budget far shorter than the store's propagation, so the poll must give up and the arm must fail.</summary>
	protected override TimeSpan PendingReadPropagationTimeout => TimeSpan.FromMilliseconds(150);

	protected override Task<ICloudNativeOutboxStore> CreateStoreAsync() => Task.FromResult<ICloudNativeOutboxStore>(_store);

	[Fact]
	public async Task FailTheStagedReadArm_WhenPropagationExceedsTheBudget()
	{
		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			AddAsync_ThenGetPending_ReturnsTheStagedMessage).ConfigureAwait(false);

		thrown.Message.ShouldContain(
			"pending read",
			Case.Insensitive,
			customMessage: "the failure must name the property that went unmet, not merely that a wait elapsed");
	}

	[Fact]
	public async Task FailTheExclusionArm_RatherThanPassVacuously_WhenTheMessageNeverBecomesPending()
	{
		// The pre-fix shape PASSED here: it published immediately and read once, finding the message
		// absent because it had never propagated. The rewritten arm refuses to draw that conclusion.
		_ = await Should.ThrowAsync<TestFixtureAssertionException>(
			MarkAsPublishedAsync_ThenGetPending_ExcludesTheMessage).ConfigureAwait(false);

		_store.TimesSeenPending.ShouldBe(
			0,
			"this control depends on the message never becoming visible; if it was seen, the store's "
			+ "propagation delay is not doing what this test assumes");
	}
}
