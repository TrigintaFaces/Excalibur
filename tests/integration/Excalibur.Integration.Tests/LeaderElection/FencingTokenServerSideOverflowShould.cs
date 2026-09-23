// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.MongoDB;
using Excalibur.LeaderElection.Postgres;
using Excalibur.LeaderElection.Redis;
using Excalibur.LeaderElection.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MongoDB.Bson;
using MongoDB.Driver;

using Npgsql;

using StackExchange.Redis;

using Tests.Shared.Fixtures;
using Tests.Shared.Infrastructure;

namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>
/// f559sr: real-infra lock for SERVER-SIDE fencing-token overflow. The impl side (translating an exhausted
/// counter into <see cref="FencingTokenExhaustedException"/> rather than wrapping) landed in S894; this is
/// the real-infra lock that proves it against the actual store's own ceiling behaviour, which a mocked
/// client cannot produce because it returns whatever it is told.
/// </summary>
/// <remarks>
/// <para>
/// Scope: Redis, MongoDB, Postgres -- the bead's stated minimum -- plus SQL Server (a real container was
/// already available and its ceiling mechanism is cheap to cover with the same pattern as Postgres). Each
/// store's OWN ceiling mechanism is genuinely different and is exercised as such, not forced into one shape:
/// </para>
/// <list type="bullet">
/// <item><b>Redis</b> -- <c>INCR</c> raises a native <c>RedisServerException</c> ("increment or decrement
/// would overflow") when the counter is at <see cref="long.MaxValue"/>; the provider translates it and
/// preserves it as <see cref="Exception.InnerException"/>.</item>
/// <item><b>MongoDB</b> -- measured against a real server (not the assumption in an earlier draft of
/// this file): <c>$inc</c> does NOT silently wrap past <see cref="long.MaxValue"/>. The server rejects the
/// operation with a native <c>MongoCommandException</c> ("Failed to apply $inc operations to current
/// value..."), which the provider translates and preserves as <see cref="Exception.InnerException"/>, same
/// as Redis and Postgres.</item>
/// <item><b>Postgres</b> -- a <c>NO CYCLE bigint SEQUENCE</c> raises a native <c>PostgresException</c>
/// (SQLSTATE <c>2200H</c>, <c>sequence_generator_limit_exceeded</c>) at its ceiling; the provider translates
/// it and preserves it as <see cref="Exception.InnerException"/>.</item>
/// <item><b>SQL Server</b> -- also a <c>NO CYCLE bigint SEQUENCE</c>. Measured against a real server
/// (f559sr): the ceiling error is <c>SqlException</c> number <b>11728</b> ("has reached its minimum or
/// maximum value"), not 11732 -- the number an earlier draft of the provider checked, which would have let
/// a real overflow escape untranslated. Fixed as part of this bead.</item>
/// </list>
/// <para>
/// Each store's counter is seeded directly at (or one below) its real ceiling before the mint under test,
/// rather than looping the mint ~2^63 times to reach it -- the ceiling behaviour itself is what is under
/// test, not how many calls it takes to reach it. <see cref="ContainerFixtureBase.DockerAvailable"/> is
/// asserted true in every fact -- never skipped, per team-lead direction that a skip is not a pass here.
/// </para>
/// <para>
/// <b>RED-on-mutant:</b> if a provider's overflow branch were removed (letting the raw store exception, or
/// a wrapped/negative token, escape untranslated), the exhaustion facts below would either throw the wrong
/// exception type or -- worse -- silently return a non-monotonic token, which
/// <see cref="RedisFencingTokenExhaustion_ThrowsWithNativeInnerException"/> et al. would catch as a
/// wrong-exception-type failure.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
public sealed class FencingTokenServerSideOverflowShould :
	IClassFixture<RedisContainerFixture>,
	IClassFixture<MongoDbContainerFixture>,
	IClassFixture<PostgresContainerFixture>,
	IClassFixture<SqlServerContainerFixture>
{
	private readonly RedisContainerFixture _redisFixture;
	private readonly MongoDbContainerFixture _mongoFixture;
	private readonly PostgresContainerFixture _postgresFixture;
	private readonly SqlServerContainerFixture _sqlServerFixture;

	public FencingTokenServerSideOverflowShould(
		RedisContainerFixture redisFixture,
		MongoDbContainerFixture mongoFixture,
		PostgresContainerFixture postgresFixture,
		SqlServerContainerFixture sqlServerFixture)
	{
		_redisFixture = redisFixture;
		_mongoFixture = mongoFixture;
		_postgresFixture = postgresFixture;
		_sqlServerFixture = sqlServerFixture;
	}

	private static string UniqueResourceId(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

	// ---------------------------------------------------------------------------------------------
	// Redis
	// ---------------------------------------------------------------------------------------------

	[Fact]
	public async Task RedisFencingTokenExhaustion_ThrowsWithNativeInnerException()
	{
		_redisFixture.DockerAvailable.ShouldBeTrue("f559sr real-infra overflow coverage must never be skipped");

		var resourceId = UniqueResourceId("redis-overflow");
		var muxer = await ConnectionMultiplexer.ConnectAsync(_redisFixture.ConnectionString);
		await using var _muxer = muxer;

		// Seed the real counter one below Redis's int64 ceiling, so the NEXT real INCR is the one that
		// genuinely overflows server-side.
		var db = muxer.GetDatabase();
		await db.StringSetAsync("fencing:" + resourceId, long.MaxValue.ToString(CultureInfo.InvariantCulture));

		var services = new ServiceCollection();
		_ = services.AddSingleton<IConnectionMultiplexer>(muxer);
		_ = services.AddRedisFencingTokenProvider();
		await using var provider = services.BuildServiceProvider();
		var fencingProvider = provider.GetRequiredService<IFencingTokenProvider>();

		// LIVENESS (below ceiling first, on a DIFFERENT resource): the same provider still mints normally.
		var livenessResourceId = UniqueResourceId("redis-liveness");
		var first = await fencingProvider.IssueTokenAsync(livenessResourceId, TestContext.Current.CancellationToken);
		var second = await fencingProvider.IssueTokenAsync(livenessResourceId, TestContext.Current.CancellationToken);
		first.ShouldBe(1L);
		second.ShouldBeGreaterThan(first);

		// SAFETY -- the real overflow.
		var ex = await Should.ThrowAsync<FencingTokenExhaustedException>(
			() => fencingProvider.IssueTokenAsync(resourceId, TestContext.Current.CancellationToken).AsTask());
		ex.InnerException.ShouldBeOfType<RedisServerException>(
			"Redis's own INCR overflow error must be preserved as the inner exception, not swallowed");
	}

	[Fact]
	public async Task RedisLeaderElection_Relinquishes_WhenFencingTokenIsExhausted()
	{
		_redisFixture.DockerAvailable.ShouldBeTrue("f559sr real-infra overflow coverage must never be skipped");

		var lockKey = UniqueResourceId("redis-election-overflow");
		var muxer = await ConnectionMultiplexer.ConnectAsync(_redisFixture.ConnectionString);
		await using var _muxer = muxer;

		var db = muxer.GetDatabase();
		await db.StringSetAsync("fencing:" + lockKey, long.MaxValue.ToString(CultureInfo.InvariantCulture));

		var services = new ServiceCollection();
		_ = services.AddSingleton<IConnectionMultiplexer>(muxer);
		_ = services.AddRedisFencingTokenProvider();
		await using var provider = services.BuildServiceProvider();
		var fencingProvider = provider.GetRequiredService<IFencingTokenProvider>();

		var election = new RedisLeaderElection(
			muxer,
			lockKey,
			Options.Create(new LeaderElectionOptions { InstanceId = "overflow-candidate" }),
			NullLogger<RedisLeaderElection>.Instance,
			new RedisLeaderElectionContext { FencingTokenProvider = fencingProvider });

		// The attempt's outcome is observable on the PUBLIC surface: every provider raises
		// AcquisitionFailed when a mint it cannot advance forces it to relinquish. Subscribe before
		// StartAsync so the first attempt cannot be missed.
		var acquisitionFailed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var failureReason = string.Empty;
		election.AcquisitionFailed += (_, args) =>
		{
			failureReason = args.Reason;
			_ = acquisitionFailed.TrySetResult();
		};

		try
		{
			await election.StartAsync(TestContext.Current.CancellationToken);

			// Observe the acquisition attempt through the public AcquisitionFailed event rather than
			// sleeping. The 500ms this replaces was REDUNDANT, not load-bearing: every provider here
			// awaits its first acquire inside StartAsync (Redis:168, Postgres/SqlServer/MongoDB
			// likewise), so IsLeader is already decided on return. What the event buys is strength, not
			// safety -- IsLeader.ShouldBeFalse alone is also satisfied by an election that never
			// acquired for some unrelated reason, so the arm below pins WHY the attempt was abandoned.
			await WaitHelpers.AwaitSignalAsync(
				acquisitionFailed.Task,
				TimeSpan.FromSeconds(60),
				cancellationToken: TestContext.Current.CancellationToken);
			failureReason.Contains("fencing", StringComparison.OrdinalIgnoreCase).ShouldBeTrue(
				$"the attempt must have been abandoned because the fencing MINT was exhausted, but the "
				+ $"reported reason was '{failureReason}' -- any other reason means this arm observed an "
				+ "unrelated failure and proves nothing about overflow.");

			// ELECTION-LEVEL ARM: an exhausted mint must relinquish -- the leadership attempt must NOT
			// declare leadership with an un-advanced (or wrapped) fence.
			election.IsLeader.ShouldBeFalse(
				"a leadership attempt whose fencing mint overflows must relinquish rather than lead with an un-advanced fence");
			election.CurrentLeadership.ShouldBeNull();
		}
		finally
		{
			await election.DisposeAsync();
		}
	}

	// ---------------------------------------------------------------------------------------------
	// MongoDB
	// ---------------------------------------------------------------------------------------------

	private IMongoCollection<BsonDocument> FencingCollection(IMongoClient client, string databaseName) =>
		client.GetDatabase(databaseName).GetCollection<BsonDocument>("leader_elections_fencing");

	[Fact]
	public async Task MongoDbFencingTokenExhaustion_ThrowsWhenTheCounterWraps()
	{
		_mongoFixture.DockerAvailable.ShouldBeTrue("f559sr real-infra overflow coverage must never be skipped");

		var databaseName = "fence_overflow_" + Guid.NewGuid().ToString("N");
		var client = new MongoClient(_mongoFixture.ConnectionString);
		var resourceId = UniqueResourceId("mongo-overflow");

		// Seed the real counter document at the int64 ceiling, so the NEXT real $inc genuinely wraps.
		await FencingCollection(client, databaseName).InsertOneAsync(
			new BsonDocument { { "_id", resourceId }, { "Seq", long.MaxValue } },
			cancellationToken: TestContext.Current.CancellationToken);

		var services = new ServiceCollection();
		_ = services.AddSingleton<IMongoClient>(client);
		_ = services.Configure<MongoDbLeaderElectionOptions>(o =>
		{
			o.DatabaseName = databaseName;
			o.CollectionName = "leader_elections";
		});
		_ = services.AddMongoDbFencingTokenProvider();
		await using var provider = services.BuildServiceProvider();
		var fencingProvider = provider.GetRequiredService<IFencingTokenProvider>();

		// LIVENESS (below ceiling, different resource): the same provider still mints normally.
		var livenessResourceId = UniqueResourceId("mongo-liveness");
		var first = await fencingProvider.IssueTokenAsync(livenessResourceId, TestContext.Current.CancellationToken);
		var second = await fencingProvider.IssueTokenAsync(livenessResourceId, TestContext.Current.CancellationToken);
		first.ShouldBe(1L);
		second.ShouldBeGreaterThan(first);

		// SAFETY -- the real overflow. MongoDB does NOT silently wrap $inc past long.MaxValue: the server
		// rejects the operation with a native MongoCommandException, which the provider now translates.
		var ex = await Should.ThrowAsync<FencingTokenExhaustedException>(
			() => fencingProvider.IssueTokenAsync(resourceId, TestContext.Current.CancellationToken).AsTask());
		ex.InnerException.ShouldBeOfType<MongoCommandException>(
			"MongoDB's own $inc-overflow-rejection error must be preserved as the inner exception, not swallowed");
	}

	[Fact]
	public async Task MongoDbLeaderElection_Relinquishes_WhenFencingTokenIsExhausted()
	{
		_mongoFixture.DockerAvailable.ShouldBeTrue("f559sr real-infra overflow coverage must never be skipped");

		var databaseName = "fence_overflow_election_" + Guid.NewGuid().ToString("N");
		var client = new MongoClient(_mongoFixture.ConnectionString);
		var resourceName = UniqueResourceId("mongo-election-overflow");

		await FencingCollection(client, databaseName).InsertOneAsync(
			new BsonDocument { { "_id", resourceName }, { "Seq", long.MaxValue } },
			cancellationToken: TestContext.Current.CancellationToken);

		var services = new ServiceCollection();
		_ = services.AddSingleton<IMongoClient>(client);
		_ = services.Configure<MongoDbLeaderElectionOptions>(o =>
		{
			o.DatabaseName = databaseName;
			o.CollectionName = "leader_elections";
		});
		_ = services.AddMongoDbFencingTokenProvider();
		await using var provider = services.BuildServiceProvider();
		var fencingProvider = provider.GetRequiredService<IFencingTokenProvider>();

		var election = new MongoDbLeaderElection(
			client,
			resourceName,
			Options.Create(new MongoDbLeaderElectionOptions
			{
				ConnectionString = _mongoFixture.ConnectionString,
				DatabaseName = databaseName,
				CollectionName = "leader_elections",
			}),
			Options.Create(new LeaderElectionOptions { InstanceId = "overflow-candidate" }),
			NullLogger<MongoDbLeaderElection>.Instance,
			fencingProvider);

		// The attempt's outcome is observable on the PUBLIC surface: every provider raises
		// AcquisitionFailed when a mint it cannot advance forces it to relinquish. Subscribe before
		// StartAsync so the first attempt cannot be missed.
		var acquisitionFailed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var failureReason = string.Empty;
		election.AcquisitionFailed += (_, args) =>
		{
			failureReason = args.Reason;
			_ = acquisitionFailed.TrySetResult();
		};

		try
		{
			await election.StartAsync(TestContext.Current.CancellationToken);

			// Observe the acquisition attempt through the public AcquisitionFailed event rather than
			// sleeping. The 500ms this replaces was REDUNDANT, not load-bearing: every provider here
			// awaits its first acquire inside StartAsync (Redis:168, Postgres/SqlServer/MongoDB
			// likewise), so IsLeader is already decided on return. What the event buys is strength, not
			// safety -- IsLeader.ShouldBeFalse alone is also satisfied by an election that never
			// acquired for some unrelated reason, so the arm below pins WHY the attempt was abandoned.
			await WaitHelpers.AwaitSignalAsync(
				acquisitionFailed.Task,
				TimeSpan.FromSeconds(60),
				cancellationToken: TestContext.Current.CancellationToken);
			failureReason.Contains("fencing", StringComparison.OrdinalIgnoreCase).ShouldBeTrue(
				$"the attempt must have been abandoned because the fencing MINT was exhausted, but the "
				+ $"reported reason was '{failureReason}' -- any other reason means this arm observed an "
				+ "unrelated failure and proves nothing about overflow.");

			election.IsLeader.ShouldBeFalse(
				"a leadership attempt whose fencing mint wraps past the int64 ceiling must relinquish rather than lead with an un-advanced fence");
			election.CurrentLeadership.ShouldBeNull();
		}
		finally
		{
			await election.DisposeAsync();
		}
	}

	// ---------------------------------------------------------------------------------------------
	// Postgres
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Mirrors <c>PostgresFencingTokenProvider.SequenceName</c> exactly (documented on that type's own
	/// remarks: <c>"fencing_"</c> + the first 55 lowercase-hex chars of the resource id's SHA-256) so this
	/// test can address the same real sequence the provider creates, without needing internals access.
	/// </summary>
	private static string PostgresSequenceName(string resourceId)
	{
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(resourceId));
		return "fencing_" + Convert.ToHexStringLower(hash)[..55];
	}

	[Fact]
	[SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
		Justification = "sequenceName comes from PostgresSequenceName(), which is 'fencing_' + lowercase-hex " +
			"SHA-256 of the resource id -- exclusively [0-9a-f], never raw input, mirroring the production " +
			"provider's own identical justification. ALTER SEQUENCE cannot parameterize an identifier.")]
	public async Task PostgresFencingTokenExhaustion_ThrowsWithNativeInnerException()
	{
		_postgresFixture.DockerAvailable.ShouldBeTrue("f559sr real-infra overflow coverage must never be skipped");

		var resourceId = UniqueResourceId("pg-overflow");

		var services = new ServiceCollection();
		_ = services.AddPostgresFencingTokenProvider(_postgresFixture.ConnectionString);
		await using var provider = services.BuildServiceProvider();
		var fencingProvider = provider.GetRequiredService<IFencingTokenProvider>();

		// Mint once first so the provider creates the real sequence, then seed it one below the ceiling
		// with a direct ALTER SEQUENCE against the real database, so the NEXT nextval() genuinely exceeds
		// a NO CYCLE bigint sequence's limit.
		_ = await fencingProvider.IssueTokenAsync(resourceId, TestContext.Current.CancellationToken);

		await using (var connection = new NpgsqlConnection(_postgresFixture.ConnectionString))
		{
			await connection.OpenAsync(TestContext.Current.CancellationToken);
			var sequenceName = PostgresSequenceName(resourceId);
			await using var alter = new NpgsqlCommand(
				$"ALTER SEQUENCE {sequenceName} RESTART WITH 9223372036854775807", connection);
			await alter.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
		}

		// RESTART WITH sets the value the NEXT nextval() returns, not the value AFTER which it errors --
		// consume it first (this call succeeds, returning long.MaxValue itself, still a valid bigint) so
		// the mint that follows is the one that genuinely tries to advance past the ceiling.
		var atCeiling = await fencingProvider.IssueTokenAsync(resourceId, TestContext.Current.CancellationToken);
		atCeiling.ShouldBe(long.MaxValue);

		// LIVENESS (below ceiling, different resource): the same provider still mints normally.
		var livenessResourceId = UniqueResourceId("pg-liveness");
		var first = await fencingProvider.IssueTokenAsync(livenessResourceId, TestContext.Current.CancellationToken);
		var second = await fencingProvider.IssueTokenAsync(livenessResourceId, TestContext.Current.CancellationToken);
		first.ShouldBe(1L);
		second.ShouldBeGreaterThan(first);

		// SAFETY -- the real ceiling. The sequence is now sitting AT long.MaxValue, so the next nextval()
		// call must exceed a NO CYCLE bigint sequence's limit and raise PostgresException 2200H.
		var ex = await Should.ThrowAsync<FencingTokenExhaustedException>(
			() => fencingProvider.IssueTokenAsync(resourceId, TestContext.Current.CancellationToken).AsTask());
		ex.InnerException.ShouldBeOfType<PostgresException>(
			"Postgres's own sequence_generator_limit_exceeded (2200H) error must be preserved as the inner exception");
	}

	[Fact]
	[SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
		Justification = "sequenceName comes from PostgresSequenceName(), which is 'fencing_' + lowercase-hex " +
			"SHA-256 of the resource id -- exclusively [0-9a-f], never raw input, mirroring the production " +
			"provider's own identical justification. ALTER SEQUENCE cannot parameterize an identifier.")]
	public async Task PostgresLeaderElection_Relinquishes_WhenFencingTokenIsExhausted()
	{
		_postgresFixture.DockerAvailable.ShouldBeTrue("f559sr real-infra overflow coverage must never be skipped");

		var services = new ServiceCollection();
		_ = services.AddPostgresFencingTokenProvider(_postgresFixture.ConnectionString);
		await using var provider = services.BuildServiceProvider();
		var fencingProvider = provider.GetRequiredService<IFencingTokenProvider>();

		// A fresh, unique lock key so the sequence it maps to (fencing_<hash-of-lockkey-string>) has never
		// been touched by another test.
		var lockKey = Random.Shared.NextInt64(1, long.MaxValue - 1000);
		var resourceId = lockKey.ToString(CultureInfo.InvariantCulture);

		_ = await fencingProvider.IssueTokenAsync(resourceId, TestContext.Current.CancellationToken);
		await using (var connection = new NpgsqlConnection(_postgresFixture.ConnectionString))
		{
			await connection.OpenAsync(TestContext.Current.CancellationToken);
			var sequenceName = PostgresSequenceName(resourceId);
			await using var alter = new NpgsqlCommand(
				$"ALTER SEQUENCE {sequenceName} RESTART WITH 9223372036854775807", connection);
			await alter.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
		}

		// Consume the ceiling value itself (a valid mint) so the election's OWN StartAsync mint attempt,
		// below, is the one that genuinely tries to advance past long.MaxValue.
		_ = await fencingProvider.IssueTokenAsync(resourceId, TestContext.Current.CancellationToken);

		var election = new PostgresLeaderElection(
			Options.Create(new PostgresLeaderElectionOptions { ConnectionString = _postgresFixture.ConnectionString, LockKey = lockKey }),
			Options.Create(new LeaderElectionOptions { InstanceId = "overflow-candidate" }),
			NullLogger<PostgresLeaderElection>.Instance,
			fencingProvider);

		// The attempt's outcome is observable on the PUBLIC surface: every provider raises
		// AcquisitionFailed when a mint it cannot advance forces it to relinquish. Subscribe before
		// StartAsync so the first attempt cannot be missed.
		var acquisitionFailed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var failureReason = string.Empty;
		election.AcquisitionFailed += (_, args) =>
		{
			failureReason = args.Reason;
			_ = acquisitionFailed.TrySetResult();
		};

		try
		{
			await election.StartAsync(TestContext.Current.CancellationToken);

			// Observe the acquisition attempt through the public AcquisitionFailed event rather than
			// sleeping. The 500ms this replaces was REDUNDANT, not load-bearing: every provider here
			// awaits its first acquire inside StartAsync (Redis:168, Postgres/SqlServer/MongoDB
			// likewise), so IsLeader is already decided on return. What the event buys is strength, not
			// safety -- IsLeader.ShouldBeFalse alone is also satisfied by an election that never
			// acquired for some unrelated reason, so the arm below pins WHY the attempt was abandoned.
			await WaitHelpers.AwaitSignalAsync(
				acquisitionFailed.Task,
				TimeSpan.FromSeconds(60),
				cancellationToken: TestContext.Current.CancellationToken);
			failureReason.Contains("fencing", StringComparison.OrdinalIgnoreCase).ShouldBeTrue(
				$"the attempt must have been abandoned because the fencing MINT was exhausted, but the "
				+ $"reported reason was '{failureReason}' -- any other reason means this arm observed an "
				+ "unrelated failure and proves nothing about overflow.");

			election.IsLeader.ShouldBeFalse(
				"a leadership attempt whose fencing mint exceeds the sequence's ceiling must relinquish rather than lead with an un-advanced fence");
			election.CurrentLeadership.ShouldBeNull();
		}
		finally
		{
			await election.DisposeAsync();
		}
	}

	// ---------------------------------------------------------------------------------------------
	// SQL Server (beyond the bead's stated Redis/Mongo/Postgres minimum -- a real container was
	// already available here, and the provider's ceiling mechanism -- a NO CYCLE bigint SEQUENCE,
	// same shape as Postgres -- is cheap to cover with the same pattern).
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Mirrors <c>SqlServerFencingTokenProvider.SequenceName</c> exactly (documented on that type's own
	/// remarks: <c>"fencing_"</c> + the full uppercase-hex SHA-256 of the resource id) so this test can
	/// address the same real sequence the provider creates, without needing internals access.
	/// </summary>
	private static string SqlServerSequenceName(string resourceId)
	{
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(resourceId));
		return "fencing_" + Convert.ToHexString(hash);
	}

	[Fact]
	[SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
		Justification = "sequenceName comes from SqlServerSequenceName(), which is 'fencing_' + hex SHA-256 " +
			"of the resource id -- exclusively [0-9A-F], never raw input, mirroring the production provider's " +
			"own identical justification. ALTER SEQUENCE cannot parameterize an identifier.")]
	public async Task SqlServerFencingTokenExhaustion_ThrowsWithNativeInnerException()
	{
		_sqlServerFixture.DockerAvailable.ShouldBeTrue("f559sr real-infra overflow coverage must never be skipped");

		var resourceId = UniqueResourceId("sqlserver-overflow");

		var services = new ServiceCollection();
		_ = services.AddSqlServerFencingTokenProvider(_sqlServerFixture.ConnectionString);
		await using var provider = services.BuildServiceProvider();
		var fencingProvider = provider.GetRequiredService<IFencingTokenProvider>();

		// Mint once first so the provider creates the real sequence, then seed it directly to the ceiling.
		_ = await fencingProvider.IssueTokenAsync(resourceId, TestContext.Current.CancellationToken);

		await using (var connection = new SqlConnection(_sqlServerFixture.ConnectionString))
		{
			await connection.OpenAsync(TestContext.Current.CancellationToken);
			var sequenceName = SqlServerSequenceName(resourceId);
			await using var alter = new SqlCommand(
				$"ALTER SEQUENCE {sequenceName} RESTART WITH 9223372036854775807", connection);
			await alter.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
		}

		// Consume the ceiling value itself (a valid draw) so the mint under test is the one that genuinely
		// tries to advance past long.MaxValue.
		var atCeiling = await fencingProvider.IssueTokenAsync(resourceId, TestContext.Current.CancellationToken);
		atCeiling.ShouldBe(long.MaxValue);

		// LIVENESS (below ceiling, different resource): the same provider still mints normally.
		var livenessResourceId = UniqueResourceId("sqlserver-liveness");
		var first = await fencingProvider.IssueTokenAsync(livenessResourceId, TestContext.Current.CancellationToken);
		var second = await fencingProvider.IssueTokenAsync(livenessResourceId, TestContext.Current.CancellationToken);
		first.ShouldBe(1L);
		second.ShouldBeGreaterThan(first);

		// SAFETY -- the real ceiling: the next draw must exceed a NO CYCLE bigint sequence's limit and
		// raise SqlException 11728 ("has reached its minimum or maximum value").
		var ex = await Should.ThrowAsync<FencingTokenExhaustedException>(
			() => fencingProvider.IssueTokenAsync(resourceId, TestContext.Current.CancellationToken).AsTask());
		ex.InnerException.ShouldBeOfType<SqlException>(
			"SQL Server's own sequence-exhausted (11728) error must be preserved as the inner exception");
	}

	[Fact]
	[SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
		Justification = "sequenceName comes from SqlServerSequenceName(), which is 'fencing_' + hex SHA-256 " +
			"of the resource id -- exclusively [0-9A-F], never raw input, mirroring the production provider's " +
			"own identical justification. ALTER SEQUENCE cannot parameterize an identifier.")]
	public async Task SqlServerLeaderElection_Relinquishes_WhenFencingTokenIsExhausted()
	{
		_sqlServerFixture.DockerAvailable.ShouldBeTrue("f559sr real-infra overflow coverage must never be skipped");

		var lockResource = UniqueResourceId("sqlserver-election-overflow");

		var services = new ServiceCollection();
		_ = services.AddSqlServerFencingTokenProvider(_sqlServerFixture.ConnectionString);
		await using var provider = services.BuildServiceProvider();
		var fencingProvider = provider.GetRequiredService<IFencingTokenProvider>();

		_ = await fencingProvider.IssueTokenAsync(lockResource, TestContext.Current.CancellationToken);
		await using (var connection = new SqlConnection(_sqlServerFixture.ConnectionString))
		{
			await connection.OpenAsync(TestContext.Current.CancellationToken);
			var sequenceName = SqlServerSequenceName(lockResource);
			await using var alter = new SqlCommand(
				$"ALTER SEQUENCE {sequenceName} RESTART WITH 9223372036854775807", connection);
			await alter.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
		}

		_ = await fencingProvider.IssueTokenAsync(lockResource, TestContext.Current.CancellationToken);

		var election = new SqlServerLeaderElection(
			_sqlServerFixture.ConnectionString,
			lockResource,
			Options.Create(new LeaderElectionOptions { InstanceId = "overflow-candidate" }),
			NullLogger<SqlServerLeaderElection>.Instance,
			failureClassifier: null,
			fencingTokenProvider: fencingProvider);

		// The attempt's outcome is observable on the PUBLIC surface: every provider raises
		// AcquisitionFailed when a mint it cannot advance forces it to relinquish. Subscribe before
		// StartAsync so the first attempt cannot be missed.
		var acquisitionFailed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var failureReason = string.Empty;
		election.AcquisitionFailed += (_, args) =>
		{
			failureReason = args.Reason;
			_ = acquisitionFailed.TrySetResult();
		};

		try
		{
			await election.StartAsync(TestContext.Current.CancellationToken);

			// Observe the acquisition attempt through the public AcquisitionFailed event rather than
			// sleeping. The 500ms this replaces was REDUNDANT, not load-bearing: every provider here
			// awaits its first acquire inside StartAsync (Redis:168, Postgres/SqlServer/MongoDB
			// likewise), so IsLeader is already decided on return. What the event buys is strength, not
			// safety -- IsLeader.ShouldBeFalse alone is also satisfied by an election that never
			// acquired for some unrelated reason, so the arm below pins WHY the attempt was abandoned.
			await WaitHelpers.AwaitSignalAsync(
				acquisitionFailed.Task,
				TimeSpan.FromSeconds(60),
				cancellationToken: TestContext.Current.CancellationToken);
			failureReason.Contains("fencing", StringComparison.OrdinalIgnoreCase).ShouldBeTrue(
				$"the attempt must have been abandoned because the fencing MINT was exhausted, but the "
				+ $"reported reason was '{failureReason}' -- any other reason means this arm observed an "
				+ "unrelated failure and proves nothing about overflow.");

			election.IsLeader.ShouldBeFalse(
				"a leadership attempt whose fencing mint exceeds the sequence's ceiling must relinquish rather than lead with an un-advanced fence");
			election.CurrentLeadership.ShouldBeNull();
		}
		finally
		{
			await election.DisposeAsync();
		}
	}
}
