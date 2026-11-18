// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.Metrics;
using Excalibur.Dispatch;
using Excalibur.Outbox.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Excalibur.Integration.Tests.Data.Outbox;

[Collection(SqlServerOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Data")]
[Trait("Database", "SqlServer")]
[Trait("Pattern", "Regression")]
public sealed class SqlServerOutboxClaimRefusalVisibilityShould : IClassFixture<SqlServerOutboxStoreContainerFixture>
{
    private readonly SqlServerOutboxStoreContainerFixture _fixture;
    public SqlServerOutboxClaimRefusalVisibilityShould(SqlServerOutboxStoreContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ReportOnlyTheCommittedStaleClaimAndLeaveThePersistedRowUnchanged()
    {
        _fixture.DockerAvailable.ShouldBeTrue("this refusal lock requires actual SQL Server");
        var token = TestContext.Current.CancellationToken;
        await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
        await _fixture.CleanupTableAsync().ConfigureAwait(false);
        try
        {
        using var observations = new RefusalObservations();
        var store = new SqlServerOutboxStore(
            Options.Create(new SqlServerOutboxOptions { ConnectionString = _fixture.ConnectionString }), observations);
        (await store.GetUnsentMessagesAsync(1, 20, token).ConfigureAwait(false)).ShouldBeEmpty();
        (await store.GetUnsentMessagesAsync(1, 20, token).ConfigureAwait(false)).ShouldBeEmpty();
        observations.AssertCounts(0, 0);
        var message = new OutboundMessage("test.message", [1, 2, 3], "dest");
        await store.StageMessageAsync(message, token).ConfigureAwait(false);
        var before = await ReadRowAsync(_fixture.ConnectionString, message.Id, token).ConfigureAwait(false);
        before.ShouldNotBe("[]");
        (await store.GetUnsentMessagesAsync(1, 10, token).ConfigureAwait(false)).ShouldBeEmpty();
        (await ReadRowAsync(_fixture.ConnectionString, message.Id, token).ConfigureAwait(false)).ShouldBe(before);
        (await store.GetFencingHighWaterAsync(token).ConfigureAwait(false)).ShouldBe(20);
        var claimed = (await store.GetUnsentMessagesAsync(1, 20, token).ConfigureAwait(false)).Single();
        claimed.Id.ShouldBe(message.Id);
        claimed.Payload.ShouldBe(new byte[] { 1, 2, 3 });
        claimed.RetryCount.ShouldBe(0);
        observations.AssertCounts(1, 1);
        observations.AssertWarning(10, 20);
        }
        finally
        {
            await _fixture.CleanupTableAsync().ConfigureAwait(false);
        }
    }

    private static async Task<string> ReadRowAsync(string connectionString, string messageId, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM [dbo].[OutboxMessages] WHERE Id = @Id FOR JSON PATH, INCLUDE_NULL_VALUES;";
        _ = command.Parameters.AddWithValue("@Id", messageId);
        return (string)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
    }

    private sealed class RefusalObservations : ILogger<SqlServerOutboxStore>, IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly AsyncLocal<bool> _active = new();
        private readonly List<KeyValuePair<string, object?>[]> _measurements = [];
        private readonly List<KeyValuePair<string, object?>[]> _warnings = [];
        internal bool ThrowLogger { get; init; }
        internal bool ThrowMetric { get; init; }

        internal RefusalObservations()
        {
            _active.Value = true;
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "Excalibur.Outbox.SqlServer" && instrument.Name == "excalibur.outbox.fence_refusals")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
            {
                if (!_active.Value) { return; }
                if (ThrowMetric) { throw new ObserverFailureException(); }
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
                tags.Single(pair => pair.Key == "backend").Value.ShouldBe("sqlserver");
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
            if (ThrowLogger) { throw new ObserverFailureException(); }
            logLevel.ShouldBe(LogLevel.Warning);
            _warnings.Add(((IEnumerable<KeyValuePair<string, object?>>)state!).ToArray());
        }
        public void Dispose()
        {
            _active.Value = false;
            _listener.Dispose();
        }
    }
    private sealed class ObserverFailureException : Exception { }
}
