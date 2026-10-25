// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Redis;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using StackExchange.Redis;

using Tests.Shared.Conformance.EventStore;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Binds the commit-certainty outcome of a Redis append whose reply is lost.
/// </summary>
/// <remarks>
/// <para>
/// This store is the clearest case in the set, and it is worth saying why rather than leaving it to look
/// like the others. The whole append is ONE Lua script evaluation, which Redis runs atomically on the
/// server. So once the script has been dispatched there are exactly two possibilities — it ran, or it did
/// not — and a lost reply cannot distinguish them. There is no partial state for a probe to measure, so
/// <see cref="AppendOutcome.Unknown"/> is not a fallback here: it is the precise answer, and the dispatch
/// guard alone is sufficient for this store to be honest.
/// </para>
/// </remarks>
[Collection(RedisEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "EventStore")]
[Trait("Pattern", "FaultInjection")]
public sealed class RedisEventStoreCommitOutcomeShould(RedisEventStoreContainerFixture fixture)
{
	/// <summary>
	/// Verifies a lost script reply is reported as Unknown, never as Failed.
	/// </summary>
	/// <param name="applied">Whether the injected fault is raised after the script really ran.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReportUnknownWhenTheScriptReplyIsLost(bool applied)
	{
		fixture.DockerAvailable.ShouldBeTrue("Commit-outcome evidence requires real Redis; never skip.");

		await using var connection = await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString)
			.ConfigureAwait(true);

		var id = Guid.NewGuid().ToString("N");
		var store = new RedisEventStore(
			connection,
			Options.Create(new RedisEventStoreOptions()),
			NullLogger<RedisEventStore>.Instance,
			SingleTenantTestContext.Instance)
		{
			ScriptEvaluateInterceptor = async evaluate =>
			{
				if (applied)
				{
					// The REAL evaluation, so the applied case is genuinely durable and a Failed report
					// would be a falsehood about state the caller can go and read.
					_ = await evaluate().ConfigureAwait(false);
				}

				// A RedisException specifically, and the type matters more than it looks. The store filters
				// its terminal handler on RedisException, so that is the fault shape which exercises the
				// !state.Dispatched guard. An earlier version of this arm threw RedisTimeoutException --
				// which derives from TimeoutException, NOT RedisException -- so it sailed past that handler
				// entirely and was answered by the commit boundary instead. The arm still passed, and a
				// mutation removing the store guard did not redden it: it was binding the boundary while
				// claiming to bind the guard. Both paths end in Unknown in production, which is why the
				// mislabelling was invisible until the mutation was run.
				throw new RedisException("Injected loss of script reply");
			},
		};

		TestDomainEvent[] events = [new() { AggregateId = id, Data = "stable-payload" }];
		var result = await store.AppendAsync(id, "CommitOutcome", events, -1, CancellationToken.None)
			.ConfigureAwait(true);

		// THE INVARIANT: never Failed. The script was sent, so "nothing was written" is exactly the one
		// thing this handler cannot establish.
		result.Outcome.ShouldNotBe(
			AppendOutcome.Failed,
			"Failed asserts nothing was written, which this store cannot establish once the script was sent");
		result.Outcome.ShouldBe(AppendOutcome.Unknown, "an atomic script with a lost reply is precisely indeterminate");
		result.IsConcurrencyConflict.ShouldBeFalse("no other writer took this version");

		// LIVENESS: the event really is durable when the fault followed a real evaluation.
		var loaded = await store.LoadAsync(id, "CommitOutcome", CancellationToken.None).ConfigureAwait(true);
		loaded.Count.ShouldBe(applied ? 1 : 0);
	}
}
