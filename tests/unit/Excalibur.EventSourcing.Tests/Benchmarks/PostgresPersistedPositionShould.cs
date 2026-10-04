// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Postgres.Requests;

using Npgsql;

using Tests.Shared.Fixtures;

namespace Excalibur.EventSourcing.Tests.Benchmarks;

[Collection("BenchmarkValidationLifecycle")]
[Trait("Category", "Integration")]
[Trait("Component", "EventSourcing")]
[Trait("Pattern", "Regression")]
public sealed class PostgresPersistedPositionShould(PostgresContainerFixture database)
    : IClassFixture<PostgresContainerFixture>
{
    [Fact]
    public async Task ReturnStoredPositionsWithVersionFilteringTenantIsolationAndArchival()
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var resource = typeof(PostgresPersistedPositionShould).Assembly
            .GetManifestResourceStream("BenchmarkSchema.Postgres.EventStore.sql");
        resource.ShouldNotBeNull();
        using var reader = new StreamReader(resource);
#pragma warning disable CA2100 // Embedded shipped SQL, not external input.
        await using var schema = new NpgsqlCommand(await reader.ReadToEndAsync(), connection);
#pragma warning restore CA2100
        _ = await schema.ExecuteNonQueryAsync();
        const string Seed = """
            INSERT INTO public.events(position,event_id,aggregate_id,aggregate_type,event_type,event_data,version,timestamp,archived_at,tenant_id)
            VALUES (1,'a0','shared','Aggregate','Event',decode('01','hex'),0,now(),NULL,'tenant-a'),
                   (2,'b0','shared','Aggregate','Event',decode('02','hex'),0,now(),NULL,'tenant-b'),
                   (3,'a1','shared','Aggregate','Event',decode('03','hex'),1,now(),NULL,'tenant-a'),
                   (4,'b1','shared','Aggregate','Event',decode('04','hex'),1,now(),NULL,'tenant-b'),
                   (5,'a2','shared','Aggregate','Event',NULL,2,'2026-10-02 12:34:56+00','2026-10-02 13:00:00+00','tenant-a');
            """;
        await using var seed = new NpgsqlCommand(Seed, connection);
        _ = await seed.ExecuteNonQueryAsync();

        var request = new LoadEventsRequest("shared", "Aggregate", 1, TenantScope.Scoped("tenant-a"), CancellationToken.None);
        var actual = (await request.ResolveAsync(connection)).ShouldHaveSingleItem();
        actual.EventId.ShouldBe("a2");
        actual.Version.ShouldBe(2);
        actual.GlobalPosition.ShouldBe(5);
        actual.EventData.ShouldBeNull();
        actual.ArchivedAt.ShouldNotBeNull();
        actual.Timestamp.ShouldBe(new DateTimeOffset(2026, 10, 2, 12, 34, 56, TimeSpan.Zero));
        actual.ArchivedAt.ShouldBe(new DateTimeOffset(2026, 10, 2, 13, 0, 0, TimeSpan.Zero));
        var other = await new LoadEventsRequest("shared", "Aggregate", -1, TenantScope.Scoped("tenant-b"), CancellationToken.None)
            .ResolveAsync(connection);
        other.Select(e => e.EventId).ShouldBe(["b0", "b1"]);
        other.Select(e => e.GlobalPosition).ShouldBe([2L, 4L]);
    }
}
