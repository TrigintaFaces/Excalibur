// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Cdc;
using Excalibur.Cdc.MongoDB;

using Microsoft.Extensions.Logging.Abstractions;

using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Excalibur.Data.Tests.MongoDB.Cdc;

/// <summary>
/// A change stream that fails on every attempt is retried with backoff, and stops when, and only when, the
/// consumer configured a limit.
/// </summary>
/// <remarks>
/// The failure used is one the framework does not recognise, which is classified transient by design. That
/// is the case that used to retry forever at a fixed interval with nothing reporting it.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Cdc")]
public sealed class MongoDbCdcReconnectBoundShould
{
	/// <summary>
	/// SAFETY. With a limit, the processor stops on the attempt that reaches it and says why.
	/// </summary>
	/// <remarks>RED against a loop with no count: it never stops, and the bounded wait fails the arm.</remarks>
	[Fact]
	public async Task StopWithRetryExhausted_OnTheAttemptThatReachesTheLimit()
	{
		var stream = new AlwaysFailingChangeStream();
		var processor = CreateProcessor(stream, new CdcFatalErrorOptions<MongoDbDataChangeEvent>
		{
			MaxConsecutiveTransientFailures = 3,
			MaxReconnectDelay = TimeSpan.FromMilliseconds(20),
		});
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

		var thrown = await Should.ThrowAsync<CdcRetryExhaustedException>(
			() => processor.StartAsync((_, _) => Task.CompletedTask, timeout.Token));

		thrown.ConsecutiveFailures.ShouldBe(3);
		_ = thrown.InnerException.ShouldBeOfType<TimeoutException>();
		stream.Opens.ShouldBe(3, "the processor must stop on the third failure, not retry a fourth time");
	}

	/// <summary>
	/// SAFETY. A configured fatal-error handler receives the exhaustion, and the processor stops.
	/// </summary>
	[Fact]
	public async Task HandTheExhaustionToTheFatalErrorHandler_WhenOneIsConfigured()
	{
		var stream = new AlwaysFailingChangeStream();
		Exception? handled = null;
		var processor = CreateProcessor(stream, new CdcFatalErrorOptions<MongoDbDataChangeEvent>
		{
			MaxConsecutiveTransientFailures = 2,
			MaxReconnectDelay = TimeSpan.FromMilliseconds(20),
			OnFatalError = (ex, _) =>
			{
				handled = ex;
				return Task.CompletedTask;
			},
		});
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

		await processor.StartAsync((_, _) => Task.CompletedTask, timeout.Token);

		timeout.IsCancellationRequested.ShouldBeFalse("the processor must stop by itself, not by the test's timeout");
		_ = handled.ShouldBeOfType<CdcRetryExhaustedException>();
		stream.Opens.ShouldBe(2);
	}

	/// <summary>
	/// LIVENESS. Without a limit the processor keeps reconnecting.
	/// </summary>
	/// <remarks>
	/// The default must not change behaviour for a consumer who configured nothing. RED against a loop that
	/// stops on some built-in count.
	/// </remarks>
	[Fact]
	public async Task KeepReconnecting_WhenNoLimitIsSet()
	{
		var stream = new AlwaysFailingChangeStream();
		var processor = CreateProcessor(stream, new CdcFatalErrorOptions<MongoDbDataChangeEvent>
		{
			MaxReconnectDelay = TimeSpan.FromMilliseconds(20),
		});
		using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		stream.StopAfter(20, stop);

		_ = await Should.ThrowAsync<OperationCanceledException>(
			() => processor.StartAsync((_, _) => Task.CompletedTask, stop.Token));

		stream.Opens.ShouldBeGreaterThanOrEqualTo(20);
	}

	/// <summary>
	/// SAFETY. When the limit is reached on a change whose handler keeps failing, the fatal-error handler receives
	/// that change, so it can be dead-lettered.
	/// </summary>
	/// <remarks>
	/// RED against clearing the in-flight change before counting the failure: the limit is then reached with no
	/// change to hand over, and the handler receives <see langword="null"/>.
	/// </remarks>
	[Fact]
	public async Task HandThePoisonedChangeToTheFatalErrorHandler_WhenTheLimitIsReachedOnIt()
	{
		MongoDbDataChangeEvent? handedOver = null;
		var options = Options();
		var store = A.Fake<IMongoDbCdcStateStore>();
		_ = A.CallTo(() => store.GetLastPositionAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(MongoDbCdcPosition.Start));

		var processor = new MongoDbCdcProcessor(
			PoisonedChangeStream(options),
			Microsoft.Extensions.Options.Options.Create(options),
			store,
			NullLogger<MongoDbCdcProcessor>.Instance,
			Microsoft.Extensions.Options.Options.Create(new CdcFatalErrorOptions<MongoDbDataChangeEvent>
			{
				MaxConsecutiveTransientFailures = 2,
				MaxReconnectDelay = TimeSpan.FromMilliseconds(20),
				OnFatalError = (_, change) =>
				{
					handedOver = change;
					return Task.CompletedTask;
				},
			}));
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

		await processor.StartAsync(
			(_, _) => throw new TimeoutException("the downstream system did not answer"),
			timeout.Token);

		timeout.IsCancellationRequested.ShouldBeFalse("the processor must stop by itself at the limit");
		_ = handedOver.ShouldNotBeNull("the change whose handler kept failing must reach the fatal-error handler");
	}

	private static MongoDbCdcOptions Options() => new()
	{
		ProcessorId = "reconnect-bound-probe",
		DatabaseName = "cdc",
		CollectionNames = ["source"],
		BatchSize = 100,
		ReconnectInterval = TimeSpan.FromMilliseconds(10),
		ChangeStream = { MaxAwaitTime = TimeSpan.FromMilliseconds(50) },
		Connection = { ConnectionString = "mongodb://localhost:27017/?replicaSet=rs0" },
	};

	/// <summary>A change stream whose every open delivers the same insert.</summary>
	private static IMongoClient PoisonedChangeStream(MongoDbCdcOptions options)
	{
		var collection = A.Fake<IMongoCollection<BsonDocument>>();
		_ = A.CallTo(
				() => collection.WatchAsync(
					A<PipelineDefinition<ChangeStreamDocument<BsonDocument>, ChangeStreamDocument<BsonDocument>>>._,
					A<ChangeStreamOptions>._,
					A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var backing = new BsonDocument
				{
					{ "_id", new BsonDocument { { "_data", "poison" } } },
					{ "operationType", "insert" },
					{ "ns", new BsonDocument { { "db", "cdc" }, { "coll", "source" } } },
					{ "documentKey", new BsonDocument { { "_id", "poison" } } },
					{ "fullDocument", new BsonDocument { { "_id", "poison" } } },
				};
				IReadOnlyList<ChangeStreamDocument<BsonDocument>> batch =
					[new ChangeStreamDocument<BsonDocument>(backing, BsonSerializer.LookupSerializer<BsonDocument>())];

				var cursor = A.Fake<IChangeStreamCursor<ChangeStreamDocument<BsonDocument>>>();
				var served = 0;
				_ = A.CallTo(() => cursor.MoveNextAsync(A<CancellationToken>._))
					.ReturnsLazily(() => Task.FromResult(Interlocked.Exchange(ref served, 1) == 0));
				_ = A.CallTo(() => cursor.Current).ReturnsLazily(() => batch);
				return Task.FromResult(cursor);
			});

		var database = A.Fake<IMongoDatabase>();
		_ = A.CallTo(() => database.GetCollection<BsonDocument>(A<string>._, A<MongoCollectionSettings>._))
			.Returns(collection);

		var client = A.Fake<IMongoClient>();
		_ = A.CallTo(() => client.GetDatabase(options.DatabaseName, A<MongoDatabaseSettings>._))
			.Returns(database);

		return client;
	}

	private static MongoDbCdcProcessor CreateProcessor(
		AlwaysFailingChangeStream stream,
		CdcFatalErrorOptions<MongoDbDataChangeEvent> fatalErrorOptions)
	{
		var options = new MongoDbCdcOptions
		{
			ProcessorId = "reconnect-bound-probe",
			DatabaseName = "cdc",
			CollectionNames = ["source"],
			BatchSize = 100,
			ReconnectInterval = TimeSpan.FromMilliseconds(10),
			ChangeStream = { MaxAwaitTime = TimeSpan.FromMilliseconds(50) },
			Connection = { ConnectionString = "mongodb://localhost:27017/?replicaSet=rs0" },
		};

		var store = A.Fake<IMongoDbCdcStateStore>();
		_ = A.CallTo(() => store.GetLastPositionAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(MongoDbCdcPosition.Start));

		return new MongoDbCdcProcessor(
			stream.BuildClient(options),
			Options.Create(options),
			store,
			NullLogger<MongoDbCdcProcessor>.Instance,
			Options.Create(fatalErrorOptions));
	}

	/// <summary>
	/// A change stream whose every open fails with an exception the framework does not classify, and which
	/// counts the opens.
	/// </summary>
	private sealed class AlwaysFailingChangeStream
	{
		private int _opens;
		private int _stopAfter = int.MaxValue;
		private CancellationTokenSource? _stop;

		public int Opens => Volatile.Read(ref _opens);

		public void StopAfter(int opens, CancellationTokenSource stop)
		{
			_stopAfter = opens;
			_stop = stop;
		}

		public IMongoClient BuildClient(MongoDbCdcOptions options)
		{
			var collection = A.Fake<IMongoCollection<BsonDocument>>();
			_ = A.CallTo(
					() => collection.WatchAsync(
						A<PipelineDefinition<ChangeStreamDocument<BsonDocument>, ChangeStreamDocument<BsonDocument>>>._,
						A<ChangeStreamOptions>._,
						A<CancellationToken>._))
				.ReturnsLazily(() =>
				{
					if (Interlocked.Increment(ref _opens) >= _stopAfter)
					{
						_stop?.Cancel();
					}

					return Task.FromException<IChangeStreamCursor<ChangeStreamDocument<BsonDocument>>>(
						new TimeoutException("the change stream could not be opened"));
				});

			var database = A.Fake<IMongoDatabase>();
			_ = A.CallTo(() => database.GetCollection<BsonDocument>(A<string>._, A<MongoCollectionSettings>._))
				.Returns(collection);

			var client = A.Fake<IMongoClient>();
			_ = A.CallTo(() => client.GetDatabase(options.DatabaseName, A<MongoDatabaseSettings>._))
				.Returns(database);

			return client;
		}
	}
}
