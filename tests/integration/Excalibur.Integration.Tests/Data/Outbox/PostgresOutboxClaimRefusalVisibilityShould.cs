// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.Metrics;
using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Outbox.Postgres;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Shouldly;
using Xunit;

namespace Excalibur.Integration.Tests.Data.Outbox;

[Collection(PostgresOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Data")]
[Trait("Database", "Postgres")]
[Trait("Pattern", "Regression")]
public sealed class PostgresOutboxClaimRefusalVisibilityShould : IClassFixture<PostgresOutboxStoreContainerFixture>
{
    private readonly PostgresOutboxStoreContainerFixture _fixture;
    public PostgresOutboxClaimRefusalVisibilityShould(PostgresOutboxStoreContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ReportOnlyTheAtomicStaleClaimAndLeaveThePersistedRowUnchanged()
    {
        _fixture.DockerAvailable.ShouldBeTrue("this refusal lock requires actual PostgreSQL");
        var token = TestContext.Current.CancellationToken;
        await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
        await _fixture.CleanupTableAsync().ConfigureAwait(false);
        Exception? primary = null;
        try
        {
            using var observations = new RefusalObservations();
            await using var connection = _fixture.CreateConnection();
            var db = A.Fake<IDb>();
            A.CallTo(() => db.Connection).Returns(connection);
            using var store = new PostgresOutboxStore(db, Options.Create(new PostgresOutboxStoreOptions
            {
                SchemaName = _fixture.SchemaName,
                OutboxTableName = _fixture.OutboxTableName,
                DeadLetterTableName = _fixture.DeadLetterTableName,
            }), observations);
            (await store.GetUnsentMessagesAsync(1, 20, token).ConfigureAwait(false)).ShouldBeEmpty();
            (await store.GetUnsentMessagesAsync(1, 20, token).ConfigureAwait(false)).ShouldBeEmpty();
            observations.AssertCounts(0, 0);
            byte[] payload = [1, 2, 3];
            var message = new OutboundMessage("test.message", payload, "dest");
            await store.StageMessageAsync(message, token).ConfigureAwait(false);
            var before = await ReadRowAsync(message.Id, token).ConfigureAwait(false);
            before.ShouldNotBeNullOrWhiteSpace();
            (await store.GetUnsentMessagesAsync(1, 10, token).ConfigureAwait(false)).ShouldBeEmpty();
            (await ReadRowAsync(message.Id, token).ConfigureAwait(false)).ShouldBe(before);
            (await store.GetFencingHighWaterAsync(token).ConfigureAwait(false)).ShouldBe(20);
            var claimed = (await store.GetUnsentMessagesAsync(1, 20, token).ConfigureAwait(false)).Single();
            claimed.Id.ShouldBe(message.Id);
            claimed.Payload.ShouldBe(payload);
            claimed.RetryCount.ShouldBe(0);
            observations.AssertCounts(1, 1);
            observations.AssertWarning(10, 20);
        }
        catch (Exception exception)
        {
            primary = exception;
            throw;
        }
        finally
        {
            try
            {
                await _fixture.CleanupTableAsync().ConfigureAwait(false);
            }
            catch (Exception cleanupFailure) when (primary is not null)
            {
                primary.Data["ClaimVisibilityCleanupFailure"] = cleanupFailure;
            }
        }
    }

    private async Task<string?> ReadRowAsync(string messageId, CancellationToken cancellationToken)
    {
        await using var connection = _fixture.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT to_jsonb(o)::text FROM public.outbox AS o WHERE message_id = @Id;";
        _ = command.Parameters.AddWithValue("@Id", messageId);
        return (string?)await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed class RefusalObservations : ILogger<PostgresOutboxStore>, IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly AsyncLocal<bool> _active = new();
        private readonly List<KeyValuePair<string, object?>[]> _measurements = [];
        private readonly List<KeyValuePair<string, object?>[]> _warnings = [];

        internal RefusalObservations()
        {
            _active.Value = true;
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "Excalibur.Outbox.Postgres" && instrument.Name == "excalibur.outbox.fence_refusals")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
            {
                if (!_active.Value) { return; }
                value.ShouldBe(1);
                _measurements.Add(tags.ToArray());
            });
            _listener.Start();
        }

        internal void AssertCounts(int measurements, int warnings)
        {
            _measurements.Count.ShouldBe(measurements);
            _warnings.Count.ShouldBe(warnings);
            foreach (var tags in _measurements)
            {
                tags.Length.ShouldBe(2);
                tags.Single(pair => pair.Key == "backend").Value.ShouldBe("postgres");
                tags.Single(pair => pair.Key == "reason").Value.ShouldBe("stale_claim");
            }
        }

        internal void AssertWarning(long presented, long highWater)
        {
            var fields = _warnings.Single();
            fields.Single(pair => pair.Key == "PresentedToken").Value.ShouldBe(presented);
            fields.Single(pair => pair.Key == "HighWater").Value.ShouldBe(highWater);
        }

        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (eventId.Name != "OutboxClaimFenceRefused") { return; }
            logLevel.ShouldBe(LogLevel.Warning);
            _warnings.Add(((IEnumerable<KeyValuePair<string, object?>>)state!).ToArray());
        }
        public void Dispose()
        {
            _active.Value = false;
            _listener.Dispose();
        }
    }
}
