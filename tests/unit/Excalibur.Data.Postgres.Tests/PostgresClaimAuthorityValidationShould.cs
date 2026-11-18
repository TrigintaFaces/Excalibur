// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Outbox.Postgres;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Excalibur.Data.Tests.Postgres;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
public sealed class PostgresClaimAuthorityValidationShould
{
    [Fact]
    public async Task RefuseDefaultOrAbsentAcquisitionBeforeBorrowingTheConnection()
    {
        var db=A.Fake<IDb>();
        _ = A.CallTo(() => db.Connection).Throws(new InvalidOperationException("invalid authority reached the connection"));
        using var store=new PostgresOutboxStore(db,Options.Create(new PostgresOutboxStoreOptions()),NullLogger<PostgresOutboxStore>.Instance);
        _ = await Should.ThrowAsync<ArgumentException>(async () => await store.MarkSentAsync("id",default(OutboxWriteAuthority),CancellationToken.None));
        _ = await Should.ThrowAsync<ArgumentException>(async () => await store.MarkDeadLetteredAsync("id","reason",1,default(OutboxWriteAuthority),CancellationToken.None));
        _ = await Should.ThrowAsync<ArgumentException>(async () => await store.MarkFailedAsync("id","error",1,null,default(OutboxWriteAuthority),CancellationToken.None));
        _ = await Should.ThrowAsync<ArgumentException>(async () => await store.MarkSentAsync("id",(string)null!,CancellationToken.None));
        A.CallTo(() => db.Connection).MustNotHaveHappened();
    }

    [Fact]
    public void ResolveAllFourStrictCapabilitiesToTheSameLeaf()
    {
        var db=A.Fake<IDb>();
        using var store=new PostgresOutboxStore(db,Options.Create(new PostgresOutboxStoreOptions()),NullLogger<PostgresOutboxStore>.Instance);
        var resolver=(IServiceProvider)(IOutboxStore)store;
        resolver.GetService(typeof(IClaimScopedOutboxStore)).ShouldBeSameAs(store);
        resolver.GetService(typeof(IClaimScopedDeadLetterableOutboxStore)).ShouldBeSameAs(store);
        resolver.GetService(typeof(IFencedClaimScopedOutboxStore)).ShouldBeSameAs(store);
        resolver.GetService(typeof(IFencedDeadLetterableOutboxStore)).ShouldBeSameAs(store);
        A.CallTo(() => db.Connection).MustNotHaveHappened();
    }
}
