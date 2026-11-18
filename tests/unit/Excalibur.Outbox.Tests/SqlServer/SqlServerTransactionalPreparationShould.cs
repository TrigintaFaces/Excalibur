// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;
using System.Text.Json.Serialization;
using Excalibur.Dispatch;
using Excalibur.Outbox.SqlServer;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Outbox.Tests.SqlServer;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
public sealed class SqlServerTransactionalPreparationShould
{
    [Fact]
    public async Task FreezeActualHeadersBodyTransportTenantAndTablesBeforeTransformWaitWithoutOpeningConnection()
    {
        var options = new SqlServerOutboxOptions();
        var store = Create(options);
        var prepare = Facet<ITransactionalOutboxPreparer>(store);
        var transform = Facet<ITransactionalOutboxPayloadTransformPreparer>(store);
        var header = new PreparationHeader { Text = "before-\u03bb" };
        var message = new OutboundMessage("type", [1, 2, 3], "destination")
        {
            Id = "stable-prepared-id", TenantId = "tenantA ", CorrelationId = "before-correlation", CausationId = "before-cause",
            PartitionKey = "partition", GroupKey = "group", SequenceNumber = 7, IsMultiTransport = true, TargetTransports = "one"
        };
        message.Headers["custom"] = header;
        var delivery = new OutboundMessageTransport(message.Id, "one") { TransportMetadata = "{\"before\":true}" };
        message.TransportDeliveries.Add(delivery);
        var expected = (await prepare.PrepareMessageAsync(message, CancellationToken.None)).GetCanonicalWireBytes();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var returned = new byte[] { 1, 2, 3 };
        var calls = 0;
        var pending = transform.PrepareMessageWithPayloadTransformAsync(message, async (bytes, tenant, token) =>
        {
            calls++; entered.TrySetResult();
            bytes.ToArray().ShouldBe(new byte[] { 1, 2, 3 }); tenant.ShouldBe("tenantA "); await release.Task.WaitAsync(token); return returned;
        }, deadline.Token).AsTask();
        try
        {
            await entered.Task.WaitAsync(deadline.Token);
            header.Text = "after"; message.Payload[0] = 99; message.TenantId = "other"; message.CorrelationId = "after";
            delivery.TransportMetadata = "after"; options.Tables.OutboxTableName = "OtherOutbox";
        }
        finally { release.TrySetResult(); }
        var prepared = await pending.WaitAsync(deadline.Token);
        prepared.TenantId.ShouldBe("tenantA "); prepared.MessageId.ShouldBe("stable-prepared-id");
        returned[0] = 77; prepared.GetCanonicalWireBytes().ShouldBe(expected); calls.ShouldBe(1);
        var borrowedResult = prepared.GetCanonicalWireBytes(); borrowedResult[0] ^= 255;
        prepared.GetCanonicalWireBytes().ShouldBe(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task RefuseTransformFaultNullOrCanceledSuccessBeforeAcceptingHandle(int kind)
    {
        var store = Create(new SqlServerOutboxOptions());
        var prepare = Facet<ITransactionalOutboxPayloadTransformPreparer>(store);
        using var cancellation = new CancellationTokenSource();
        var expected = new IOException("exact-prepare-fault");
        async ValueTask<byte[]> Transform(ReadOnlyMemory<byte> bytes, string? tenant, CancellationToken token)
        {
            await Task.CompletedTask;
            if (kind == 0) { throw expected; }
            if (kind == 1) { return null!; }
            cancellation.Cancel(); return bytes.ToArray();
        }
        var operation = prepare.PrepareMessageWithPayloadTransformAsync(new OutboundMessage("type", [1], "destination"), Transform, cancellation.Token).AsTask();
        if (kind == 0) { (await Should.ThrowAsync<IOException>(() => operation)).ShouldBeSameAs(expected); }
        else if (kind == 1) { await Should.ThrowAsync<InvalidOperationException>(() => operation); }
        else { await Should.ThrowAsync<OperationCanceledException>(() => operation); }
    }

    [Fact]
    public async Task RefuseUnsupportedTransactionWithoutOpeningAnotherConnection()
    {
        var store = Create(new SqlServerOutboxOptions());
        var prepare = Facet<ITransactionalOutboxPreparer>(store);
        var write = await prepare.PrepareMessageAsync(new OutboundMessage("type", [1], "destination"), CancellationToken.None);
        await Should.ThrowAsync<InvalidOperationException>(() => write.StageAsync(A.Fake<IDbTransaction>(), CancellationToken.None).AsTask());
    }

    [Fact]
    public void WithholdPreparationThroughUnknownDecorator()
    {
        var store = Create(new SqlServerOutboxOptions());
        ((IServiceProvider)store).GetService(typeof(ITransactionalOutboxPreparer)).ShouldNotBeNull();
        var unknown = new UnknownPayloadDecoration(store);
        unknown.GetService(typeof(ITransactionalOutboxPreparer)).ShouldBeNull();
        unknown.GetService(typeof(ITransactionalOutboxPayloadTransformPreparer)).ShouldBeNull();
    }

    [Fact]
    public async Task RefuseMissingGeneratedHeaderMetadataBeforeCallingTransform()
    {
        var store = Create(new SqlServerOutboxOptions());
        var message = new OutboundMessage("type", [1], "destination");
        message.Headers["missing"] = new MissingPreparationHeader();
        var calls = 0;
        await Should.ThrowAsync<NotSupportedException>(() => Facet<ITransactionalOutboxPayloadTransformPreparer>(store)
            .PrepareMessageWithPayloadTransformAsync(message, (bytes, tenant, token) =>
            { calls++; return ValueTask.FromResult(bytes.ToArray()); }, CancellationToken.None).AsTask());
        calls.ShouldBe(0);
    }

    [Fact]
    public async Task BindLogicalBackendWithoutPersistingCredentialsOrTreatingDifferentCatalogAsEquivalent()
    {
        var message = new OutboundMessage("type", [1], "destination") { Id = "logical-binding" };
        var first = Create(new SqlServerOutboxOptions());
        var same = Create(new SqlServerOutboxOptions(),
            "Server=127.0.0.1,1;Database=prepared;User ID=other-user;Password=other-secret;Connect Timeout=1");
        var different = Create(new SqlServerOutboxOptions(),
            "Server=127.0.0.1,1;Database=different-catalog;User ID=prep;Password=pass-secret-A;Connect Timeout=1");
        var expected = (await Facet<ITransactionalOutboxPreparer>(first).PrepareMessageAsync(message, CancellationToken.None)).GetCanonicalWireBytes();
        (await Facet<ITransactionalOutboxPreparer>(same).PrepareMessageAsync(message, CancellationToken.None))
            .GetCanonicalWireBytes().ShouldBe(expected);
        var differentBytes = (await Facet<ITransactionalOutboxPreparer>(different).PrepareMessageAsync(message, CancellationToken.None))
            .GetCanonicalWireBytes();
        expected.SequenceEqual(differentBytes).ShouldBeFalse();
        expected.AsSpan().IndexOf(Encoding.Unicode.GetBytes("pass-secret-A")).ShouldBe(-1);
        expected.AsSpan().IndexOf(Encoding.Unicode.GetBytes("other-secret")).ShouldBe(-1);
    }

    private static T Facet<T>(IOutboxStore store) where T : class =>
        ((IServiceProvider)store).GetService(typeof(T)) as T ?? throw new InvalidOperationException("Required preparation facet missing.");

    private static SqlServerOutboxStore Create(SqlServerOutboxOptions options,
        string connectionString = "Server=127.0.0.1,1;Database=prepared;User ID=prep;Password=pass-secret-A;Connect Timeout=1") => new(
        () => new SqlConnection(connectionString), options,
        payloadSerializer: null, inboxOptions: null, logger: NullLogger<SqlServerOutboxStore>.Instance,
        metadataSerialization: new OutboxMetadataSerialization(value => new PreparationHeaderContext(value)));

    private sealed class UnknownPayloadDecoration(IOutboxStore inner) : OutboxStoreDecorator(inner);
}

internal sealed class PreparationHeader
{
    public string Text { get; set; } = string.Empty;
}

[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(PreparationHeader))]
[JsonSerializable(typeof(string))]
internal sealed partial class PreparationHeaderContext : JsonSerializerContext;

internal sealed class MissingPreparationHeader;
