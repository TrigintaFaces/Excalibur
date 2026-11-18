// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using Excalibur.Dispatch.Examples.ECommerceSample;
using Microsoft.Data.SqlClient;

namespace Excalibur.Integration.Tests.Data.Inbox;

public sealed partial class CommerceOrderSubmissionShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReconcileTheOriginalBusinessIdentityAfterARealSqlCommitResponseIsWithheld(bool withholdResponse)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(90));
        var token = deadline.Token;
        var order = SqlCommerceProfile.DemoOrder();
        (await CountsAsync(order, token)).ShouldBe((0L, 0L, 0L));
        (await ProcessedInboxCountAsync(order, token)).ShouldBe(0L);
        using var diagnostics = new CommitDiagnostics();
        Task<OrderProcessingOutcome>? submission = null;
        Exception? primary = null;
        Exception? cleanupFailure = null;
        var expectedCommitFailure = false;
        var proxy = new AcknowledgementProxy(_connectionString);
        try
        {
            var registrations = new ServiceCollection();
            registrations.AddLogging();
            registrations.AddExcaliburInbox(inbox => inbox.UseSqlServer(sql => sql.ConnectionString(proxy.ConnectionString)));
            await using var proxiedServices = registrations.BuildServiceProvider();
            var actualWriter = (ITransactionalOutboxWriter)_outbox.GetService(typeof(ITransactionalOutboxWriter))!;
            var writer = A.Fake<ITransactionalOutboxWriter>();
            var flowReady = new TaskCompletionSource<AcknowledgementProxy.ProxyFlow>(TaskCreationOptions.RunContinuationsAsynchronously);
            var stageCalls = 0;
            A.CallTo(() => writer.StageMessageAsync(A<OutboundMessage>._, A<IDbTransaction>._, A<CancellationToken>._))
                .ReturnsLazily((OutboundMessage message, IDbTransaction transaction, CancellationToken cancellation) =>
                    new ValueTask(StageAndArmAsync(message, transaction, cancellation)));
            async Task StageAndArmAsync(OutboundMessage message, IDbTransaction transaction, CancellationToken cancellation)
            {
                Interlocked.Increment(ref stageCalls).ShouldBe(1);
                await actualWriter.StageMessageAsync(message, transaction, cancellation).ConfigureAwait(false);
                var connection = transaction.ShouldBeOfType<SqlTransaction>().Connection
                    ?? throw new InvalidOperationException("The actual SQL stage lost its transaction connection.");
                diagnostics.Bind(connection);
                flowReady.SetResult(proxy.Arm(connection, withholdResponse));
            }

            var selectedOutbox = A.Fake<IOutboxStore>();
            A.CallTo(() => selectedOutbox.GetService(typeof(ITransactionalOutboxWriter))).Returns(writer);
            var service = new SqlOrderSubmissionService(proxiedServices.GetRequiredService<IInboxStore>(), selectedOutbox,
                () => new SqlConnection(_connectionString));
            // Pinned SqlTransaction.CommitAsync may synchronously block; keep the observer runnable.
            submission = Task.Run(() => service.ProcessAsync(order, token), token);
            var flow = await flowReady.Task.WaitAsync(TimeSpan.FromSeconds(20), token);
            await flow.CommitForwarded.WaitAsync(TimeSpan.FromSeconds(20), token);
            _ = await diagnostics.Before.WaitAsync(TimeSpan.FromSeconds(20), token);
            flow.CommitCount.ShouldBe(1);
            if (withholdResponse)
            {
                await flow.ResponseHeld.WaitAsync(TimeSpan.FromSeconds(20), token);
                await AssertCommittedAsync(order, token);
                submission.IsCompleted.ShouldBeFalse();
                diagnostics.AssertPendingCommit();
                flow.HeldBytes.ShouldBeGreaterThan(0);
                flow.ForwardedWhileHeld.ShouldBe(0L);
                token.IsCancellationRequested.ShouldBeFalse();
                flow.Sever();
                _ = await diagnostics.Error.WaitAsync(TimeSpan.FromSeconds(20), token);
                // Wait only for completion; a gate timeout/cancellation escapes as a test failure.
                _ = await submission.ContinueWith(static finished => finished.IsCompleted, CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default)
                    .WaitAsync(TimeSpan.FromSeconds(20), token);
                submission.IsFaulted.ShouldBeTrue();
                Exception? observed = null;
                try
                {
                    _ = await submission;
                }
                catch (Exception error)
                {
                    observed = error;
                }

                observed.ShouldNotBeNull();
                observed.ShouldBeSameAs(submission.Exception!.InnerExceptions.Single());
                diagnostics.AssertCompleted(failed: true);
                expectedCommitFailure = true;
            }
            else
            {
                (await submission.WaitAsync(TimeSpan.FromSeconds(20), token)).ShouldBe(OrderProcessingOutcome.Processed);
                diagnostics.AssertCompleted(failed: false);
                flow.HeldBytes.ShouldBe(0);
            }

            await AssertCommittedAsync(order, token);
            var originalPayload = await IntentPayloadAsync(order, token);
            (await Subject().ProcessAsync(order, token)).ShouldBe(OrderProcessingOutcome.Duplicate);
            stageCalls.ShouldBe(1);
            (await IntentPayloadAsync(order, token)).ShouldBe(originalPayload);
            await AssertCommittedAsync(order, token);
            AssertOneSenderReceipt(await RunChildAsync("--drain-only", token));
            SenderReceiptCount(await RunChildAsync("--drain-only", token)).ShouldBe(0);
            (await Subject().ProcessAsync(order, token)).ShouldBe(OrderProcessingOutcome.Duplicate);
            (await CountsAsync(order, token)).ShouldBe((1L, 1L, 1L));
        }
        catch (Exception error)
        {
            primary = error;
            throw;
        }
        finally
        {
            var cleanup = await CleanupAcknowledgementAsync(proxy, submission, expectedCommitFailure);
            if (cleanup is not null)
            {
                if (primary is not null)
                {
                    primary.Data["CommerceAcknowledgementCleanup"] = cleanup;
                }
                else
                {
                    cleanupFailure = cleanup;
                }
            }
        }

        if (cleanupFailure is not null)
        {
            throw cleanupFailure;
        }
    }

    private static async Task<Exception?> CleanupAcknowledgementAsync(AcknowledgementProxy proxy,
        Task<OrderProcessingOutcome>? submission, bool expectedCommitFailure)
    {
        var failures = new List<Exception>();
        try { await proxy.DisposeAsync(); }
        catch (Exception cleanup) { failures.Add(cleanup); }
        if (submission is not null)
        {
            try
            {
                _ = await submission.WaitAsync(TimeSpan.FromSeconds(20), CancellationToken.None);
            }
            catch (Exception cleanup)
            {
                if (!expectedCommitFailure)
                {
                    failures.Add(cleanup);
                }
            }
        }

        return failures.Count == 0 ? null : new AggregateException("Owned acknowledgement control cleanup failed.", failures);
    }
}
