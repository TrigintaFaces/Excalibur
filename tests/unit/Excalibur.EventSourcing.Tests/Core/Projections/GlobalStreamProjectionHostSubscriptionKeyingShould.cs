// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

/// <summary>
/// The two keys this host addresses: the subscription name it binds under, and the projection key that
/// names its member row and both halves of its cursor.
/// </summary>
/// <remarks>
/// <para>
/// TWO MEASURED DEFECTS, both of which satisfied every pre-existing assertion. Neither is detectable by
/// asserting that an operation succeeded, because in both cases it did.
/// </para>
/// <para>
/// The first: the subscription name was returned BARE whenever a projection name was configured and
/// non-default, so a consumer who configured the same name the asynchronous processing host uses bound
/// both hosts to one subscription, hence one roster. This host folds a plain state instance and holds no
/// projection registry, so its member can never be an entry in that host's hosted set -- the shared
/// roster was permanently unattestable, every page, forever.
/// </para>
/// <para>
/// The second: the member was bound under the pair (subscription name, projection key) while the cursor
/// was read and advanced under the subscription name alone. The release operation therefore deleted a
/// row nothing had ever written, RETURNED SUCCESS, and left the real cursor exactly where it was.
/// </para>
/// <para>
/// NO ARM HERE HARDCODES THE NAMESPACE PREFIX, and that is deliberate rather than tidiness. Each arm
/// reads the subscription name back from the bind the host actually performed, so the two defects stay
/// independently falsifiable: a mutation to the name derivation cannot redden a cursor-keying arm, and a
/// mutation to the cursor keying cannot redden a name arm. A shared hardcoded constant would couple
/// them, and a single mutation reddening both halves proves neither.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Projections")]
public sealed class GlobalStreamProjectionHostSubscriptionKeyingShould
{
	/// <summary>
	/// The name the asynchronous processing host binds under: the configured projection name, BARE.
	/// </summary>
	/// <remarks>
	/// Load-bearing, and the one string this fixture takes from the sibling host rather than reading
	/// back. That host binds <c>options.ProjectionName</c> with no namespace of its own, so a consumer
	/// configuring this value on both hosts is the collision the first defect produced.
	/// </remarks>
	private const string SharedConfiguredName = "OrderSummaries";

	/// <summary>
	/// Stands in for a projection hosted by another deployment under the shared subscription name.
	/// </summary>
	/// <remarks>
	/// The unattestable-member computation reads only a registration's projection TYPE, deriving the key
	/// from it. A real asynchronous projection would need its state type at compile time; a stand-in is
	/// enough, and deriving the planted member's key from this very type is what makes the correspondence
	/// structural -- the roster cannot drift out of "fully hosted" through a rename.
	/// </remarks>
	internal sealed class AsyncHostedProjection
	{
		public int Count { get; set; }
	}

	/// <summary>A projection key the asynchronous host hosts and this host could never host.</summary>
	private static readonly string AsyncHostedProjectionKey = typeof(AsyncHostedProjection).FullName!;

	/// <summary>A registration standing in for that projection, as the asynchronous host would hold it.</summary>
	private static ProjectionRegistration AsyncHostedRegistration() =>
		new(typeof(AsyncHostedProjection), ProjectionMode.Async, new object(), inlineApply: null);

	private static SubscriptionMember Member(string key) => new(key, new string('A', 64), string.Empty);

	/// <summary>
	/// Records the subscription name the host bound under, and signals the bind and the first cursor
	/// advance so the arms wait on the real condition instead of a sleep.
	/// </summary>
	private sealed class RecordingCheckpointStore(ISubscriptionCheckpointStore inner) : ISubscriptionCheckpointStore
	{
		private readonly TaskCompletionSource _bound = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly TaskCompletionSource _advanced = new(TaskCreationOptions.RunContinuationsAsynchronously);

		/// <summary>The subscription name of the first non-empty bind observed, or null if none.</summary>
		public string? BoundSubscriptionName { get; private set; }

		public Task Bound => _bound.Task;

		public Task Advanced => _advanced.Task;

		public Task<SubscriptionRoster> BindSubscriptionAsync(
			string subscriptionName, IReadOnlyList<SubscriptionMember> members, CancellationToken cancellationToken)
		{
			if (members.Count > 0)
			{
				BoundSubscriptionName ??= subscriptionName;
				_ = _bound.TrySetResult();
			}

			return inner.BindSubscriptionAsync(subscriptionName, members, cancellationToken);
		}

		public Task<bool> ReleaseSubscriptionMemberAsync(
            string subscriptionName, string projectionKey, Guid bindingId, CancellationToken cancellationToken) =>
            inner.ReleaseSubscriptionMemberAsync(subscriptionName, projectionKey, bindingId, cancellationToken);

        public Task<SubscriptionRoster> ReadSubscriptionAsync(string subscriptionName, CancellationToken cancellationToken) =>
            inner.ReadSubscriptionAsync(subscriptionName, cancellationToken);

		public Task<long?> GetCheckpointAsync(
			string subscriptionName, string projectionKey, CancellationToken cancellationToken) =>
			inner.GetCheckpointAsync(subscriptionName, projectionKey, cancellationToken);

		public async Task<CheckpointAdvanceOutcome> AdvanceCheckpointAsync(
			string subscriptionName, string projectionKey, Guid bindingId, long? expectedPosition, long newPosition,
			CancellationToken cancellationToken)
		{
			var outcome = await inner
				.AdvanceCheckpointAsync(subscriptionName, projectionKey, bindingId, expectedPosition, newPosition, cancellationToken)
				.ConfigureAwait(false);
			_ = _advanced.TrySetResult();
			return outcome;
		}

		public Task<IReadOnlyList<SubscriptionCheckpoint>> EnumerateCheckpointsAsync(CancellationToken cancellationToken) =>
			inner.EnumerateCheckpointsAsync(cancellationToken);
	}

	/// <summary>What one host run observed.</summary>
	/// <param name="BoundSubscriptionName">The name the host bound its roster under.</param>
	/// <param name="ReadPositions">Every global-stream position the host read from, in order.</param>
	private sealed record HostRun(string BoundSubscriptionName, IReadOnlyList<long> ReadPositions);

	/// <summary>
	/// Starts a host over the supplied store, lets it bind (and checkpoint, when an event is supplied),
	/// then stops it and reports what it addressed.
	/// </summary>
	/// <param name="checkpoints">The store the host binds and checkpoints against.</param>
	/// <param name="projectionName">The configured projection name, or null to leave the default.</param>
	/// <param name="firstBatchEvent">An event to serve on the first read, or null for an empty stream.</param>
	/// <returns>The subscription name bound and the positions read.</returns>
	private static async Task<HostRun> RunHostAsync(
		ISubscriptionCheckpointStore checkpoints,
		string? projectionName = null,
		StoredEvent? firstBatchEvent = null)
	{
		var recording = new RecordingCheckpointStore(checkpoints);
		var readPositions = new List<long>();
		var query = A.Fake<IGlobalStreamQuery>();
		var projection = A.Fake<IGlobalStreamProjection<GlobalStreamTestState>>();
		var serializer = A.Fake<IEventSerializer>();
		var domainEvent = A.Fake<IDomainEvent>();

		var served = false;
		A.CallTo(() => query.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily(call =>
			{
				lock (readPositions)
				{
					readPositions.Add(((GlobalStreamPosition)call.Arguments[0]!).Position);
				}

				if (firstBatchEvent is null || served)
				{
					return new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>());
				}

				served = true;
				return new ValueTask<IReadOnlyList<StoredEvent>>(new[] { firstBatchEvent });
			});

		A.CallTo(() => serializer.ResolveType(A<string>._)).Returns(typeof(IDomainEvent));
		A.CallTo(() => serializer.DeserializeEvent(A<byte[]>._, A<Type>._)).Returns(domainEvent);
		A.CallTo(() => projection.ApplyAsync(domainEvent, A<GlobalStreamTestState>._, A<CancellationToken>._))
			.Returns(Task.CompletedTask);

		var services = new ServiceCollection();
		_ = services.AddSingleton(query);
		_ = services.AddSingleton(projection);
		_ = services.AddSingleton<ISubscriptionCheckpointStore>(recording);
		await using var provider = services.BuildServiceProvider();

		var options = new GlobalStreamProjectionOptions
		{
			IdlePollingInterval = TimeSpan.FromMilliseconds(10),
			CheckpointInterval = 1,
		};

		if (projectionName is not null)
		{
			options.ProjectionName = projectionName;
		}

		var host = new GlobalStreamProjectionHost<GlobalStreamTestState>(
			provider.GetRequiredService<IServiceScopeFactory>(),
			serializer,
			Options.Create(options),
			NullLogger<GlobalStreamProjectionHost<GlobalStreamTestState>>.Instance,
			provider);

		using var cts = new CancellationTokenSource();
		await host.StartAsync(cts.Token);
		await Await(recording.Bound, "the host never bound its roster").ConfigureAwait(false);

		if (firstBatchEvent is not null)
		{
			await Await(recording.Advanced, "the host never advanced a cursor").ConfigureAwait(false);
		}

		await cts.CancelAsync().ConfigureAwait(false);
		await host.StopAsync(CancellationToken.None);

		lock (readPositions)
		{
			return new HostRun(
				recording.BoundSubscriptionName.ShouldNotBeNull(),
				[.. readPositions]);
		}
	}

	/// <summary>Waits on a signal with a sentence rather than a bare cancellation on timeout.</summary>
	/// <param name="signal">The signal to await.</param>
	/// <param name="whatFailed">What a timeout means, stated for the next reader.</param>
	/// <returns>A task that completes once the signal fires.</returns>
	/// <remarks>
	/// A timeout here has exactly one cause and it is worth saying out loud. The shared signal helper
	/// surfaces a bare cancellation, which detects the regression and teaches nothing -- the next reader
	/// would have to reconstruct what the arm was waiting for.
	/// </remarks>
	private static async Task Await(Task signal, string whatFailed)
	{
		var deadline = DateTimeOffset.UtcNow
			+ global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(30));

		while (!signal.IsCompleted)
		{
			if (DateTimeOffset.UtcNow > deadline)
			{
				throw new ShouldAssertException(whatFailed);
			}

			await Task.Delay(10, CancellationToken.None).ConfigureAwait(false);
		}

		await signal.ConfigureAwait(false);
	}

	// ───────────────────────── DEFECT 1: one configured name, two hosts, one roster ─────────────────────────

	/// <summary>
	/// SAFETY. A global-stream host and an asynchronous host configured with the SAME projection name do
	/// not share a roster, and the asynchronous host's attestation still succeeds.
	/// </summary>
	/// <remarks>
	/// <para>
	/// THE ATTESTATION IS THE ASSERTION, not the inequality of two strings. The damage this defect did was
	/// a permanently DENIED attestation: a name-inequality assertion would pass against a variant that
	/// namespaced the two hosts apart and still left liveness dead, because inequality is implied by the
	/// fix and is not the property anyone depends on.
	/// </para>
	/// <para>
	/// So this arm computes the quantifier the asynchronous host itself reads -- the roster members it
	/// does not host -- and requires it empty. Before the fix the global host's state type sat in that
	/// roster, was never an entry in the asynchronous host's registry, and therefore appeared in the
	/// unattestable set on every page: every Inline save reported the read model possibly behind forever,
	/// and the pull entry point could never report caught up.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task NotShareARosterWithAnAsyncHostConfiguredWithTheSameProjectionName()
	{
		var checkpoints = new InMemorySubscriptionCheckpointStore();

		// The asynchronous host binds the configured name BARE, and hosts a projection of its own.
		_ = await checkpoints.BindSubscriptionAsync(
			SharedConfiguredName, [Member(AsyncHostedProjectionKey)], CancellationToken.None);

		// The global-stream host is configured with the very same name.
		var run = await RunHostAsync(checkpoints, SharedConfiguredName);

		// The asynchronous host re-reads its roster, as it does on every binding cycle.
		var asyncRoster = await checkpoints.BindSubscriptionAsync(
			SharedConfiguredName, [Member(AsyncHostedProjectionKey)], CancellationToken.None);

		AsyncProjectionProcessingHost
			.UnattestableMembers(asyncRoster, [AsyncHostedRegistration()])
			.ShouldBeEmpty(
				"the asynchronous host must still be able to attest its own subscription. A global-stream "
				+ "host sharing that roster contributes a member the asynchronous host can never host -- "
				+ "it folds a plain state instance and holds no projection registry -- so the set below "
				+ "would be non-empty on every page and every attestation would be denied forever.");

		// And the global host really did run and bind, so the arm above is not measuring a host that
		// never started.
		run.BoundSubscriptionName.ShouldNotBe(
			SharedConfiguredName,
			"the global-stream host must not bind the configured name bare; that is what put its member "
			+ "into the asynchronous host's roster.");
	}

	/// <summary>
	/// LIVENESS. Two global-stream hosts over ONE state type stay distinct subscriptions when their
	/// configured names differ.
	/// </summary>
	/// <remarks>
	/// This is the property the type's own documentation promises, and the one a careless namespacing
	/// breaks: prefixing while DROPPING the configured distinguisher would give every state type one
	/// name, trading a cross-host collision for a global-versus-global one. The arm asserts the names
	/// read back from the two binds, so it is blind to what the prefix is and sensitive only to whether
	/// the configured name still distinguishes.
	/// </remarks>
	[Fact]
	public async Task KeepTwoGlobalHostsOverOneStateTypeDistinctWhenTheirConfiguredNamesDiffer()
	{
		var checkpoints = new InMemorySubscriptionCheckpointStore();

		var alpha = await RunHostAsync(checkpoints, "orders-alpha");
		var beta = await RunHostAsync(checkpoints, "orders-beta");

		alpha.BoundSubscriptionName.ShouldNotBe(
			beta.BoundSubscriptionName,
			"two hosts over the same state type with different configured names are two subscriptions. "
			+ "Namespacing that discards the configured name collapses them onto one roster and one "
			+ "cursor, so each would skip what the other consumed.");

		// Each is a subscription of its own with exactly one member, not one roster holding two.
		foreach (var name in (string[])[alpha.BoundSubscriptionName, beta.BoundSubscriptionName])
		{
			var roster = await checkpoints.BindSubscriptionAsync(name, [], CancellationToken.None);
			roster.Members.ShouldHaveSingleItem().ProjectionKey.ShouldBe(typeof(GlobalStreamTestState).FullName);
		}
	}

	/// <summary>
	/// LIVENESS. A blank, whitespace or default projection name still falls back to the name derived from
	/// the state type.
	/// </summary>
	/// <param name="configured">The configured projection name under test.</param>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// The default-check lives INSIDE the namespacing rather than beside it, and that nesting is what
	/// this arm binds. Prefixing the configured value while dropping the default-check would name every
	/// state type after the asynchronous host's default, which is why the second assertion is here.
	/// </remarks>
	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData(GlobalStreamProjectionOptions.DefaultProjectionName)]
	public async Task FallBackToTheStateDerivedNameWhenTheConfiguredNameIsBlankOrDefault(string configured)
	{
		var checkpoints = new InMemorySubscriptionCheckpointStore();

		var run = await RunHostAsync(checkpoints, configured);

		// Ordinal, matching how a subscription name is compared in storage.
		run.BoundSubscriptionName
			.Contains(typeof(GlobalStreamTestState).FullName!, StringComparison.Ordinal)
			.ShouldBeTrue(
				"with no usable configured name the subscription must be distinguished by the state type; "
				+ "otherwise two hosts over different state types share one cursor. Bound name was "
				+ run.BoundSubscriptionName);

		run.BoundSubscriptionName
			.Contains(GlobalStreamProjectionOptions.DefaultProjectionName, StringComparison.Ordinal)
			.ShouldBeFalse(
				"the default value is not a distinguisher. Carrying it through would give every state "
				+ "type the same subscription name. Bound name was " + run.BoundSubscriptionName);
	}

	// ───────────────────────── DEFECT 2: bound under one key, advanced under another ─────────────────────────

	/// <summary>
	/// SAFETY. Releasing this host's member clears the cursor the host READS, and a subsequent start
	/// folds from the beginning instead of resuming.
	/// </summary>
	/// <remarks>
	/// <para>
	/// THE CURSOR IS THE ASSERTION, never the release's return value. That return value was ALREADY
	/// <see langword="true"/> before the fix -- the release deleted the member row it had genuinely
	/// bound, and reported success -- while the cursor lived under a different key and survived
	/// untouched. A consumer following the release-and-rebuild guidance got a success and no reset, and
	/// the next start resumed from the old position against a read model the rebuild had already
	/// rewritten.
	/// </para>
	/// <para>
	/// The first assertion is the direct one: the cursor must be addressable by the SAME pair the member
	/// was bound under. The resume is the consequence, asserted separately so a reader can see that the
	/// cleared cursor is the one the host consults on startup rather than merely a row that is now absent.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task ClearTheCursorTheHostReadsWhenItsMemberIsReleased()
	{
		var checkpoints = new InMemorySubscriptionCheckpointStore();
		var projectionKey = typeof(GlobalStreamTestState).FullName!;
		var committed = new StoredEvent(
			"evt-1", "agg-1", "TestAggregate", "TestEvent", "data"u8.ToArray(), null, 0, DateTimeOffset.UtcNow)
		{
			GlobalPosition = 500,
		};

		var first = await RunHostAsync(checkpoints, SharedConfiguredName, committed);
		var subscription = first.BoundSubscriptionName;

		(await checkpoints.GetCheckpointAsync(subscription, projectionKey, CancellationToken.None))
			.ShouldBe(
				500,
				"the host must advance the cursor under the SAME pair it bound its member with. A cursor "
				+ "derived any other way is an orphan: read as absent, advanced by nobody else, and left "
				+ "behind by a release that nonetheless reports success.");

		(await checkpoints.ReleaseBoundMemberAsync(subscription, projectionKey, CancellationToken.None))
			.ShouldBeTrue();

		(await checkpoints.GetCheckpointAsync(subscription, projectionKey, CancellationToken.None))
			.ShouldBeNull("the release must clear the cursor, not just the member row.");

		// The consequence a consumer actually depends on: the next start folds from the beginning.
		var second = await RunHostAsync(checkpoints, SharedConfiguredName);

		second.ReadPositions.ShouldNotBeEmpty();
		second.ReadPositions[0].ShouldBe(
			GlobalStreamPosition.Start.Position,
			"after a release the host must fold from the beginning. Resuming at the old position is the "
			+ "failure the release exists to prevent, and it reports success either way.");
	}

	/// <summary>
	/// LIVENESS. A sibling projection's cursor on the same subscription is untouched by that release.
	/// </summary>
	/// <remarks>
	/// A release that cleared the whole subscription would satisfy the safety arm above completely while
	/// destroying every unrelated projection's progress -- the cheapest way to pass and the most
	/// expensive way to be wrong.
	/// </remarks>
	[Fact]
	public async Task LeaveASiblingProjectionsCursorUntouchedWhenThisHostsMemberIsReleased()
	{
		var checkpoints = new InMemorySubscriptionCheckpointStore();
		var projectionKey = typeof(GlobalStreamTestState).FullName!;
		var committed = new StoredEvent(
			"evt-1", "agg-1", "TestAggregate", "TestEvent", "data"u8.ToArray(), null, 0, DateTimeOffset.UtcNow)
		{
			GlobalPosition = 500,
		};

		var run = await RunHostAsync(checkpoints, SharedConfiguredName, committed);
		var subscription = run.BoundSubscriptionName;

		_ = await checkpoints.BindSubscriptionAsync(
			subscription, [Member("Sibling.Projection")], CancellationToken.None);
		_ = await checkpoints.AdvanceBoundCheckpointAsync(
			subscription, "Sibling.Projection", null, 77, CancellationToken.None);

		_ = await checkpoints.ReleaseBoundMemberAsync(subscription, projectionKey, CancellationToken.None);

		(await checkpoints.GetCheckpointAsync(subscription, "Sibling.Projection", CancellationToken.None))
			.ShouldBe(77, "releasing one member must not reset the subscription's other cursors.");
	}
}
