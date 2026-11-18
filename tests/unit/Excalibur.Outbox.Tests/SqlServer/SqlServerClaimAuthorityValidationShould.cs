// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Outbox.SqlServer;
using Excalibur.Outbox.SqlServer.Requests;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Excalibur.Outbox.Tests.SqlServer;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
public sealed class SqlServerClaimAuthorityValidationShould
{
    [Fact]
    public void ResolveAllStrictCapabilitiesToTheSameStoreInstance()
    {
        SqlConnection Factory() => throw new InvalidOperationException("capability discovery must not open a connection");
        var store = new SqlServerOutboxStore(Factory, new SqlServerOutboxOptions(), NullLogger<SqlServerOutboxStore>.Instance);
        var resolver = (IServiceProvider)(IOutboxStore)store;
        resolver.GetService(typeof(IClaimScopedOutboxStore)).ShouldBeSameAs(store);
        resolver.GetService(typeof(IClaimScopedDeadLetterableOutboxStore)).ShouldBeSameAs(store);
        resolver.GetService(typeof(IFencedClaimScopedOutboxStore)).ShouldBeSameAs(store);
        resolver.GetService(typeof(IFencedDeadLetterableOutboxStore)).ShouldBeSameAs(store);
    }

    [Fact]
    public async Task RejectDefaultAuthorityBeforeOpeningAConnection()
    {
        var opened = 0;
        SqlConnection Factory() { opened++; throw new InvalidOperationException("invalid authority reached the database factory"); }
        var store = new SqlServerOutboxStore(Factory, new SqlServerOutboxOptions(), NullLogger<SqlServerOutboxStore>.Instance);
        _ = await Should.ThrowAsync<ArgumentException>(async () => await store.MarkSentAsync("id", default(OutboxWriteAuthority), CancellationToken.None));
        _ = await Should.ThrowAsync<ArgumentException>(async () => await store.MarkFailedAsync("id", "error", 1, null, default(OutboxWriteAuthority), CancellationToken.None));
        _ = await Should.ThrowAsync<ArgumentException>(async () => await store.MarkDeadLetteredAsync("id", "reason", 1, default(OutboxWriteAuthority), CancellationToken.None));
        _ = await Should.ThrowAsync<ArgumentException>(async () => await store.MarkSentAsync("id", (string)null!, CancellationToken.None));
        opened.ShouldBe(0);
    }

    [Fact]
    public void RejectAnOversizedAcquisitionRatherThanTruncateItIntoAnAlias()
    {
        _ = Should.Throw<ArgumentException>(() => Request(new string('p', 223)));
        var first = Request(new string('p', 222));
        var second = Request(new string('p', 222));
        var identity = first.Parameters.Get<string>("ProcessorId");
        identity.Length.ShouldBe(255);
        second.Parameters.Get<string>("ProcessorId").ShouldNotBe(identity);
    }

    private static GetUnsentMessagesRequest Request(string processor) => new("[dbo].[OutboxMessages]", 1, 30, 120,
        processor, null, "[dbo].[OutboxFence]", "[dbo].[OutboxMessages]", CancellationToken.None);
}
