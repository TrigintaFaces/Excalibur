// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.ExceptionServices;
using Excalibur.Data.Sharding;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.MongoDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Testcontainers.MongoDb;
using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.MongoDb;

[Trait("Category", "Integration")]
[Trait("Component", "EventSourcing")]
[Trait("Database", "MongoDB")]
[Trait("Pattern", "Regression")]
public sealed class TenantShardReadinessAgainstRealMongoDbShould(MongoShardReadinessFixture fixture)
	: IClassFixture<MongoShardReadinessFixture>
{
	[Fact]
	public async Task DetectDeniedPhysicalDatabaseAndRecoverAfterGrantWithoutReadingPayloads()
	{
		var suffix = Guid.NewGuid().ToString("N");
		var good = "health_good_" + suffix;
		var bad = "health_bad_" + suffix;
		var user = "reader_" + suffix;
		var password = Guid.NewGuid().ToString("N");
		var administrator = new MongoClient(fixture.ConnectionString);
		var admin = administrator.GetDatabase("admin");
		var token = TestContext.Current.CancellationToken;
		var errors = new List<Exception>();
		ServiceProvider? provider = null;
		try
		{
			await admin.RunCommandAsync<BsonDocument>(new BsonDocument
			{
				{ "createUser", user }, { "pwd", password },
				{ "roles", new BsonArray { new BsonDocument { { "role", "read" }, { "db", good } } } }
			}, cancellationToken: token);
			// The profiler below proves the actual wire projection; malformed payload alone cannot.
			foreach (var database in new[] { good, bad })
			{
				await administrator.GetDatabase(database).GetCollection<BsonDocument>("events")
					.InsertOneAsync(new BsonDocument { { "_id", "probe" }, { "payload", "not-event-json" } }, cancellationToken: token);
			}
			// This isolated test database can profile every operation without changing a shared server.
			await administrator.GetDatabase(good).RunCommandAsync<BsonDocument>(
				new BsonDocument("profile", 2), cancellationToken: token);
			var url = new MongoUrlBuilder(fixture.ConnectionString)
			{
				Username = user, Password = password, AuthenticationSource = "admin"
			};
			var connection = url.ToString();
			var map = new PhysicalMap(connection, good, bad);
			var services = new ServiceCollection();
			services.AddLogging();
			services.AddSingleton<ITenantShardMap>(map);
			services.AddExcalibur(excalibur => excalibur.AddEventSourcing(events =>
			{
				events.UseMongoDB(mongo => mongo.ConnectionString(connection).DatabaseName(good).CollectionName("events"));
				events.UseMongoDbTenantEventStore();
			}));
			services.AddHealthChecks().AddTenantShardHealthCheck();
			provider = services.BuildServiceProvider();
			var registration = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
				.Single(item => item.Name == "tenant-shard");
			var context = new HealthCheckContext { Registration = registration };
			var check = registration.Factory(provider);
			var failed = await check.CheckHealthAsync(context, token);
			failed.Status.ShouldBe(HealthStatus.Degraded);
			failed.Data["healthyShards"].ShouldBe(1);
			failed.Data["failedShardIds"].ShouldBe("physical-bad");
			var cause = failed.Exception.ShouldBeOfType<AggregateException>().InnerExceptions.Single()
				.InnerException.ShouldBeOfType<MongoCommandException>();
			cause.Code.ShouldBe(13);
			var profile = await administrator.GetDatabase(good).GetCollection<BsonDocument>("system.profile")
				.Find(new BsonDocument("command.find", "events")).ToListAsync(token);
			profile.ShouldNotBeEmpty();
			foreach (var entry in profile)
			{
				var command = entry["command"].AsBsonDocument;
				command["projection"].AsBsonDocument.ShouldBe(new BsonDocument("_id", 1));
				command["limit"].ToInt64().ShouldBe(1);
				// The CRUD spec requires limit+1 on the wire when configured limit equals batch size,
				// avoiding a needless open cursor. The server's limit still bounds the result to one.
				// https://specifications.readthedocs.io/en/latest/crud/crud/
				command["batchSize"].ToInt64().ShouldBe(2);
			}
			map.TenantLookups.ShouldBe(0);
			await admin.RunCommandAsync<BsonDocument>(new BsonDocument
			{
				{ "grantRolesToUser", user },
				{ "roles", new BsonArray { new BsonDocument { { "role", "read" }, { "db", bad } } } }
			}, cancellationToken: token);
			var repaired = await check.CheckHealthAsync(context, token);
			repaired.Status.ShouldBe(HealthStatus.Healthy);
			repaired.Data["healthyShards"].ShouldBe(2);
			map.TenantLookups.ShouldBe(0);
		}
		catch (Exception error) { errors.Add(error); }
		finally
		{
			if (provider is not null)
			{
				await CaptureCleanupAsync(_ => provider.DisposeAsync().AsTask(), errors);
			}
			await CaptureCleanupAsync(cleanup => administrator.GetDatabase(good)
				.RunCommandAsync<BsonDocument>(new BsonDocument("profile", 0), cancellationToken: cleanup), errors);
			await CaptureCleanupAsync(async cleanup =>
			{
				try { await admin.RunCommandAsync<BsonDocument>(new BsonDocument("dropUser", user), cancellationToken: cleanup); }
				catch (MongoCommandException error) when (error.Code == 11) { } // UserNotFound after failed creation.
			}, errors);
			await CaptureCleanupAsync(cleanup => administrator.DropDatabaseAsync(good, cleanup), errors);
			await CaptureCleanupAsync(cleanup => administrator.DropDatabaseAsync(bad, cleanup), errors);
			try { administrator.Dispose(); }
			catch (Exception error) { errors.Add(error); }
		}
		if (errors.Count == 1) { ExceptionDispatchInfo.Capture(errors[0]).Throw(); }
		if (errors.Count > 1) { throw new AggregateException(errors); }
	}

	private static async Task CaptureCleanupAsync(Func<CancellationToken, Task> action, List<Exception> errors)
	{
		using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		try { await action(cleanup.Token); }
		catch (Exception error) { errors.Add(error); }
	}

	private sealed class PhysicalMap(string connection, string good, string bad) : ITenantShardMap
	{
		internal int TenantLookups;
		public ShardInfo GetShardInfo(string tenantId)
		{
			Interlocked.Increment(ref TenantLookups);
			return new("physical-good", connection, DatabaseName: good);
		}
		public ShardInfo GetShardById(string id) => id switch
		{
			"physical-good" => new(id, connection, DatabaseName: good),
			"physical-bad" => new(id, connection, DatabaseName: bad),
			_ => throw new KeyNotFoundException(id)
		};
		public IReadOnlyCollection<string> GetRegisteredShardIds() => ["physical-good", "physical-bad"];
	}
}

/// <summary>Dedicated authenticated local server; shared CDC fixtures need a different replica-set topology.</summary>
public sealed class MongoShardReadinessFixture : ContainerFixtureBase
{
	private MongoDbContainer? _container;
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized.");
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new MongoDbBuilder().WithImage("mongo:7").WithUsername("root")
			.WithPassword(Guid.NewGuid().ToString("N")).WithCleanUp(true).Build();
		await _container.StartAsync(cancellationToken);
	}
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		if (_container is not null) { await _container.DisposeAsync(); }
	}
}
