// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.Metrics;
using System.Globalization;
using Excalibur.Data;
using Excalibur.Dispatch;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oracle.ManagedDataAccess.Client;
using Shouldly;
using Xunit;

namespace Excalibur.Outbox.Oracle.Tests;

[Collection(OracleOutboxCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Component", "Outbox")]
[Trait("Database", "Oracle")]
[Trait("Pattern", "Regression")]
public sealed class OracleOutboxClaimRefusalVisibilityShould(OracleOutboxStoreContainerFixture fixture)
{
    [Fact]
    public async Task ReportOnlyTheCommittedStaleClaimAndLeaveThePersistedRowUnchanged()
    {
        fixture.DockerAvailable.ShouldBeTrue("this refusal control requires actual Oracle");
        var token = TestContext.Current.CancellationToken;
        await fixture.EnsureInitializedAsync().ConfigureAwait(false);
        await fixture.CleanupTableAsync().ConfigureAwait(false);
        Exception? primary = null;
        try
        {
            using var observations = new RefusalObservations();
            var db = A.Fake<IDb>();
            A.CallTo(() => db.Connection).ReturnsLazily(() => fixture.CreateConnection());
            using var store = new OracleOutboxStore(db, Options.Create(new OracleOutboxStoreOptions
            {
                SchemaName = fixture.SchemaName,
                OutboxTableName = fixture.OutboxTableName,
                DeadLetterTableName = fixture.DeadLetterTableName,
                ReservationTimeout = 300,
                MaxAttempts = 3,
            }), observations, workerConnections: OracleWorkerConnectionSource.FromConnectionString(fixture.ConnectionString));
            (await store.GetUnsentMessagesAsync(1, 20, token).ConfigureAwait(false)).ShouldBeEmpty();
            (await store.GetUnsentMessagesAsync(1, 20, token).ConfigureAwait(false)).ShouldBeEmpty();
            observations.AssertCounts(0, 0);
            var message = new OutboundMessage("test.message", [1, 2, 3], "dest");
            await store.StageMessageAsync(message, token).ConfigureAwait(false);
            var before = await ReadRowAsync(message.Id, token).ConfigureAwait(false);
            before.ShouldNotBeEmpty();
            (await store.GetUnsentMessagesAsync(1, 10, token).ConfigureAwait(false)).ShouldBeEmpty();
            (await ReadRowAsync(message.Id, token).ConfigureAwait(false)).ShouldBe(before);
            (await store.GetFencingHighWaterAsync(token).ConfigureAwait(false)).ShouldBe(20);
            var claimed = (await store.GetUnsentMessagesAsync(1, 20, token).ConfigureAwait(false)).Single();
            claimed.Id.ShouldBe(message.Id);
            claimed.Payload.ShouldBe(message.Payload);
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
                await fixture.CleanupTableAsync().ConfigureAwait(false);
            }
            catch (Exception cleanupFailure) when (primary is not null)
            {
                primary.Data["ClaimVisibilityCleanupFailure"] = cleanupFailure;
            }
        }
    }

    private async Task<string[]> ReadRowAsync(string messageId, CancellationToken cancellationToken)
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandTimeout = 30;
        command.CommandText = "SELECT * FROM OUTBOX WHERE message_id=:MessageId";
        _ = command.Parameters.Add("MessageId", OracleDbType.Varchar2, messageId, System.Data.ParameterDirection.Input);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)).ShouldBeTrue();
        var values = new string[reader.FieldCount];
        for (var index = 0; index < values.Length; index++)
        {
            var value = reader.GetValue(index);
            var encoded = value switch
            {
                DBNull => "null",
                byte[] bytes => "bytes:" + Convert.ToHexString(bytes),
                DateTime timestamp => "time:" + timestamp.ToString("O", CultureInfo.InvariantCulture),
                DateTimeOffset timestamp => "offset:" + timestamp.ToString("O", CultureInfo.InvariantCulture),
                _ => "value:" + Convert.ToString(value, CultureInfo.InvariantCulture),
            };
            values[index] = reader.GetName(index) + ":" + encoded;
        }
        (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)).ShouldBeFalse();
        return values;
    }

    private sealed class RefusalObservations : ILogger<OracleOutboxStore>, IDisposable
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
                if (instrument.Meter.Name == "Excalibur.Outbox.Oracle" && instrument.Name == "excalibur.outbox.fence_refusals")
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
                tags.Single(pair => pair.Key == "backend").Value.ShouldBe("oracle");
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
