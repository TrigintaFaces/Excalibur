// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Text.Json;
using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;
using Excalibur.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Oracle.ManagedDataAccess.Client;
using Shouldly;
using Xunit;

namespace Excalibur.Outbox.Oracle.Tests;

public sealed partial class OracleOutboxEnqueueTransformShould
{
    private static readonly byte[] ExpectedCallbackResult = [37, 0, 255];
    private static readonly byte[] CanceledCallbackResult = [9];
    [Fact]
    public async Task PrepareTheEnvelopeBeforeCallbackAndCopyTheResultBeforeConnectionHandoff()
    {
        await RunAsync(null, async harness =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await using var connection = fixture.CreateConnection(); await connection.OpenAsync(deadline.Token);
            var db = new CallbackObservedDb(connection);
            await using var services = BorrowedServices(db);
            var raw = services.GetRequiredService<OracleOutboxStore>();
            var facet = RequireOracleTransform(raw); var input = new InputMessage();
            var context = Context(harness, input, harness.Correlation, true);
            byte[] result = [37, 0, 255]; var calls = 0;
            db.BeforeUse = () => result[0] = 99;
            await facet.EnqueueWithPayloadTransformAsync(input, context, async (bytes, tenant, token) =>
            {
                calls++; bytes.ToArray().ShouldBe(CanonicalBytes); tenant.ShouldBe("before-tenant"); token.ShouldBe(deadline.Token);
                db.Reads.ShouldBe(0); (await harness.ReadAsync(token)).ShouldBeEmpty();
                context.MessageId = "changed-id"; context.CorrelationId = "changed-correlation"; context.CausationId = "changed-cause";
                context.GetOrCreateIdentityFeature().TenantId = "changed-tenant"; context.SetDestination("changed-destination");
                bytes.ToArray().ShouldBe(CanonicalBytes, "borrowed input remains stable during invocation");
                return result;
            }, deadline.Token);
            calls.ShouldBe(1); db.Reads.ShouldBe(1); result[0].ShouldBe((byte)99);
            var row = (await harness.ReadAsync(deadline.Token)).Single();
            AssertEnvelope(harness, row, harness.Correlation, true);
            row.Payload.ShouldBe(ExpectedCallbackResult);
            await services.DisposeAsync();
            connection.State.ShouldBe(ConnectionState.Open, "disposing the provider must leave its supplied staging lifecycle borrowed");
            await using var ownershipProbe = connection.CreateCommand();
            ownershipProbe.CommandText = "SELECT 1 FROM DUAL"; ownershipProbe.CommandTimeout = 30;
            Convert.ToInt32(await ownershipProbe.ExecuteScalarAsync(deadline.Token), System.Globalization.CultureInfo.InvariantCulture).ShouldBe(1);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task RefuseCallbackFaultNullAndCancellationWithoutAcceptingOracleRows(int outcome)
    {
        await RunAsync(null, async harness =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var operation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            await using var connection = fixture.CreateConnection(); await connection.OpenAsync(deadline.Token);
            var db = new CallbackObservedDb(connection); await using var services = BorrowedServices(db);
            var facet = RequireOracleTransform(services.GetRequiredService<OracleOutboxStore>());
            var input = new InputMessage(); var context = Context(harness, input, harness.Correlation, true);
            var fault = new IOException("callback-fault"); var calls = 0;
            Func<ReadOnlyMemory<byte>, string?, CancellationToken, ValueTask<byte[]>>? callback = (bytes, tenant, token) =>
            {
                calls++; bytes.ToArray().ShouldBe(CanonicalBytes); tenant.ShouldBe("before-tenant"); token.ShouldBe(operation.Token);
                if (outcome == 0) { return ValueTask.FromException<byte[]>(fault); }
                if (outcome == 1) { return ValueTask.FromResult<byte[]>(null!); }
                operation.Cancel();
                if (outcome == 2) { return new ValueTask<byte[]>(Task.FromCanceled<byte[]>(operation.Token)); }
                return ValueTask.FromResult(CanceledCallbackResult);
            };
            if (outcome == 4) { callback = null; }
            if (outcome == 5) { operation.Cancel(); }
            Exception? observed = null;
            try { await facet.EnqueueWithPayloadTransformAsync(input, context, callback!, operation.Token); }
            catch (Exception error) { observed = error; }
            observed.ShouldNotBeNull();
            if (outcome == 0) { observed.ShouldBeSameAs(fault); }
            else if (outcome == 1) { observed.ShouldBeOfType<InvalidOperationException>(); }
            else if (outcome == 4) { observed.ShouldBeOfType<ArgumentNullException>(); }
            else { observed.ShouldBeAssignableTo<OperationCanceledException>().ShouldNotBeNull().CancellationToken.ShouldBe(operation.Token); }
            calls.ShouldBe(outcome is 4 or 5 ? 0 : 1); db.Reads.ShouldBe(0);
            input.SecretReads.ShouldBe(outcome is 4 or 5 ? 0 : 1);
            (await harness.ReadAsync(deadline.Token)).ShouldBeEmpty();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CaptureTheRequiredOracleContextIdentityBeforeSerialization(bool transformed)
    {
        await RunAsync(null, async harness =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var input = new IdentityChangingMessage();
            var expectedId = "  " + harness.Correlation + "  ";
            var context = new Excalibur.Dispatch.Messaging.MessageContext(input, harness.Services)
            { MessageId = expectedId, CorrelationId = harness.Correlation };
            input.OnRead = () => context.MessageId = "changed-during-serialization";
            if (transformed)
            {
                var facet = RequireOracleTransform(harness.Raw);
                await facet.EnqueueWithPayloadTransformAsync(input, context, (bytes, tenant, token) => ValueTask.FromResult(bytes.ToArray()), deadline.Token);
            }
            else { await harness.Store.EnqueueAsync(input, context, deadline.Token); }
            var row = (await harness.ReadAsync(deadline.Token)).Single();
            row.Id.ShouldBe(expectedId); row.Payload.ShouldBe("{\"secret\":\"before\"}"u8.ToArray()); input.Reads.ShouldBe(1);
            using var metadata = JsonDocument.Parse(row.Headers); metadata.RootElement.GetProperty("MessageId").GetString().ShouldBe(expectedId);
        });
    }

    [Fact]
    public async Task AcceptAnEmptyTransformResultAsANonNullEmptyBlob()
    {
        await RunAsync(null, async harness =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30)); var calls = 0;
            var facet = RequireOracleTransform(harness.Raw); var input = new InputMessage();
            await facet.EnqueueWithPayloadTransformAsync(input, Context(harness, input, harness.Correlation, true), (bytes, tenant, token) =>
            { calls++; bytes.ToArray().ShouldBe(CanonicalBytes); return ValueTask.FromResult(Array.Empty<byte>()); }, deadline.Token);
            calls.ShouldBe(1); var row = (await harness.ReadAsync(deadline.Token)).Single();
            row.IsNull.ShouldBeFalse(); row.Length.ShouldBe(0L); row.Payload.ShouldBeEmpty();
        });
    }
    private ServiceProvider BorrowedServices(CallbackObservedDb db)
    {
        var services = new ServiceCollection(); services.AddLogging();
        services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox => outbox.UseOracle(oracle => oracle
            .ConnectionFactory(_ => db).WorkerConnectionFactory((_, token) => ValueTask.FromResult(fixture.CreateConnection())))));
        return services.BuildServiceProvider(validateScopes: true);
    }
    private static IOutboxPayloadTransformStore RequireOracleTransform(OracleOutboxStore raw) =>
        ((IOutboxStore)raw).GetService(typeof(IOutboxPayloadTransformStore)).ShouldBeAssignableTo<IOutboxPayloadTransformStore>().ShouldNotBeNull();
    private sealed class IdentityChangingMessage : IDispatchMessage
    {
        internal int Reads { get; private set; }
        internal Action? OnRead { get; set; }
        public string Secret { get { Reads++; OnRead?.Invoke(); return "before"; } }
    }
    private sealed class CallbackObservedDb(OracleConnection connection) : IDb
    {
        private int _reads; private Action? _beforeUse;
        internal int Reads => Volatile.Read(ref _reads);
        internal Action? BeforeUse { set => Volatile.Write(ref _beforeUse, value); }
        public IDbConnection Connection { get { Interlocked.Increment(ref _reads); Interlocked.Exchange(ref _beforeUse, null)?.Invoke(); return connection; } }
        public void Open() => connection.Open();
        public void Close() => connection.Close();
        public Task OpenAsync(CancellationToken token) => connection.OpenAsync(token);
        public Task CloseAsync() => connection.CloseAsync();
    }
}
