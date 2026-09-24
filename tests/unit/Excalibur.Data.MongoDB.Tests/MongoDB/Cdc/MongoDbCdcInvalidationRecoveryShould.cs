// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Excalibur.Cdc.MongoDB;

using Microsoft.Extensions.Logging.Abstractions;

using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Excalibur.Data.Tests.MongoDB.Cdc;

/// <summary>
/// Holds the MongoDB CDC processor to advancing past an <c>invalidate</c> event instead of reopening the
/// cursor it was already at.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect these arms catch.</b> On an <c>invalidate</c> the processor saved the position it
/// already held — the change <em>before</em> the invalidation — and returned without recording the
/// invalidate event's own token. The driving loop re-entered immediately and rebuilt the stream options
/// from that unchanged checkpoint, with <c>resumeAfter</c>. A real server answers <c>resumeAfter</c> a
/// pre-invalidation token by replaying up to the invalidate and closing again, so the processor reopened,
/// re-read the same invalidate, re-saved the same token, and reopened — forever. Every change made to the
/// namespace after the drop or rename was silently never delivered, and a process restart landed in the
/// same place because the persisted checkpoint was the pre-invalidation one.
/// </para>
/// <para>
/// <b>MongoDB's documented rule.</b> <c>resumeAfter</c> cannot carry a stream past an invalidation;
/// <c>startAfter</c> can, and the two are mutually exclusive on one request. So the fix is not a
/// different token — it is a token <em>and</em> the mode it is usable in, persisted together, because a
/// restarted process reading the token alone would reopen it the wrong way.
/// </para>
/// <para>
/// <b>Why there is a fake server here rather than assertions on option fields.</b> An arm that only
/// checks "<c>StartAfter</c> holds the invalidate token" passes against an implementation that sets the
/// field and never opens anything. <see cref="FakeChangeStreamServer"/> answers each open the way a
/// replica set does — a pre-invalidation <c>resumeAfter</c> replays the invalidate and closes, a
/// post-invalidation <c>startAfter</c> yields what happened next — so the liveness arm below fails, by
/// never delivering the post-invalidation change at all, against the defect as it shipped.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.CDC)]
[Trait(TraitNames.Feature, "Cdc")]
public sealed class MongoDbCdcInvalidationRecoveryShould : UnitTestBase
{
	private const string BeforeToken = "before-invalidate";
	private const string InvalidateToken = "the-invalidate";
	private const string AfterToken = "after-invalidate";

	[Fact]
	public async Task DeliverChangesMadeAfterTheInvalidation()
	{
		// LIVENESS — the arm the bead is actually about. The server model only yields the
		// post-invalidation insert to a stream opened with startAfter at the invalidate token. Against the
		// shipped defect this loops on the invalidate and this assertion is never reached.
		var server = new FakeChangeStreamServer();
		var store = new RecordingStateStore();
		var delivered = new ConcurrentQueue<string>();
		using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));

		var processor = CreateProcessor(server, store);

		await RunUntilAsync(
			processor,
			change =>
			{
				delivered.Enqueue(TokenOf(change.Position) ?? "<none>");
				return delivered.Contains(AfterToken);
			},
			stop.Token);

		delivered.ToArray().ShouldBe(
			new[] { BeforeToken, AfterToken },
			customMessage:
			"a change made after the namespace was invalidated must still reach the handler; the "
			+ "processor reopened the pre-invalidation cursor instead and never got past the invalidate.");
	}

	[Fact]
	public async Task CheckpointTheInvalidateTokenInStartAfterMode()
	{
		// SAFETY. The durable checkpoint must name the invalidation boundary, not the change before it,
		// and must record that it is only usable as a startAfter — the two together are what a cold
		// restart needs.
		var server = new FakeChangeStreamServer();
		var store = new RecordingStateStore();
		using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));

		var processor = CreateProcessor(server, store);

		await RunUntilAsync(
			processor,
			change => change.Position.ResumeToken?["_data"].AsString == AfterToken,
			stop.Token);

		var checkpoints = store.Saved.ToArray();

		checkpoints.ShouldContain(
			p => TokenOf(p) == InvalidateToken && p.ResumeMode == MongoDbChangeStreamResumeMode.StartAfter,
			customMessage:
			"no checkpoint named the invalidation boundary in startAfter mode, so nothing could ever "
			+ "advance past it.");

		checkpoints.ShouldNotContain(
			p => TokenOf(p) == InvalidateToken && p.ResumeMode == MongoDbChangeStreamResumeMode.ResumeAfter,
			customMessage:
			"an invalidate token saved as a resumeAfter position is rejected by the server on the next "
			+ "open; substituting the token without the mode is the half-fix the bead calls insufficient.");
	}

	[Fact]
	public async Task SurviveAProcessRestartAtTheInvalidationBoundary()
	{
		// The restart half of the recovery. A cold start reads the checkpoint back as a STRING through
		// the state store, so the mode has to survive serialization — an in-memory-only flag would pass
		// the arms above and still strand a restarted process before the invalidation.
		var server = new FakeChangeStreamServer();
		var store = new RecordingStateStore();
		using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));

		await RunUntilAsync(
			CreateProcessor(server, store),
			_ => false,
			stop.Token,
			stopOnceStored: store);

		var persisted = store.Saved.Last(p => TokenOf(p) == InvalidateToken);
		var reread = MongoDbCdcPosition.FromString(persisted.TokenString);

		reread.ResumeMode.ShouldBe(
			MongoDbChangeStreamResumeMode.StartAfter,
			"the mode did not survive the round trip through storage, so a restarted process would "
			+ "reopen the invalidate token with resumeAfter and land back before the invalidation.");
		TokenOf(reread).ShouldBe(InvalidateToken);

		// And the restarted processor must actually open that way.
		var restartedServer = new FakeChangeStreamServer();
		var restartedStore = new RecordingStateStore(MongoDbCdcPosition.FromString(persisted.TokenString));
		using var restartStop = new CancellationTokenSource(TimeSpan.FromSeconds(20));
		var deliveredAfterRestart = new ConcurrentQueue<string>();

		await RunUntilAsync(
			CreateProcessor(restartedServer, restartedStore),
			change =>
			{
				deliveredAfterRestart.Enqueue(TokenOf(change.Position) ?? "<none>");
				return true;
			},
			restartStop.Token);

		deliveredAfterRestart.ToArray().ShouldBe(
			new[] { AfterToken },
			customMessage:
			"a restarted process must resume past the invalidation it had already recorded.");
	}

	[Fact]
	public async Task OpenAnOrdinaryCheckpointWithResumeAfter()
	{
		// CONTROL — non-vacuous, and it is the arm that forbids the lazy fix. "Always use startAfter"
		// would satisfy every other arm here while changing how every ordinary reconnect resumes.
		var server = new FakeChangeStreamServer();
		var store = new RecordingStateStore(new MongoDbCdcPosition(TokenFor(BeforeToken)));
		using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));

		await RunUntilAsync(
			CreateProcessor(server, store),
			_ => true,
			stop.Token);

		var firstOpen = server.Opens[0];

		firstOpen.ResumeAfter.ShouldNotBeNull("an ordinary checkpoint resumes with resumeAfter.");
		firstOpen.ResumeAfter!["_data"].AsString.ShouldBe(BeforeToken);
		firstOpen.StartAfter.ShouldBeNull();
	}

	[Fact]
	public async Task NeverSendBothResumeOptionsOnOneOpen()
	{
		// SAFETY. The server rejects a request carrying resumeAfter and startAfter together, which would
		// turn the recovery into a hard failure on the very open that was supposed to fix it.
		var server = new FakeChangeStreamServer();
		var store = new RecordingStateStore();
		using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));

		await RunUntilAsync(
			CreateProcessor(server, store),
			change => TokenOf(change.Position) == AfterToken,
			stop.Token);

		server.Opens.ShouldAllBe(
			o => o.ResumeAfter == null || o.StartAfter == null,
			"resumeAfter and startAfter are mutually exclusive on a change-stream open.");
	}

	[Fact]
	public async Task CheckpointTheInvalidateTokenInStartAfterModeFromTheBatchPathToo()
	{
		// The batch path carried the same defect and is the one a serverless host runs. Consistency with
		// the continuous path is part of the recovery: either both advance past an invalidation or the
		// deployment shape decides whether changes are lost.
		var server = new FakeChangeStreamServer();
		var store = new RecordingStateStore();
		var processor = CreateProcessor(server, store);
		using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));

		_ = await processor.ProcessBatchAsync((_, _) => Task.CompletedTask, stop.Token);

		store.Saved.ShouldContain(
			p => TokenOf(p) == InvalidateToken && p.ResumeMode == MongoDbChangeStreamResumeMode.StartAfter,
			customMessage: "the batch path saved a checkpoint that cannot be reopened past the invalidation.");

		// LIVENESS: the next batch call must then reach the post-invalidation change.
		var received = new List<string?>();
		_ = await processor.ProcessBatchAsync(
			(change, _) =>
			{
				received.Add(TokenOf(change.Position));
				return Task.CompletedTask;
			},
			stop.Token);

		received.ShouldContain(AfterToken, "the second batch never advanced past the invalidation.");
	}

	private static string? TokenOf(MongoDbCdcPosition position) =>
		position.ResumeToken is null ? null : position.ResumeToken["_data"].AsString;

	private static BsonDocument TokenFor(string value) => new() { { "_data", value } };

	private static MongoDbCdcProcessor CreateProcessor(FakeChangeStreamServer server, RecordingStateStore store)
	{
		var options = new MongoDbCdcOptions
		{
			ProcessorId = "invalidation-probe",
			DatabaseName = "cdc",
			CollectionNames = ["source"],
			BatchSize = 100,
			// Short enough that the paced reopen does not dominate the arm, long enough to still be a pace.
			ReconnectInterval = TimeSpan.FromMilliseconds(10),
			ChangeStream = { MaxAwaitTime = TimeSpan.FromMilliseconds(50) },
			Connection = { ConnectionString = "mongodb://localhost:27017/?replicaSet=rs0" },
		};

		return new MongoDbCdcProcessor(
			server.BuildClient(options),
			Options.Create(options),
			store.Store,
			NullLogger<MongoDbCdcProcessor>.Instance);
	}

	/// <summary>
	/// Drives <see cref="MongoDbCdcProcessor.StartAsync"/> until <paramref name="stopWhen"/> returns true
	/// for a delivered change, then cancels it. A bounded wait is the point: against the defect nothing
	/// is ever delivered, and the arm must fail rather than hang.
	/// </summary>
	private static async Task RunUntilAsync(
		MongoDbCdcProcessor processor,
		Func<MongoDbDataChangeEvent, bool> stopWhen,
		CancellationToken timeout,
		RecordingStateStore? stopOnceStored = null)
	{
		using var stop = CancellationTokenSource.CreateLinkedTokenSource(timeout);

		var run = Task.Run(
			async () =>
			{
				try
				{
					await processor.StartAsync(
						(change, _) =>
						{
							if (stopWhen(change))
							{
								stop.Cancel();
							}

							return Task.CompletedTask;
						},
						stop.Token);
				}
				catch (OperationCanceledException)
				{
					// The only way out of the loop.
				}
			},
			CancellationToken.None);

		if (stopOnceStored is not null)
		{
			while (!stop.IsCancellationRequested &&
				   !stopOnceStored.Saved.Any(p => p.ResumeMode == MongoDbChangeStreamResumeMode.StartAfter))
			{
				await Task.Delay(10, CancellationToken.None); // delay-ok: poll pacing; the loop exits on a StartAfter checkpoint being saved, not on elapsed time
			}

			await stop.CancelAsync();
		}

		await run;
		processor.Dispose();
	}

	/// <summary>
	/// Records every checkpoint the processor writes, and hands back the one a restarted process would
	/// read — through the string form, which is what a real store persists.
	/// </summary>
	private sealed class RecordingStateStore
	{
		private readonly ConcurrentQueue<MongoDbCdcPosition> _saved = new();
		private MongoDbCdcPosition _last;

		public RecordingStateStore(MongoDbCdcPosition initial = default)
		{
			_last = initial;

			Store = A.Fake<IMongoDbCdcStateStore>();

			_ = A.CallTo(() => Store.GetLastPositionAsync(A<string>._, A<CancellationToken>._))
				.ReturnsLazily(() => Task.FromResult(_last));

			_ = A.CallTo(() => Store.SavePositionAsync(A<string>._, A<MongoDbCdcPosition>._, A<CancellationToken>._))
				.ReturnsLazily(call =>
				{
					var position = call.GetArgument<MongoDbCdcPosition>(1);
					_saved.Enqueue(position);

					// The round trip is deliberate: a mode held only in memory would pass every arm here
					// and still strand a restarted process.
					_last = MongoDbCdcPosition.FromString(position.TokenString);
					return Task.CompletedTask;
				});
		}

		public IMongoDbCdcStateStore Store { get; }

		public IReadOnlyList<MongoDbCdcPosition> Saved => [.. _saved];
	}

	/// <summary>
	/// A change-stream server that answers an open the way a replica set does across an invalidation.
	/// </summary>
	/// <remarks>
	/// Three events exist on the namespace: an insert, an <c>invalidate</c>, and an insert made after the
	/// namespace came back. Opens are answered on MongoDB's documented rules — a fresh open or a
	/// <c>resumeAfter</c> from before the invalidation runs up to and including the invalidate and then
	/// ends; only a <c>startAfter</c> at the invalidate token sees what happened next; <c>resumeAfter</c>
	/// at the invalidate token is an error, as it is on a real server.
	/// </remarks>
	private sealed class FakeChangeStreamServer
	{
		private readonly ConcurrentQueue<ChangeStreamOptions> _opens = new();

		public IReadOnlyList<ChangeStreamOptions> Opens => [.. _opens];

		public IMongoClient BuildClient(MongoDbCdcOptions options)
		{
			var collection = A.Fake<IMongoCollection<BsonDocument>>();
			_ = A.CallTo(
					() => collection.WatchAsync(
						A<PipelineDefinition<ChangeStreamDocument<BsonDocument>, ChangeStreamDocument<BsonDocument>>>._,
						A<ChangeStreamOptions>._,
						A<CancellationToken>._))
				.ReturnsLazily(call => Task.FromResult(Open(call.GetArgument<ChangeStreamOptions>(1)!)));

			var database = A.Fake<IMongoDatabase>();
			_ = A.CallTo(() => database.GetCollection<BsonDocument>(A<string>._, A<MongoCollectionSettings>._))
				.Returns(collection);

			var client = A.Fake<IMongoClient>();
			_ = A.CallTo(() => client.GetDatabase(options.DatabaseName, A<MongoDatabaseSettings>._))
				.Returns(database);

			return client;
		}

		private IChangeStreamCursor<ChangeStreamDocument<BsonDocument>> Open(ChangeStreamOptions options)
		{
			_opens.Enqueue(options);

			var resumeAfter = options.ResumeAfter?["_data"].AsString;
			var startAfter = options.StartAfter?["_data"].AsString;

			if (resumeAfter is not null && startAfter is not null)
			{
				throw new InvalidOperationException(
					"resumeAfter and startAfter cannot both be supplied to one change-stream open.");
			}

			if (resumeAfter == InvalidateToken)
			{
				throw new InvalidOperationException(
					"resumeAfter cannot reopen a change stream past an invalidate event.");
			}

			// Everything except a startAfter at the invalidation boundary lands before it, and runs up to
			// the invalidate again.
			ChangeStreamDocument<BsonDocument>[] batch = startAfter == InvalidateToken
				? [Insert(AfterToken)]
				: resumeAfter == AfterToken
					? []
					: resumeAfter == BeforeToken
						? [Invalidate()]
						: [Insert(BeforeToken), Invalidate()];

			return Cursor(batch);
		}

		private static IChangeStreamCursor<ChangeStreamDocument<BsonDocument>> Cursor(
			IReadOnlyList<ChangeStreamDocument<BsonDocument>> batch)
		{
			var cursor = A.Fake<IChangeStreamCursor<ChangeStreamDocument<BsonDocument>>>();
			var served = 0;

			_ = A.CallTo(() => cursor.MoveNextAsync(A<CancellationToken>._))
				.ReturnsLazily(() => Task.FromResult(Interlocked.Exchange(ref served, 1) == 0));
			_ = A.CallTo(() => cursor.Current).ReturnsLazily(() => batch);

			return cursor;
		}

		private static ChangeStreamDocument<BsonDocument> Insert(string token) =>
			Change(token, "insert", new BsonDocument { { "_id", token } });

		private static ChangeStreamDocument<BsonDocument> Invalidate() =>
			Change(InvalidateToken, "invalidate", fullDocument: null);

		private static ChangeStreamDocument<BsonDocument> Change(
			string token,
			string operationType,
			BsonDocument? fullDocument)
		{
			var backing = new BsonDocument
			{
				{ "_id", new BsonDocument { { "_data", token } } },
				{ "operationType", operationType },
				{ "ns", new BsonDocument { { "db", "cdc" }, { "coll", "source" } } },
			};

			if (fullDocument is not null)
			{
				backing.Add("documentKey", new BsonDocument { { "_id", token } });
				backing.Add("fullDocument", fullDocument);
			}

			return new ChangeStreamDocument<BsonDocument>(
				backing,
				BsonSerializer.LookupSerializer<BsonDocument>());
		}
	}
}
