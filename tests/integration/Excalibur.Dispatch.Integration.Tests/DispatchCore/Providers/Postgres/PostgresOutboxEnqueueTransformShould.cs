// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Encryption;
using Excalibur.Data;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Outbox;
using Excalibur.Outbox.Postgres;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Tests.Shared.Helpers;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Postgres;

[Collection(ContainerCollections.Postgres)]
[Trait("Category", "Integration")]
[Trait("Component", "Outbox")]
[Trait("Database", "Postgres")]
[Trait("Pattern", "Regression")]
public sealed partial class PostgresOutboxEnqueueTransformShould(PostgresFixture fixture)
{
    private static readonly byte[] CanonicalBytes = Encoding.UTF8.GetBytes("{\"secret\":\"before\",\"state\":\"Ready\"}");
    private static readonly TimeSpan JoinDeadline = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData("provided-id", true)]
    [InlineData("provided-id", false)]
    [InlineData(null, true)]
    [InlineData("", false)]
    public async Task PreserveIndependentCanonicalPayloadAndPostgresEnvelopeIdentity(string? contextId, bool destination)
    {
        await RunAsync(null, async harness =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var message = new InputMessage();
            var context = Context(harness.Services, message, contextId, destination);
            await harness.Raw.EnqueueAsync(message, context, deadline.Token);
            var row = (await harness.ReadAsync("before-correlation", deadline.Token)).Single();
            row.Payload.ShouldBe(CanonicalBytes);
            AssertEnvelope(row, contextId, destination);
        });
    }

    [Theory]
    [InlineData(false, " ")]
    [InlineData(true, " ")]
    [InlineData(false, " \t\r\n")]
    [InlineData(true, " \t\r\n")]
    public async Task RejectNonemptyWhitespaceContextIdBeforeSerializationCallbackAndDatabase(bool transformed, string id)
    {
        await RunAsync(null, async harness =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var input = new InputMessage();
            var context = Context(harness.Services, input, id, true);
            var readsBefore = harness.Db.Reads;
            var callbacks = 0;
            Exception? observed = null;
            var facet = transformed ? Facet(harness.Raw) : null;
            ValueTask<byte[]> Transform(ReadOnlyMemory<byte> bytes, string? tenant, CancellationToken token)
            {
                Interlocked.Increment(ref callbacks);
                return ValueTask.FromResult(new byte[] { 1 });
            }
            try
            {
                if (transformed) { await facet!.EnqueueWithPayloadTransformAsync(input, context, Transform, deadline.Token); }
                else { await harness.Raw.EnqueueAsync(input, context, deadline.Token); }
            }
            catch (Exception error) { observed = error; }
            (await harness.ReadAsync("before-correlation", deadline.Token)).ShouldBeEmpty("an ID that completion refuses must not be accepted");
            observed.ShouldBeOfType<ArgumentException>().ShouldNotBeNull();
            input.SecretReads.ShouldBe(0); callbacks.ShouldBe(0); harness.Db.Reads.ShouldBe(readsBefore);
            context.MessageId.ShouldBe(id);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CaptureTheOpaqueContextIdOnceBeforeSerialization(bool transformed)
    {
        await RunAsync(null, async harness =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            const string expectedId = "  opaque-id  ";
            var input = new InputMessage();
            var context = Context(harness.Services, input, expectedId, true);
            input.SecretReadAction = () => context.MessageId = "changed-during-serialization";
            if (transformed)
            {
                var facet = Facet(harness.Raw);
                await facet.EnqueueWithPayloadTransformAsync(input, context,
                    (bytes, _, _) => ValueTask.FromResult(bytes.ToArray()), deadline.Token);
            }
            else { await harness.Raw.EnqueueAsync(input, context, deadline.Token); }
            context.MessageId.ShouldBe("changed-during-serialization");
            input.SecretReads.ShouldBe(1);
            var row = (await harness.ReadAsync("before-correlation", deadline.Token)).Single();
            AssertEnvelope(row, expectedId, true); row.Payload.ShouldBe(CanonicalBytes);
        });
    }

    [Fact]
    public async Task PreserveAValidSpaceSurroundedOpaqueContextIdThroughStrongCompletion()
    {
        await RunAsync(null, async harness =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            const string id = "  valid-opaque-id  ";
            var input = new InputMessage(); var context = Context(harness.Services, input, id, true);
            await harness.Raw.EnqueueAsync(input, context, deadline.Token);
            AssertEnvelope((await harness.ReadAsync("before-correlation", deadline.Token)).Single(), id, true);
            var claim = (await harness.Raw.GetUnsentMessagesAsync(10, deadline.Token)).Single();
            claim.Id.ShouldBe(id);
            var completion = ((IOutboxStore)harness.Raw).GetService(typeof(IClaimScopedOutboxStore))
                .ShouldBeAssignableTo<IClaimScopedOutboxStore>().ShouldNotBeNull();
            (await completion.MarkSentAsync(id, claim.DispatcherId!, deadline.Token)).ShouldBe(OutboxCompletionOutcome.Applied);
            (await harness.ReadAsync("before-correlation", deadline.Token)).ShouldBeEmpty();
        });
    }

    [Fact]
    public async Task FreezeBeforeCallbackAndCopyItsResultBeforeTheBorrowedConnectionIsUsed()
    {
        await RunAsync(null, async harness =>
        {
            var facet = Facet(harness.Raw);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var input = new InputMessage();
            var context = Context(harness.Services, input, "prepared-id", true);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var result = new byte[] { 37, 0, 255 };
            var expected = result.ToArray();
            var readsBefore = harness.Db.Reads;
            var handoffs = 0;
            harness.Db.BeforeUse = () => { Interlocked.Increment(ref handoffs); result[0] = 99; };
            var callbacks = 0;
            async ValueTask<byte[]> Transform(ReadOnlyMemory<byte> bytes, string? tenant, CancellationToken token)
            {
                Interlocked.Increment(ref callbacks);
                bytes.ToArray().ShouldBe(CanonicalBytes);
                tenant.ShouldBe("before-tenant");
                token.ShouldBe(deadline.Token);
                entered.TrySetResult();
                await release.Task.WaitAsync(token);
                bytes.ToArray().ShouldBe(CanonicalBytes, "borrowed input stays stable throughout its invocation");
                return result;
            }
            var operation = facet.EnqueueWithPayloadTransformAsync(input, context, Transform, deadline.Token).AsTask();
            Exception? primary = null;
            Exception? secondary = null;
            try
            {
                await entered.Task.WaitAsync(JoinDeadline);
                (await harness.ReadAsync("before-correlation", deadline.Token)).ShouldBeEmpty();
                harness.Db.Reads.ShouldBe(readsBefore, "no provider connection handoff may precede callback completion");
                input.Secret = "after";
                context.MessageId = "after-id"; context.CorrelationId = "after-correlation"; context.CausationId = "after-cause";
                context.GetOrCreateIdentityFeature().TenantId = "after-tenant";
                context.SetDestination("after-destination"); context.SetPropagatedItem("custom", "after-header");
            }
            catch (Exception error) { primary = error; }
            finally
            {
                release.TrySetResult();
                try { await operation.WaitAsync(JoinDeadline); }
                catch (Exception error) { secondary = error; }
                if (!operation.IsCompleted) { harness.DeferUntil(operation); }
            }
            if (primary is not null)
            {
                if (secondary is not null) { primary.Data["SecondaryOperationJoin"] = secondary; }
                ExceptionDispatchInfo.Capture(primary).Throw();
            }
            if (secondary is not null) { ExceptionDispatchInfo.Capture(secondary).Throw(); }
            callbacks.ShouldBe(1);
            handoffs.ShouldBe(1, "actual provider connection handoff must execute the mutation oracle before persistence");
            result[0].ShouldBe((byte)99);
            var row = (await harness.ReadAsync("before-correlation", deadline.Token)).Single();
            row.Payload.ShouldBe(expected, "provider owns its callback result before using the real borrowed connection");
            AssertEnvelope(row, "prepared-id", true);
            (await harness.ReadAsync("after-correlation", deadline.Token)).ShouldBeEmpty();
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task RefuseCallbackFaultNullOrCancellationBeforeAcceptingAnyRow(int outcome)
    {
        await RunAsync(null, async harness =>
        {
            var facet = Facet(harness.Raw);
            using var cancellation = new CancellationTokenSource();
            var input = new InputMessage();
            var context = Context(harness.Services, input, "refused-id", true);
            var expected = new IOException("independent callback failure");
            var callbacks = 0;
            var readsBefore = harness.Db.Reads;
            ValueTask<byte[]> Transform(ReadOnlyMemory<byte> bytes, string? tenant, CancellationToken token)
            {
                Interlocked.Increment(ref callbacks);
                bytes.ToArray().ShouldBe(CanonicalBytes); tenant.ShouldBe("before-tenant"); token.ShouldBe(cancellation.Token);
                if (outcome == 0) { return ValueTask.FromException<byte[]>(expected); }
                if (outcome == 1) { return ValueTask.FromResult<byte[]>(null!); }
                cancellation.Cancel();
                return outcome == 2 ? new ValueTask<byte[]>(Task.FromCanceled<byte[]>(token)) : ValueTask.FromResult(new byte[] { 1 });
            }
            if (outcome == 5) { cancellation.Cancel(); }
            Exception? observed = null;
            try
            {
                await facet.EnqueueWithPayloadTransformAsync(input, context, outcome == 4 ? null! : Transform, cancellation.Token);
            }
            catch (Exception error) { observed = error; }
            observed.ShouldNotBeNull();
            if (outcome == 0) { observed.ShouldBeSameAs(expected); }
            else if (outcome == 1) { observed.ShouldBeOfType<InvalidOperationException>(); }
            else if (outcome == 4) { observed.ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("transformPayload"); }
            else { observed.ShouldBeAssignableTo<OperationCanceledException>().ShouldNotBeNull().CancellationToken.ShouldBe(cancellation.Token); }
            callbacks.ShouldBe(outcome is 4 or 5 ? 0 : 1);
            input.SecretReads.ShouldBe(outcome is 4 or 5 ? 0 : 1, "null callback and pre-cancellation refuse before serialization");
            harness.Db.Reads.ShouldBe(readsBefore);
            (await harness.ReadAsync("before-correlation", CancellationToken.None)).ShouldBeEmpty();
            input.Secret.ShouldBe("before");
            context.MessageId.ShouldBe("refused-id");
        });
    }

    [Theory]
    [InlineData(EncryptionMode.EncryptAndDecrypt)]
    [InlineData(EncryptionMode.EncryptNewDecryptAll)]
    public async Task EncryptPublicEnqueueWithoutLosingPostgresFramingAndDecryptThroughItsAdminRead(EncryptionMode mode)
    {
        await RunAsync(mode, async harness =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var input = new InputMessage();
            var context = Context(harness.Services, input, "encrypted-context-id", true);
            harness.Store.GetService(typeof(IOutboxPayloadTransformStore)).ShouldBeNull();
            try { await harness.Store.EnqueueAsync(input, context, deadline.Token); }
            catch
            {
                (await harness.ReadAsync("before-correlation", deadline.Token)).ShouldBeEmpty();
                throw;
            }
            var stored = (await harness.ReadAsync("before-correlation", deadline.Token)).Single();
            AssertEnvelope(stored, "encrypted-context-id", true);
            EncryptedData.IsFieldEncrypted(stored.Payload).ShouldBeTrue();
            var ciphertext = stored.Payload.ToArray();
            var claim = (await harness.Raw.GetUnsentMessagesAsync(10, deadline.Token)).Single();
            claim.Id.ShouldBe(stored.Id);
            var completion = ((IOutboxStore)harness.Raw).GetService(typeof(IClaimScopedOutboxStore))
                .ShouldBeAssignableTo<IClaimScopedOutboxStore>().ShouldNotBeNull();
            (await completion.MarkFailedAsync(claim.Id, "read-control", 1, claim.DispatcherId!, deadline.Token))
                .ShouldBe(OutboxCompletionOutcome.Applied);
            var admin = harness.Store.GetService(typeof(IOutboxStoreAdmin)).ShouldBeAssignableTo<IOutboxStoreAdmin>().ShouldNotBeNull();
            var read = (await admin.GetAllTenantsFailedMessagesAsync(5, null, 10, deadline.Token)).Single();
            read.Id.ShouldBe(stored.Id); read.TenantId.ShouldBe("before-tenant"); read.Payload.ShouldBe(CanonicalBytes);
            (await harness.ReadAsync("before-correlation", deadline.Token)).Single().Payload.ShouldBe(ciphertext);
            input.Secret.ShouldBe("before");
        });
    }

    [Fact]
    public async Task KeepTheSeparateExternalStageTransactionBorrowedAndUncommitted()
    {
        await RunAsync(null, async harness =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await using var transaction = await harness.Connection.BeginTransactionAsync(deadline.Token);
            var message = new OutboundMessage("external", [41], "external-destination")
            {
                Id = "external-stage-id", TenantId = "before-tenant", CorrelationId = "external-correlation",
            };
            await harness.Raw.StageMessageAsync(message, transaction, deadline.Token);
            (await harness.ReadAsync("external-correlation", deadline.Token)).ShouldBeEmpty("external work remains uncommitted");
            await using (var command = new NpgsqlCommand("SELECT count(*) FROM public.outbox WHERE message_id='external-stage-id'", harness.Connection, transaction))
            {
                command.CommandTimeout = 30;
                Convert.ToInt64(await command.ExecuteScalarAsync(deadline.Token), CultureInfo.InvariantCulture).ShouldBe(1);
            }
            await transaction.RollbackAsync(deadline.Token);
            (await harness.ReadAsync("external-correlation", deadline.Token)).ShouldBeEmpty();
            harness.Connection.State.ShouldBe(ConnectionState.Open);
            await using var probe = new NpgsqlCommand("SELECT 1", harness.Connection) { CommandTimeout = 30 };
            (await probe.ExecuteScalarAsync(deadline.Token)).ShouldBe(1);
        });
    }

    private static IOutboxPayloadTransformStore Facet(PostgresOutboxStore raw) =>
        ((IOutboxStore)raw).GetService(typeof(IOutboxPayloadTransformStore)).ShouldBeAssignableTo<IOutboxPayloadTransformStore>().ShouldNotBeNull();
    private static MessageContext Context(IServiceProvider services, InputMessage input, string? id, bool destination)
    {
        var context = new MessageContext(input, services) { MessageId = id, CorrelationId = "before-correlation", CausationId = "before-cause" };
        context.GetOrCreateIdentityFeature().TenantId = "before-tenant";
        context.SetPropagatedItem("custom", "before-header");
        context.Items["request-only"] = "must-not-persist";
        if (destination) { context.SetDestination("before-destination"); }
        return context;
    }
    private static void AssertEnvelope(Row row, string? contextId, bool destination)
    {
        if (string.IsNullOrEmpty(contextId))
        {
            row.Id.ShouldNotBeNullOrWhiteSpace();
            Guid.TryParse(row.Id, out var parsed).ShouldBeTrue(); parsed.ShouldNotBe(Guid.Empty);
        }
        else { row.Id.ShouldBe(contextId); }
        row.Type.ShouldBe(nameof(InputMessage));
        row.Destination.ShouldBe(destination ? "before-destination" : typeof(InputMessage).FullName);
        row.Tenant.ShouldBe("before-tenant"); row.Correlation.ShouldBe("before-correlation"); row.Causation.ShouldBe("before-cause");
        using var metadata = JsonDocument.Parse(row.Headers);
        var headers = metadata.RootElement;
        headers.GetProperty("MessageId").GetString().ShouldBe(contextId ?? string.Empty);
        headers.GetProperty("CorrelationId").GetString().ShouldBe("before-correlation");
        headers.GetProperty("CausationId").GetString().ShouldBe("before-cause");
        headers.GetProperty("MessageType").GetString().ShouldBe(nameof(InputMessage));
        headers.GetProperty("custom").GetString().ShouldBe("before-header");
        DateTimeOffset.TryParse(headers.GetProperty("Timestamp").GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out _).ShouldBeTrue();
        headers.TryGetProperty("request-only", out _).ShouldBeFalse();
    }
    private async Task RunAsync(EncryptionMode? mode, Func<Harness, Task> assertion)
    {
        fixture.DockerAvailable.ShouldBeTrue("these controls require real PostgreSQL, never a no-op");
        var harness = new Harness(fixture.ConnectionString, mode);
        Exception? primary = null;
        Exception? cleanup = null;
        try { await harness.InitializeAsync(); await assertion(harness); }
        catch (Exception error) { primary = error; }
        finally
        {
            try { await harness.DisposeAsync(); }
            catch (Exception error) { cleanup = error; }
        }
        if (primary is not null)
        {
            if (cleanup is not null) { primary.Data["SecondaryOwnedCleanup"] = cleanup; }
            ExceptionDispatchInfo.Capture(primary).Throw();
        }
        if (cleanup is not null) { ExceptionDispatchInfo.Capture(cleanup).Throw(); }
    }
    private enum InputState { Ready }
    private sealed class InputMessage : IDispatchMessage
    {
        private string _secret = "before";
        internal int SecretReads { get; private set; }
        internal Action? SecretReadAction { get; set; }
        public string Secret { get { SecretReads++; SecretReadAction?.Invoke(); return _secret; } set => _secret = value; }
        public InputState State { get; set; } = InputState.Ready;
        public string? Optional { get; set; }
    }
    private sealed record Row(string Id, string Type, byte[] Payload, string Headers, string Tenant, string Destination, string Correlation, string Causation);
    private sealed class ObservedDb(NpgsqlConnection connection) : IDb
    {
        private Action? _beforeUse;
        private int _reads;
        internal int Reads => Volatile.Read(ref _reads);
        internal Action? BeforeUse { set => Volatile.Write(ref _beforeUse, value); }
        public IDbConnection Connection
        {
            get
            {
                Interlocked.Increment(ref _reads);
                Interlocked.Exchange(ref _beforeUse, null)?.Invoke();
                return connection;
            }
        }
        public void Open() => connection.Open();
        public void Close() => connection.Close();
        public Task OpenAsync(CancellationToken token) => connection.OpenAsync(token);
        public Task CloseAsync() => connection.CloseAsync();
    }
    private sealed class Harness(string baseConnection, EncryptionMode? mode) : IAsyncDisposable
    {
        private readonly string _database = "transform_pg_" + Guid.NewGuid().ToString("N");
        private readonly InMemoryKeyManagementProvider _keys = new(NullLogger<InMemoryKeyManagementProvider>.Instance,
            new InMemoryKeyManagementOptions { AutoGenerateDefaultKey = true });
        private AesGcmEncryptionProvider? _real;
        private Task? _pending;
        private string ConnectionString => new NpgsqlConnectionStringBuilder(baseConnection) { Database = _database, Pooling = false }.ConnectionString;
        internal NpgsqlConnection Connection { get; private set; } = null!;
        internal ObservedDb Db { get; private set; } = null!;
        internal ServiceProvider Services { get; private set; } = null!;
        internal PostgresOutboxStore Raw { get; private set; } = null!;
        internal IOutboxStore Store { get; private set; } = null!;
        internal async Task InitializeAsync()
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await ExecuteAsync(baseConnection, $"CREATE DATABASE \"{_database}\";", deadline.Token);
            await ExecuteAsync(ConnectionString, ShippedSchemaScript.Read("src/Excalibur/Excalibur.Outbox.Postgres/Scripts/001_CreateOutboxSchema.sql"), deadline.Token);
            Connection = new NpgsqlConnection(ConnectionString); await Connection.OpenAsync(deadline.Token);
            Db = new ObservedDb(Connection);
            var services = new ServiceCollection(); services.AddLogging();
            services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox => outbox.UsePostgres(pg => pg.ConnectionFactory(_ => Db))));
            if (mode is { } selected)
            {
                _real = new AesGcmEncryptionProvider(_keys, NullLogger<AesGcmEncryptionProvider>.Instance);
                var registry = A.Fake<IEncryptionProviderRegistry>();
                A.CallTo(() => registry.GetPrimary()).Returns(_real);
                A.CallTo(() => registry.FindDecryptionProvider(A<EncryptedData>._)).Returns(_real);
                services.AddSingleton(registry);
                services.Configure<EncryptionOptions>(options => options.Mode = selected);
                services.AddOutboxEncryption();
            }
            Services = services.BuildServiceProvider(validateScopes: true);
            Raw = Services.GetRequiredService<PostgresOutboxStore>();
            Store = Services.GetRequiredKeyedService<IOutboxStore>("postgres");
        }
        internal void DeferUntil(Task operation) => _pending = operation;
        internal async Task<List<Row>> ReadAsync(string correlation, CancellationToken token)
        {
            await using var connection = new NpgsqlConnection(ConnectionString); await connection.OpenAsync(token);
            await using var command = new NpgsqlCommand("SELECT message_id,message_type,message_body,message_metadata,tenant_id,destination,correlation_id,causation_id FROM public.outbox WHERE correlation_id=@correlation", connection) { CommandTimeout = 30 };
            _ = command.Parameters.AddWithValue("correlation", correlation);
            await using var reader = await command.ExecuteReaderAsync(token);
            var rows = new List<Row>();
            while (await reader.ReadAsync(token)) { rows.Add(new Row(reader.GetString(0), reader.GetString(1), reader.GetFieldValue<byte[]>(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7))); }
            return rows;
        }
        public async ValueTask DisposeAsync()
        {
            if (_pending is { IsCompleted: false } pending)
            {
                _ = DisposeAfterOperationAsync(pending);
                return;
            }
            await DisposeCoreAsync();
        }
        private async Task DisposeAfterOperationAsync(Task operation)
        {
            try { await operation; }
            catch (Exception error) { ReportLate("Owned operation fault", error); }
            try { await DisposeCoreAsync(); }
            catch (Exception error) { ReportLate("Owned cleanup fault", error); }
        }
        private static void ReportLate(string purpose, Exception error)
        {
            // Diagnostic listeners are consumer code; reporting must not fault a deferred cleanup task.
            try { Trace.WriteLine(purpose + ": " + error.GetType().Name); }
            catch (Exception) { }
        }
        private async ValueTask DisposeCoreAsync()
        {
            Exception? first = null;
            try { if (Services is not null) { await Services.DisposeAsync(); } }
            catch (Exception error) { first = error; }
            try { if (Connection is not null) { await Connection.DisposeAsync(); } }
            catch (Exception error) { if (first is null) { first = error; } else { first.Data["SecondaryConnectionCleanup"] = error; } }
            try { _real?.Dispose(); }
            catch (Exception error) { if (first is null) { first = error; } else { first.Data["SecondaryEncryptionCleanup"] = error; } }
            try { _keys.Dispose(); }
            catch (Exception error) { if (first is null) { first = error; } else { first.Data["SecondaryKeyCleanup"] = error; } }
            try
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await ExecuteAsync(baseConnection, $"DROP DATABASE IF EXISTS \"{_database}\" ;", cleanup.Token);
            }
            catch (Exception error) { if (first is null) { first = error; } else { first.Data["SecondaryDatabaseCleanup"] = error; } }
            if (first is not null) { ExceptionDispatchInfo.Capture(first).Throw(); }
        }
        [SuppressMessage("Security", "CA2100", Justification = "Only fixed fixture SQL, unchanged shipped DDL, or private GUID-hex database names reach this helper.")]
        private static async Task ExecuteAsync(string connectionString, string text, CancellationToken token)
        {
            await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(token);
            await using var command = new NpgsqlCommand(text, connection) { CommandTimeout = 30 };
            _ = await command.ExecuteNonQueryAsync(token);
        }
    }
}
