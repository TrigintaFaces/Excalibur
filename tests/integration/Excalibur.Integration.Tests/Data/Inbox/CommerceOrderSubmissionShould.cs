// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using Excalibur.Dispatch.Examples.ECommerceSample;
using Microsoft.Data.SqlClient;

namespace Excalibur.Integration.Tests.Data.Inbox;

[Collection(SqlServerTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Sample.OrderSubmission")]
[Trait("Pattern", "Regression")]
[Trait("Database", "SqlServer")]
public sealed partial class CommerceOrderSubmissionShould(SqlServerContainerFixture fixture) : IAsyncLifetime
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RollBackBusinessIntentAndInboxWhenTheTransactionalCallbackFails(bool afterRealStage)
    {
        var original = new IOException(afterRealStage ? "after-real-stage" : "before-real-stage");
        var actualWriter = (ITransactionalOutboxWriter)_outbox.GetService(typeof(ITransactionalOutboxWriter))!;
        var writer = A.Fake<ITransactionalOutboxWriter>();
        A.CallTo(() => writer.StageMessageAsync(A<OutboundMessage>._, A<IDbTransaction>._, A<CancellationToken>._))
            .ReturnsLazily((OutboundMessage message, IDbTransaction transaction, CancellationToken token) =>
                new ValueTask(FailAsync(message, transaction, token)));
        async Task FailAsync(OutboundMessage message, IDbTransaction transaction, CancellationToken token)
        {
            if (afterRealStage)
            {
                await actualWriter.StageMessageAsync(message, transaction, token).ConfigureAwait(false);
            }

            throw original;
        }

        var order = SqlCommerceProfile.DemoOrder();
        var observed = await Should.ThrowAsync<IOException>(() => Subject(writer).ProcessAsync(order, Ct));
        observed.ShouldBeSameAs(original);
        (await CountsAsync(order, Ct)).ShouldBe((0L, 0L, 0L));
        (await Subject().ProcessAsync(order, Ct)).ShouldBe(OrderProcessingOutcome.Processed);
        await AssertCommittedAsync(order, Ct);
        var payload = await IntentPayloadAsync(order, Ct);
        (await Subject().ProcessAsync(order, Ct)).ShouldBe(OrderProcessingOutcome.Duplicate);
        (await IntentPayloadAsync(order, Ct)).ShouldBe(payload);
        await AssertCommittedAsync(order, Ct);
    }

    [Fact]
    public async Task DistinguishMatchingDuplicateConflictMissingEvidenceAndRealReadFailure()
    {
        var order = SqlCommerceProfile.DemoOrder();
        var service = Subject();
        (await service.ProcessAsync(order, Ct)).ShouldBe(OrderProcessingOutcome.Processed);
        var payload = await IntentPayloadAsync(order, Ct);
        (await service.ProcessAsync(order with { Price = order.Price + 1 }, Ct)).ShouldBe(OrderProcessingOutcome.Conflict);
        (await service.ProcessAsync(order, Ct)).ShouldBe(OrderProcessingOutcome.Duplicate);
        (await IntentPayloadAsync(order, Ct)).ShouldBe(payload);

        var missingDatabase = new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = _database + "_missing" };
        await Should.ThrowAsync<SqlException>(() => Subject(connections: () => new SqlConnection(missingDatabase.ConnectionString))
            .ProcessAsync(order, Ct));
        await AssertCommittedAsync(order, Ct);
        // Only the test-owned database bypasses its immutable-row trigger, to simulate missing evidence.
        await ExecuteAsync("DISABLE TRIGGER dbo.CommerceOrdersImmutable ON dbo.CommerceOrders; DELETE FROM dbo.CommerceOrders;", Ct);
        (await service.ProcessAsync(order, Ct)).ShouldBe(OrderProcessingOutcome.NeedsReconciliation);
        (await IntentPayloadAsync(order, Ct)).ShouldBe(payload);
    }

    [Fact]
    public async Task RetainBusinessIdentityAfterActualInboxAndOutboxRetention()
    {
        var order = SqlCommerceProfile.DemoOrder();
        (await Subject().ProcessAsync(order, Ct)).ShouldBe(OrderProcessingOutcome.Processed);
        var original = await Subject().ReadAsync(order.OrderId, Ct);
        var originalPayload = await IntentPayloadAsync(order, Ct);
        var delivered = await RunChildAsync("--drain-only", Ct);
        AssertOneSenderReceipt(delivered);
        (await IntentPayloadAsync(order, Ct)).ShouldBe(originalPayload);
        await AssertCommittedAsync(order, Ct);
        var outboxAdmin = (IOutboxStoreAdmin)_outbox.GetService(typeof(IOutboxStoreAdmin))!;
        (await outboxAdmin.CleanupAllTenantsSentMessagesAsync(DateTimeOffset.UtcNow.AddMinutes(1), 10, Ct)).ShouldBe(1);
        (await Subject().ProcessAsync(order, Ct)).ShouldBe(OrderProcessingOutcome.Duplicate);
        var inboxAdmin = (IInboxStoreAdmin)_inbox;
        (await inboxAdmin.CleanupAllTenantsProcessedEntriesAsync(DateTimeOffset.UtcNow.AddMinutes(1), Ct)).ShouldBe(1);
        (await CountsAsync(order, Ct)).ShouldBe((1L, 0L, 0L));
        (await Subject().ProcessAsync(order, Ct)).ShouldBe(OrderProcessingOutcome.Duplicate);
        (await Subject().ReadAsync(order.OrderId, Ct)).ShouldBe(original);
        (await CountsAsync(order, Ct)).ShouldBe((1L, 0L, 1L));
        (await ProcessedInboxCountAsync(order, Ct)).ShouldBe(1L);
        var duplicateDrain = await RunChildAsync("--drain-only", Ct);
        SenderReceiptCount(duplicateDrain).ShouldBe(0);
        duplicateDrain.ShouldNotContain("Sent OrderConfirmation email to ");
        (await CountsAsync(order, Ct)).ShouldBe((1L, 0L, 1L));
        (await Subject().ReadAsync(order.OrderId, Ct)).ShouldBe(original);
        (await Subject().ProcessAsync(order with { Quantity = 2 }, Ct)).ShouldBe(OrderProcessingOutcome.Conflict);
    }

    [Fact]
    public async Task SerializeConcurrentMatchingSubmissionsAndRefuseChangedInputs()
    {
        var order = SqlCommerceProfile.DemoOrder();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<OrderProcessingOutcome> SubmitAsync(OrderCreated input)
        {
            await start.Task;
            return await Subject().ProcessAsync(input, Ct);
        }

        var first = SubmitAsync(order);
        var second = SubmitAsync(order);
        start.SetResult();
        var outcomes = await Task.WhenAll(first, second);
        outcomes.Count(value => value == OrderProcessingOutcome.Processed).ShouldBe(1);
        outcomes.Count(value => value == OrderProcessingOutcome.Duplicate).ShouldBe(1);
        await AssertCommittedAsync(order, Ct);
        var changed = order with { Price = 99m };
        var pair = await Task.WhenAll(Subject().ProcessAsync(order, Ct), Subject().ProcessAsync(changed, Ct));
        pair.ShouldContain(OrderProcessingOutcome.Duplicate);
        pair.ShouldContain(OrderProcessingOutcome.Conflict);
        await AssertCommittedAsync(order, Ct);
    }
}
