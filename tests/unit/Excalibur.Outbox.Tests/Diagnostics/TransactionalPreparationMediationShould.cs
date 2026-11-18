// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;

using Excalibur.Dispatch;
using Excalibur.Outbox.Diagnostics;

namespace Excalibur.Outbox.Tests.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class TransactionalPreparationMediationShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MediatePreparationAndStageOnTheOriginalTransaction(bool transformed)
    {
        var inner = A.Fake<IOutboxStore>();
        var preparer = A.Fake<ITransactionalOutboxPreparer>();
        var transformPreparer = A.Fake<ITransactionalOutboxPayloadTransformPreparer>();
        var write = A.Fake<IPreparedTransactionalOutboxWrite>();
        using var transaction = A.Fake<IDbTransaction>();
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var message = new OutboundMessage("type", [1, 2], "destination");
        Func<ReadOnlyMemory<byte>, string?, CancellationToken, ValueTask<byte[]>> transform =
            static (payload, _, _) => ValueTask.FromResult(payload.ToArray());
        A.CallTo(() => inner.GetService(typeof(ITransactionalOutboxPreparer))).Returns(preparer);
        A.CallTo(() => inner.GetService(typeof(ITransactionalOutboxPayloadTransformPreparer))).Returns(transformPreparer);
        A.CallTo(() => preparer.PrepareMessageAsync(message, token))
            .ReturnsLazily(() => ValueTask.FromResult(write));
        A.CallTo(() => transformPreparer.PrepareMessageWithPayloadTransformAsync(message, transform, token))
            .ReturnsLazily(() => ValueTask.FromResult(write));
        A.CallTo(() => write.MessageId).Returns("original-row");
        A.CallTo(() => write.TenantId).Returns("tenant ");
        A.CallTo(() => write.GetCanonicalWireBytes()).ReturnsLazily(() => new byte[] { 7, 8 });
        A.CallTo(() => write.StageAsync(transaction, token)).ReturnsLazily(() => ValueTask.CompletedTask);
        using var decorated = new TelemetryOutboxStoreDecorator(inner);

        var mediated = transformed
            ? await ((ITransactionalOutboxPayloadTransformPreparer)decorated.GetService(typeof(ITransactionalOutboxPayloadTransformPreparer))!)
                .PrepareMessageWithPayloadTransformAsync(message, transform, token)
            : await ((ITransactionalOutboxPreparer)decorated.GetService(typeof(ITransactionalOutboxPreparer))!)
                .PrepareMessageAsync(message, token);

        mediated.ShouldNotBeSameAs(write);
        mediated.MessageId.ShouldBe("original-row");
        mediated.TenantId.ShouldBe("tenant ");
        mediated.GetCanonicalWireBytes().ShouldBe(new byte[] { 7, 8 });
        A.CallTo(() => write.StageAsync(A<IDbTransaction>._, A<CancellationToken>._)).MustNotHaveHappened();
        await mediated.StageAsync(transaction, token);
        A.CallTo(() => write.StageAsync(transaction, token)).MustHaveHappenedOnceExactly();
        A.CallTo(() => transaction.Commit()).MustNotHaveHappened();
        A.CallTo(() => transaction.Rollback()).MustNotHaveHappened();
        if (transformed)
        {
            A.CallTo(() => transformPreparer.PrepareMessageWithPayloadTransformAsync(message, transform, token)).MustHaveHappenedOnceExactly();
            A.CallTo(() => preparer.PrepareMessageAsync(A<OutboundMessage>._, A<CancellationToken>._)).MustNotHaveHappened();
        }
        else
        {
            A.CallTo(() => preparer.PrepareMessageAsync(message, token)).MustHaveHappenedOnceExactly();
            A.CallTo(() => transformPreparer.PrepareMessageWithPayloadTransformAsync(A<OutboundMessage>._,
                A<Func<ReadOnlyMemory<byte>, string?, CancellationToken, ValueTask<byte[]>>>._, A<CancellationToken>._)).MustNotHaveHappened();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithholdPreparationWhenTheInnerFacetIsAbsent(bool transformed)
    {
        var inner = A.Fake<IOutboxStore>();
        var capability = transformed ? typeof(ITransactionalOutboxPayloadTransformPreparer) : typeof(ITransactionalOutboxPreparer);
        A.CallTo(() => inner.GetService(capability)).Returns(null);
        using var decorated = new TelemetryOutboxStoreDecorator(inner);
        decorated.GetService(capability).ShouldBeNull();
    }
}
