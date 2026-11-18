// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;
using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Outbox.Postgres;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Shouldly;
using Tests.Shared.Infrastructure;

namespace Excalibur.Integration.Tests.Data.Outbox;

[Collection(PostgresOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
[Trait("Database", "Postgres")]
public sealed class PostgresClaimCompletionShould(PostgresOutboxStoreContainerFixture fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefuseEveryStaleSameProcessCompletionAndAllowTheSuccessor(bool fenced)
    {
        await PrepareAsync().ConfigureAwait(false);
        await using var connection = await OpenAsync().ConfigureAwait(false);
        using var store = Store(connection);
        var id = await StageAsync(store).ConfigureAwait(false);
        var first = (await ClaimAsync(store,fenced).ConfigureAwait(false)).Single();
        var old = first.DispatcherId.ShouldNotBeNull();
        _ = await connection.ExecuteAsync("UPDATE public.outbox SET dispatcher_timeout=clock_timestamp()-interval '1 second' WHERE message_id=@Id",new { Id=id }).ConfigureAwait(false);
        var current = (await ClaimAsync(store,fenced).ConfigureAwait(false)).Single().DispatcherId.ShouldNotBeNull();
        current.ShouldNotBe(old);
        first.DispatcherId.ShouldBe(old);
        if (fenced)
        {
            var authority = new OutboxWriteAuthority(7,old);
            (await store.MarkSentAsync(id,authority,CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
            (await store.MarkFailedAsync(id,"stale",9,null,authority,CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
            (await store.MarkFailedAsync(id,"stale",9,DateTimeOffset.UtcNow.AddHours(1),authority,CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
            (await store.MarkDeadLetteredAsync(id,"stale",9,authority,CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
        }
        else
        {
            (await store.MarkSentAsync(id,old,CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
            (await store.MarkFailedAsync(id,"stale",9,old,CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
            (await store.MarkFailedWithBackoffAsync(id,"stale",9,DateTimeOffset.UtcNow.AddHours(1),old,CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
            (await store.MarkDeadLetteredAsync(id,"stale",9,old,CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
        }
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM public.outbox WHERE message_id=@Id AND dispatcher_id=@Owner AND attempts=0 AND error_message IS NULL AND next_attempt_at IS NULL",new { Id=id,Owner=current }).ConfigureAwait(false)).ShouldBe(1);
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM public.outbox_dead_letters WHERE message_id=@Id",new { Id=id }).ConfigureAwait(false)).ShouldBe(0);
        var outcome = fenced
            ? await store.MarkSentAsync(id,new OutboxWriteAuthority(7,current),CancellationToken.None).ConfigureAwait(false)
            : await store.MarkSentAsync(id,current,CancellationToken.None).ConfigureAwait(false);
        outcome.ShouldBe(OutboxCompletionOutcome.Applied);
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM public.outbox WHERE message_id=@Id",new { Id=id }).ConfigureAwait(false)).ShouldBe(0);
    }

    [Theory]
    [InlineData(false,false)]
    [InlineData(false,true)]
    [InlineData(true,false)]
    [InlineData(true,true)]
    public async Task PreserveCurrentFailureAttemptSemanticsAndTheServerFloor(bool fenced,bool scheduled)
    {
        await PrepareAsync().ConfigureAwait(false);
        await using var connection = await OpenAsync().ConfigureAwait(false);
        using var store = Store(connection);
        var id = await StageAsync(store).ConfigureAwait(false);
        var owner = (await ClaimAsync(store,fenced).ConfigureAwait(false)).Single().DispatcherId.ShouldNotBeNull();
        _ = await connection.ExecuteAsync("UPDATE public.outbox SET attempts=5 WHERE message_id=@Id",new { Id=id }).ConfigureAwait(false);
        var before = await connection.ExecuteScalarAsync<DateTimeOffset>("SELECT clock_timestamp()").ConfigureAwait(false);
        var next = DateTimeOffset.UtcNow.AddMinutes(10);
        var outcome = fenced
            ? await store.MarkFailedAsync(id,"current",2,scheduled?next:null,new OutboxWriteAuthority(7,owner),CancellationToken.None).ConfigureAwait(false)
            : scheduled
                ? await store.MarkFailedWithBackoffAsync(id,"current",2,next,owner,CancellationToken.None).ConfigureAwait(false)
                : await store.MarkFailedAsync(id,"current",2,owner,CancellationToken.None).ConfigureAwait(false);
        outcome.ShouldBe(OutboxCompletionOutcome.Applied);
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM public.outbox WHERE message_id=@Id AND attempts=@Expected AND error_message='current' AND dispatcher_id IS NULL AND dispatcher_timeout IS NULL AND next_attempt_at>=@Before+interval '30 seconds'",
            new { Id=id,Expected=scheduled?6:5,Before=before }).ConfigureAwait(false)).ShouldBe(1);
        if (scheduled)
        {
            (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM public.outbox WHERE message_id=@Id AND next_attempt_at>=@Before+interval '540 seconds'",new { Id=id,Before=before }).ConfigureAwait(false)).ShouldBe(1);
        }
        (await ClaimAsync(store,fenced).ConfigureAwait(false)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ArchiveCurrentOwnershipWithOriginalBytesTenantAndCreationTime(bool fenced)
    {
        await PrepareAsync().ConfigureAwait(false);
        await using var connection = await OpenAsync().ConfigureAwait(false);
        using var store = Store(connection);
        var id = await StageAsync(store).ConfigureAwait(false);
        var stamp = new DateTimeOffset(2020,1,2,3,4,5,TimeSpan.Zero);
        const string metadata = "{\"nested\":{\"value\":7},\"explicitNull\":null}";
        _ = await connection.ExecuteAsync("UPDATE public.outbox SET occurred_on=@Stamp,tenant_id='tenant-é',message_metadata=@Metadata,attempts=5 WHERE message_id=@Id",new { Id=id,Stamp=stamp,Metadata=metadata }).ConfigureAwait(false);
        var owner = (await ClaimAsync(store,fenced).ConfigureAwait(false)).Single().DispatcherId.ShouldNotBeNull();
        var outcome = fenced
            ? await store.MarkDeadLetteredAsync(id,"poison",2,new OutboxWriteAuthority(7,owner),CancellationToken.None).ConfigureAwait(false)
            : await store.MarkDeadLetteredAsync(id,"poison",2,owner,CancellationToken.None).ConfigureAwait(false);
        outcome.ShouldBe(OutboxCompletionOutcome.Applied);
        var archived = await connection.QuerySingleAsync<ArchiveRow>("SELECT tenant_id AS Tenant,message_type AS Type,message_metadata AS Metadata,message_body AS Body,occurred_on AS Occurred,attempts AS Attempts,error_message AS Error FROM public.outbox_dead_letters WHERE message_id=@Id",new { Id=id }).ConfigureAwait(false);
        archived.Tenant.ShouldBe("tenant-é"); archived.Type.ShouldBe("type"); archived.Metadata.ShouldBe(metadata);
        archived.Body.ShouldBe(new byte[] { 0,255,128,1 }); archived.Occurred.ShouldBe(stamp);
        archived.Attempts.ShouldBe(5); archived.Error.ShouldBe("poison");
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM public.outbox WHERE message_id=@Id",new { Id=id }).ConfigureAwait(false)).ShouldBe(0);
        (await store.MarkSentAsync(id,owner,CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.MessageNotFound);
    }

    [Theory]
    [InlineData(false,"insert")]
    [InlineData(true,"insert")]
    [InlineData(false,"delete")]
    [InlineData(true,"delete")]
    [InlineData(false,"throw-delete")]
    [InlineData(true,"throw-delete")]
    public async Task RollBackTheWholeBurialWhenARequiredMutationIsSuppressedOrThrows(bool fenced,string fault)
    {
        await PrepareAsync().ConfigureAwait(false);
        await using var connection = await OpenAsync().ConfigureAwait(false);
        await using var admin = await OpenAsync().ConfigureAwait(false);
        using var store = Store(connection);
        var id = await StageAsync(store).ConfigureAwait(false);
        var owner = (await ClaimAsync(store,fenced).ConfigureAwait(false)).Single().DispatcherId.ShouldNotBeNull();
        var name = "strict_fault_"+Guid.NewGuid().ToString("N");
        var table = fault=="insert"?"public.outbox_dead_letters":"public.outbox";
        var action = fault=="insert"?"INSERT":"DELETE";
        var body = fault=="throw-delete"?"RAISE EXCEPTION 'strict deletion fault';":"RETURN NULL;";
        _ = await admin.ExecuteAsync($"CREATE FUNCTION public.{name}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN {body} END $$; CREATE TRIGGER {name} BEFORE {action} ON {table} FOR EACH ROW EXECUTE FUNCTION public.{name}()").ConfigureAwait(false);
        try
        {
            async Task RetireAsync()
            {
                if (fenced) { _ = await store.MarkDeadLetteredAsync(id,"fault",9,new OutboxWriteAuthority(8,owner),CancellationToken.None).ConfigureAwait(false); }
                else { _ = await store.MarkDeadLetteredAsync(id,"fault",9,owner,CancellationToken.None).ConfigureAwait(false); }
            }
            var error = await Should.ThrowAsync<OperationFailedException>(RetireAsync);
            error.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(fault=="throw-delete"?PostgresErrorCodes.RaiseException:PostgresErrorCodes.DivisionByZero);
            (await admin.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM public.outbox WHERE message_id=@Id AND dispatcher_id=@Owner AND attempts=0",new { Id=id,Owner=owner }).ConfigureAwait(false)).ShouldBe(1);
            (await admin.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM public.outbox_dead_letters WHERE message_id=@Id",new { Id=id }).ConfigureAwait(false)).ShouldBe(0);
            if (fenced)
            {
                (await admin.ExecuteScalarAsync<long>("SELECT high_water_token FROM public.outbox_fence WHERE scope_key='\"public\".\"outbox\"'").ConfigureAwait(false)).ShouldBe(7,"failed statement must roll back its proposed fence advance too");
            }
        }
        finally { _ = await admin.ExecuteAsync($"DROP TRIGGER {name} ON {table}; DROP FUNCTION public.{name}()").ConfigureAwait(false); }
        (await store.MarkDeadLetteredAsync(id,"restored",9,owner,CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.Applied);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EvaluateLeaseTimeAfterTheRowLockWait(bool fenced)
    {
        await PrepareAsync().ConfigureAwait(false);
        await using var worker = await OpenAsync().ConfigureAwait(false);
        await using var blocker = await OpenAsync().ConfigureAwait(false);
        using var store = Store(worker);
        var id = await StageAsync(store).ConfigureAwait(false);
        var owner = (await ClaimAsync(store,fenced).ConfigureAwait(false)).Single().DispatcherId.ShouldNotBeNull();
        var workerPid = await worker.ExecuteScalarAsync<int>("SELECT pg_backend_pid()").ConfigureAwait(false);
        var blockerPid = await blocker.ExecuteScalarAsync<int>("SELECT pg_backend_pid()").ConfigureAwait(false);
        await using var transaction = await blocker.BeginTransactionAsync().ConfigureAwait(false);
        var expiry = await blocker.ExecuteScalarAsync<DateTimeOffset>("UPDATE public.outbox SET dispatcher_timeout=clock_timestamp()+interval '3 seconds' WHERE message_id=@Id RETURNING dispatcher_timeout",new { Id=id },transaction).ConfigureAwait(false);
        var completion = fenced
            ? store.MarkSentAsync(id,new OutboxWriteAuthority(7,owner),CancellationToken.None).AsTask()
            : store.MarkSentAsync(id,owner,CancellationToken.None).AsTask();
        var released = false;
        try
        {
            (await WaitForBlockAsync(workerPid,blockerPid).ConfigureAwait(false)).ShouldBeTrue();
            (await WaitHelpers.WaitUntilAsync(async () =>
            {
                await using var observer = await OpenAsync().ConfigureAwait(false);
                return await observer.ExecuteScalarAsync<bool>("SELECT clock_timestamp()>@Expiry",new { Expiry=expiry }).ConfigureAwait(false);
            },TimeSpan.FromSeconds(15),TimeSpan.FromMilliseconds(100),CancellationToken.None).ConfigureAwait(false)).ShouldBeTrue();
            await transaction.CommitAsync().ConfigureAwait(false); released=true;
            (await completion.ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.ClaimLost,"clock projected before LockRows waits can falsely admit the expired lease");
        }
        finally
        {
            if (!released) { await transaction.RollbackAsync().ConfigureAwait(false); }
            try { _ = await completion.ConfigureAwait(false); } catch { }
        }
        (await worker.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM public.outbox WHERE message_id=@Id AND dispatcher_id=@Owner",new { Id=id,Owner=owner }).ConfigureAwait(false)).ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReportClaimLostForTheNewTupleAfterConcurrentOwnerReplacement(bool fenced)
    {
        await PrepareAsync().ConfigureAwait(false);
        await using var worker = await OpenAsync().ConfigureAwait(false);
        await using var blocker = await OpenAsync().ConfigureAwait(false);
        using var store = Store(worker);
        var id = await StageAsync(store).ConfigureAwait(false);
        var owner = (await ClaimAsync(store,fenced).ConfigureAwait(false)).Single().DispatcherId.ShouldNotBeNull();
        var workerPid = await worker.ExecuteScalarAsync<int>("SELECT pg_backend_pid()").ConfigureAwait(false);
        var blockerPid = await blocker.ExecuteScalarAsync<int>("SELECT pg_backend_pid()").ConfigureAwait(false);
        await using var transaction = await blocker.BeginTransactionAsync().ConfigureAwait(false);
        _ = await blocker.ExecuteAsync("UPDATE public.outbox SET dispatcher_id='successor',dispatcher_timeout=clock_timestamp()+interval '5 minutes' WHERE message_id=@Id",new { Id=id },transaction).ConfigureAwait(false);
        var completion = fenced
            ? store.MarkSentAsync(id,new OutboxWriteAuthority(7,owner),CancellationToken.None).AsTask()
            : store.MarkSentAsync(id,owner,CancellationToken.None).AsTask();
        var released=false;
        try
        {
            (await WaitForBlockAsync(workerPid,blockerPid).ConfigureAwait(false)).ShouldBeTrue();
            await transaction.CommitAsync().ConfigureAwait(false); released=true;
            (await completion.ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.ClaimLost,"owner predicates below LockRows can discard the successor tuple and falsely report missing");
        }
        finally
        {
            if (!released) { await transaction.RollbackAsync().ConfigureAwait(false); }
            try { _ = await completion.ConfigureAwait(false); } catch { }
        }
        (await store.MarkSentAsync(id,"successor",CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.Applied);
    }

    [Theory]
    [InlineData("sent")]
    [InlineData("failed")]
    [InlineData("deadletter")]
    public async Task LeaveTheBorrowedTransactionAndItsCommitDecisionToTheCaller(string operation)
    {
        await PrepareAsync().ConfigureAwait(false);
        await using var connection = await OpenAsync().ConfigureAwait(false);
        await using var observer = await OpenAsync().ConfigureAwait(false);
        using var store = Store(connection);
        var id = await StageAsync(store).ConfigureAwait(false);
        var owner = (await ClaimAsync(store,true).ConfigureAwait(false)).Single().DispatcherId.ShouldNotBeNull();
        await using var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false);
        var authority = new OutboxWriteAuthority(8,owner);
        var result = operation switch
        {
            "sent" => await store.MarkSentAsync(id,authority,CancellationToken.None).ConfigureAwait(false),
            "failed" => await store.MarkFailedAsync(id,"current",2,null,authority,CancellationToken.None).ConfigureAwait(false),
            _ => await store.MarkDeadLetteredAsync(id,"current",2,authority,CancellationToken.None).ConfigureAwait(false),
        };
        result.ShouldBe(OutboxCompletionOutcome.Applied);
        connection.State.ShouldBe(System.Data.ConnectionState.Open);
        (await observer.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM public.outbox WHERE message_id=@Id AND dispatcher_id=@Owner AND attempts=0",new { Id=id,Owner=owner }).ConfigureAwait(false)).ShouldBe(1);
        (await observer.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM public.outbox_dead_letters WHERE message_id=@Id",new { Id=id }).ConfigureAwait(false)).ShouldBe(0);
        (await observer.ExecuteScalarAsync<long>("SELECT high_water_token FROM public.outbox_fence WHERE scope_key='\"public\".\"outbox\"'").ConfigureAwait(false)).ShouldBe(7);
        (await connection.ExecuteScalarAsync<long>("SELECT high_water_token FROM public.outbox_fence WHERE scope_key='\"public\".\"outbox\"'",transaction:transaction).ConfigureAwait(false)).ShouldBe(8);
        await transaction.RollbackAsync().ConfigureAwait(false);
        (await observer.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM public.outbox WHERE message_id=@Id AND dispatcher_id=@Owner AND attempts=0",new { Id=id,Owner=owner }).ConfigureAwait(false)).ShouldBe(1);
        (await observer.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM public.outbox_dead_letters WHERE message_id=@Id",new { Id=id }).ConfigureAwait(false)).ShouldBe(0);
        (await observer.ExecuteScalarAsync<long>("SELECT high_water_token FROM public.outbox_fence WHERE scope_key='\"public\".\"outbox\"'").ConfigureAwait(false)).ShouldBe(7);
    }

    [Fact]
    public async Task HoldScopeAuthorityWhileTheActualStoreWaitsForItsRow()
    {
        await PrepareAsync().ConfigureAwait(false);
        await using var worker=await OpenAsync().ConfigureAwait(false);
        await using var successor=await OpenAsync().ConfigureAwait(false);
        await using var blocker=await OpenAsync().ConfigureAwait(false);
        using var oldStore=Store(worker);
        using var newStore=Store(successor);
        var id=await StageAsync(oldStore).ConfigureAwait(false);
        var owner=(await ClaimAsync(oldStore,true).ConfigureAwait(false)).Single().DispatcherId.ShouldNotBeNull();
        var oldPid=await worker.ExecuteScalarAsync<int>("SELECT pg_backend_pid()").ConfigureAwait(false);
        var newPid=await successor.ExecuteScalarAsync<int>("SELECT pg_backend_pid()").ConfigureAwait(false);
        var blockerPid=await blocker.ExecuteScalarAsync<int>("SELECT pg_backend_pid()").ConfigureAwait(false);
        await using var barrier=await blocker.BeginTransactionAsync().ConfigureAwait(false);
        _ = await blocker.ExecuteScalarAsync<string>("SELECT message_id FROM public.outbox WHERE message_id=@Id FOR UPDATE",new { Id=id },barrier).ConfigureAwait(false);
        var old=oldStore.MarkSentAsync(id,new OutboxWriteAuthority(7,owner),CancellationToken.None).AsTask();
        Task<OutboxCompletionOutcome>? next=null;
        var released=false;
        try
        {
            (await WaitForBlockAsync(oldPid,blockerPid).ConfigureAwait(false)).ShouldBeTrue();
            next=newStore.MarkSentAsync("missing",new OutboxWriteAuthority(8,owner),CancellationToken.None).AsTask();
            (await WaitForBlockAsync(newPid,oldPid).ConfigureAwait(false)).ShouldBeTrue("successor authority must wait behind the old statement's fence lock");
            await barrier.CommitAsync().ConfigureAwait(false); released=true;
            (await old.ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.Applied);
            (await next.ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.MessageNotFound);
            (await successor.ExecuteScalarAsync<long>("SELECT high_water_token FROM public.outbox_fence WHERE scope_key='\"public\".\"outbox\"'").ConfigureAwait(false)).ShouldBe(8);
            (await oldStore.MarkSentAsync("missing",new OutboxWriteAuthority(7,owner),CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.FenceRefused);
        }
        finally
        {
            if (!released) { await barrier.RollbackAsync().ConfigureAwait(false); }
            try { _ = await old.ConfigureAwait(false); } catch { }
            if (next is not null) { try { _ = await next.ConfigureAwait(false); } catch { } }
        }
    }

    [Theory]
    [InlineData("OWNER:é:😀:claim")]
    [InlineData("owner:e:😀:claim")]
    [InlineData("owner:e\u0301:😀:claim")]
    [InlineData("owner:é:😀:claim ")]
    public async Task CompareBytesEvenWithANondeterministicIcuIdentityColumn(string wrongIdentity)
    {
        await PrepareAsync().ConfigureAwait(false);
        await using var connection=await OpenAsync().ConfigureAwait(false);
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM pg_collation WHERE collprovider='i'").ConfigureAwait(false)).ShouldBeGreaterThan(0,
            "this control needs an ICU-capable PostgreSQL16 fixture; absence is unverified infrastructure, never a skipped success");
        var suffix=Guid.NewGuid().ToString("N");
        var table="ordinal_claim_"+suffix;
        var collation="ordinal_ci_"+suffix;
        _ = await connection.ExecuteAsync($"CREATE COLLATION public.{collation} (provider=icu,locale='und-u-ks-level1',deterministic=false); CREATE TABLE public.{table} (LIKE public.outbox INCLUDING ALL); ALTER TABLE public.{table} ALTER COLUMN dispatcher_id TYPE varchar(100) COLLATE public.{collation}").ConfigureAwait(false);
        try
        {
            using var store=Store(connection,table);
            var id=await StageAsync(store).ConfigureAwait(false);
            _ = await connection.ExecuteAsync($"UPDATE public.{table} SET dispatcher_id=@Owner,dispatcher_timeout=clock_timestamp()+interval '5 minutes' WHERE message_id=@Id",new { Id=id,Owner="owner:é:😀:claim" }).ConfigureAwait(false);
            (await store.MarkSentAsync(id,wrongIdentity,CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
            (await store.MarkSentAsync(id,"owner:é:😀:claim",CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.Applied);
        }
        finally { _ = await connection.ExecuteAsync($"DROP TABLE public.{table}; DROP COLLATION public.{collation}").ConfigureAwait(false); }
    }

    [Fact]
    public async Task RefuseInvalidHostTextInsteadOfReplacementEncodingAnotherAcquisition()
    {
        await PrepareAsync().ConfigureAwait(false);
        await using var connection=await OpenAsync().ConfigureAwait(false);
        await using var observer=await OpenAsync().ConfigureAwait(false);
        using var store=Store(connection);
        var id=await StageAsync(store).ConfigureAwait(false);
        const string stored="owner:\uFFFD:claim";
        _ = await connection.ExecuteAsync("UPDATE public.outbox SET dispatcher_id=@Owner,dispatcher_timeout=clock_timestamp()+interval '5 minutes' WHERE message_id=@Id",new { Id=id,Owner=stored }).ConfigureAwait(false);
        var invalid="owner:"+new string((char)0xD800,1)+":claim";
        var error=await Should.ThrowAsync<OperationFailedException>(async () =>
            await store.MarkSentAsync(id,invalid,CancellationToken.None).ConfigureAwait(false));
        Exception? cause=error;
        while (cause is not null && cause is not System.Text.EncoderFallbackException) { cause=cause.InnerException; }
        cause.ShouldBeOfType<System.Text.EncoderFallbackException>();
        (await observer.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM public.outbox WHERE message_id=@Id AND dispatcher_id=@Owner",new { Id=id,Owner=stored }).ConfigureAwait(false)).ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailWhenTheScopeWriteReturnsNoAuthorityInsteadOfInventingAMissingRow(bool existingScope)
    {
        await PrepareAsync().ConfigureAwait(false);
        await using var connection=await OpenAsync().ConfigureAwait(false);
        await using var admin=await OpenAsync().ConfigureAwait(false);
        using var store=Store(connection);
        var id=await StageAsync(store).ConfigureAwait(false);
        var owner=(await ClaimAsync(store,existingScope).ConfigureAwait(false)).Single().DispatcherId.ShouldNotBeNull();
        var name="scope_suppression_"+Guid.NewGuid().ToString("N");
        var action=existingScope?"UPDATE":"INSERT";
        _ = await admin.ExecuteAsync($"CREATE FUNCTION public.{name}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER {name} BEFORE {action} ON public.outbox_fence FOR EACH ROW EXECUTE FUNCTION public.{name}()").ConfigureAwait(false);
        try
        {
            var error=await Should.ThrowAsync<OperationFailedException>(async () =>
                await store.MarkSentAsync(id,new OutboxWriteAuthority(8,owner),CancellationToken.None).ConfigureAwait(false));
            error.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.DivisionByZero,
                "empty authority RETURNING is an invariant error, never missing-row or fence-refusal evidence");
            (await admin.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM public.outbox WHERE message_id=@Id AND dispatcher_id=@Owner AND attempts=0 AND error_message IS NULL",new { Id=id,Owner=owner }).ConfigureAwait(false)).ShouldBe(1);
            (await admin.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM public.outbox_dead_letters WHERE message_id=@Id",new { Id=id }).ConfigureAwait(false)).ShouldBe(0);
            (await admin.ExecuteScalarAsync<long>("SELECT COALESCE(MAX(high_water_token),0) FROM public.outbox_fence WHERE scope_key='\"public\".\"outbox\"'").ConfigureAwait(false)).ShouldBe(existingScope?7L:0L);
        }
        finally { _ = await admin.ExecuteAsync($"DROP TRIGGER {name} ON public.outbox_fence; DROP FUNCTION public.{name}()").ConfigureAwait(false); }
        (await store.MarkSentAsync(id,new OutboxWriteAuthority(8,owner),CancellationToken.None).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.Applied);
        (await admin.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM public.outbox WHERE message_id=@Id",new { Id=id }).ConfigureAwait(false)).ShouldBe(0);
        (await admin.ExecuteScalarAsync<long>("SELECT high_water_token FROM public.outbox_fence WHERE scope_key='\"public\".\"outbox\"'").ConfigureAwait(false)).ShouldBe(8);
    }

    private PostgresOutboxStore Store(NpgsqlConnection connection,string table="outbox")
    {
        var db=A.Fake<IDb>();
        _ = A.CallTo(() => db.Connection).Returns(connection);
        return new PostgresOutboxStore(db,Options.Create(new PostgresOutboxStoreOptions { OutboxTableName=table,ReservationTimeout=300,FailureBackoffFloorSeconds=30 }),NullLogger<PostgresOutboxStore>.Instance);
    }

    private static async Task<string> StageAsync(PostgresOutboxStore store)
    {
        var message=new OutboundMessage("type",new byte[] { 0,255,128,1 },"destination") { Id=Guid.NewGuid().ToString("N") };
        await store.StageMessageAsync(message,CancellationToken.None).ConfigureAwait(false);
        return message.Id;
    }

    private static ValueTask<IEnumerable<OutboundMessage>> ClaimAsync(PostgresOutboxStore store,bool fenced) => fenced
        ? store.GetUnsentMessagesAsync(10,7,CancellationToken.None)
        : store.GetUnsentMessagesAsync(10,CancellationToken.None);

    private async Task PrepareAsync()
    {
        fixture.DockerAvailable.ShouldBeTrue("real provider controls cannot become skipped passes");
        await fixture.EnsureInitializedAsync().ConfigureAwait(false);
        await fixture.CleanupTableAsync().ConfigureAwait(false);
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection=fixture.CreateConnection();
        try { await connection.OpenAsync().ConfigureAwait(false); return connection; }
        catch { await connection.DisposeAsync().ConfigureAwait(false); throw; }
    }

    private Task<bool> WaitForBlockAsync(int waiter,int owner) => WaitHelpers.WaitUntilAsync(async () =>
    {
        await using var observer=await OpenAsync().ConfigureAwait(false);
        return await observer.ExecuteScalarAsync<bool>("SELECT @Owner=ANY(pg_blocking_pids(@Waiter))",new { Owner=owner,Waiter=waiter }).ConfigureAwait(false);
    },TimeSpan.FromSeconds(20),TimeSpan.FromMilliseconds(100),CancellationToken.None);

    private sealed class ArchiveRow
    {
        public string Tenant { get; set; }="";
        public string Type { get; set; }="";
        public string Metadata { get; set; }="";
        public byte[] Body { get; set; }=[];
        public DateTimeOffset Occurred { get; set; }
        public int Attempts { get; set; }
        public string Error { get; set; }="";
    }
}
