// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.ErrorHandling;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.ErrorHandling;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Regression")]
public sealed class InMemoryDeadLetterStoreShould
{
    [Fact]
    public async Task CombineTypeReasonAndPendingFilters()
    {
        var store = CreateStore();
        await store.StoreAsync(Message("match", 1), CancellationToken.None);
        await store.StoreAsync(Message("other-type", 2, type: "invoice"), CancellationToken.None);
        await store.StoreAsync(Message("other-reason", 3, reason: "invalid"), CancellationToken.None);
        await store.StoreAsync(Message("replayed", 4, replayed: true), CancellationToken.None);
        var matches = await store.GetMessagesAsync(new DeadLetterFilter
        {
            MessageType = "order", Reason = "TIMEOUT", IsReplayed = false,
        }, CancellationToken.None);
        matches.Select(message => message.MessageId).ShouldBe(["match"]);
        (await store.GetCountAsync(CancellationToken.None)).ShouldBe(4);
    }

    [Fact]
    public async Task ReturnOnlyPendingMessagesAfterReplay()
    {
        var store = CreateStore();
        await store.StoreAsync(Message("pending", 1), CancellationToken.None);
        await store.StoreAsync(Message("replayed", 2), CancellationToken.None);
        await store.MarkAsReplayedAsync("replayed", CancellationToken.None);
        var matches = await store.GetMessagesAsync(new DeadLetterFilter { IsReplayed = false }, CancellationToken.None);
        matches.Select(message => message.MessageId).ShouldBe(["pending"]);
    }

    [Fact]
    public async Task PageNewestFirstUsingStoredTimestamps()
    {
        var store = CreateStore();
        foreach (var sequence in new[] { 3, 1, 4, 2 })
        {
            await store.StoreAsync(Message(sequence.ToString(System.Globalization.CultureInfo.InvariantCulture), sequence), CancellationToken.None);
        }

        var matches = await store.GetMessagesAsync(new DeadLetterFilter { Skip = 1, MaxResults = 2 }, CancellationToken.None);
        matches.Select(message => message.MessageId).ShouldBe(["3", "2"]);
    }

    private static InMemoryDeadLetterStore CreateStore()
    {
        var tenant = A.Fake<ITenantContext>();
        A.CallTo(() => tenant.TenantId).Returns("tenant-a");
        return new InMemoryDeadLetterStore(tenant, NullLogger<InMemoryDeadLetterStore>.Instance);
    }

    private static DeadLetterMessage Message(string id, int sequence, string type = "order", string reason = "delivery timeout", bool replayed = false) => new()
    {
        MessageId = id, MessageType = type, Reason = reason, IsReplayed = replayed,
        MessageBody = "{}", MessageMetadata = "{}",
        MovedToDeadLetterAt = DateTimeOffset.UnixEpoch.AddMinutes(sequence),
    };
}
