// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Pattern", "Concurrency")]
public sealed class InMemoryScheduleExecutionStoreShould
{
	[Fact]
	public async Task KeepStoredStateDetachedFromCallerInput()
	{
		var store = new InMemoryScheduleStore();
		var due = DateTimeOffset.UtcNow;
		var input = new ScheduledMessage { MessageName = "original", MessageBody = "original-body", Enabled = true, NextExecutionUtc = due };
		await store.StoreAsync(input, CancellationToken.None);
		input.MessageName = "modified";
		input.MessageBody = "modified-body";
		input.Enabled = false;
		input.NextExecutionUtc = due.AddDays(1);
		var authoritative = (await store.GetAllAsync(CancellationToken.None)).Single();
		authoritative.MessageName.ShouldBe("original");
		authoritative.MessageBody.ShouldBe("original-body");
		authoritative.Enabled.ShouldBeTrue();
		authoritative.NextExecutionUtc.ShouldBe(due);
	}

	[Fact]
	public async Task KeepStoredStateDetachedFromReturnedInventory()
	{
		var store = new InMemoryScheduleStore();
		var due = DateTimeOffset.UtcNow;
		await store.StoreAsync(new ScheduledMessage { MessageName = "original", MessageBody = "original-body", Enabled = true, NextExecutionUtc = due }, CancellationToken.None);
		var returned = (await store.GetAllAsync(CancellationToken.None)).Single();
		returned.MessageName = "modified";
		returned.MessageBody = "modified-body";
		returned.Enabled = false;
		returned.NextExecutionUtc = due.AddDays(1);
		var authoritative = (await store.GetAllAsync(CancellationToken.None)).Single();
		authoritative.MessageName.ShouldBe("original");
		authoritative.MessageBody.ShouldBe("original-body");
		authoritative.Enabled.ShouldBeTrue();
		authoritative.NextExecutionUtc.ShouldBe(due);
	}

	private static ScheduledMessage Message(DateTimeOffset due) => new()
	{
		MessageName = "original", MessageBody = "body", Enabled = true, NextExecutionUtc = due,
		CronExpression = "* * * * *", TimeZoneId = "UTC", Interval = TimeSpan.FromMinutes(1),
		CorrelationId = "correlation", TenantId = "tenant", TraceParent = "trace", UserId = "user",
		LastExecutionUtc = due.AddMinutes(-1), MissedExecutionBehavior = MissedExecutionBehavior.ExecuteLatestMissed,
	};

	[Fact]
	public async Task AdmitExactlyOneSimultaneousWorkerForOneOccurrence()
	{
		var store = new InMemoryScheduleStore();
		var due = DateTimeOffset.UtcNow;
		await store.StoreAsync(Message(due), CancellationToken.None);
		var snapshot = (await store.GetExecutionSnapshotsAsync(CancellationToken.None)).Single();
		using var start = new ManualResetEventSlim(false);
		var contenders = Enumerable.Range(0, 16).Select(_ => Task.Run(async () =>
		{
			start.Wait();
			return await store.TryAcquireExecutionAsync(snapshot.Message.Id, snapshot.Generation, due, CancellationToken.None);
		})).ToArray();
		start.Set();
		var outcomes = await Task.WhenAll(contenders);
		outcomes.Count(lease => lease is not null).ShouldBe(1);
	}

	[Fact]
	public async Task RefuseAdmissionAfterCompletionAndOldProgressAfterAdmissionThenCompletion()
	{
		var store = new InMemoryScheduleStore();
		var due = DateTimeOffset.UtcNow;
		var message = Message(due);
		await store.StoreAsync(message, CancellationToken.None);
		var before = (await store.GetExecutionSnapshotsAsync(CancellationToken.None)).Single();
		await store.CompleteAsync(message.Id, CancellationToken.None);
		(await store.TryAcquireExecutionAsync(message.Id, before.Generation, due, CancellationToken.None)).ShouldBeNull();
		await store.StoreAsync(message, CancellationToken.None);
		var current = (await store.GetExecutionSnapshotsAsync(CancellationToken.None)).Single();
		var lease = (await store.TryAcquireExecutionAsync(message.Id, current.Generation, due, CancellationToken.None)).ShouldNotBeNull();
		await store.CompleteAsync(message.Id, CancellationToken.None);
		(await store.TryUpdateProgressAsync(lease, new(due, due.AddMinutes(1)), CancellationToken.None)).ShouldBeFalse();
		(await store.ReleaseExecutionAsync(lease, CancellationToken.None)).ShouldBeFalse();
		(await store.GetAllAsync(CancellationToken.None)).Single().Enabled.ShouldBeFalse();
	}

	[Fact]
	public async Task FenceSameDueReplacementAndAbaReactivationWithFreshGenerations()
	{
		var store = new InMemoryScheduleStore();
		var due = DateTimeOffset.UtcNow;
		var message = Message(due);
		await store.StoreAsync(message, CancellationToken.None);
		var first = (await store.GetExecutionSnapshotsAsync(CancellationToken.None)).Single();
		var old = (await store.TryAcquireExecutionAsync(message.Id, first.Generation, due, CancellationToken.None)).ShouldNotBeNull();
		message.MessageBody = "replacement";
		await store.StoreAsync(message, CancellationToken.None);
		var second = (await store.GetExecutionSnapshotsAsync(CancellationToken.None)).Single();
		second.Generation.ShouldNotBe(first.Generation);
		(await store.TryAcquireExecutionAsync(message.Id, first.Generation, due, CancellationToken.None)).ShouldBeNull();
		(await store.TryUpdateProgressAsync(old, new(due, null, true), CancellationToken.None)).ShouldBeFalse();
		var owner = (await store.TryAcquireExecutionAsync(message.Id, second.Generation, due, CancellationToken.None)).ShouldNotBeNull();
		(await store.ReleaseExecutionAsync(old, CancellationToken.None)).ShouldBeFalse();
		(await store.TryAcquireExecutionAsync(message.Id, second.Generation, due, CancellationToken.None)).ShouldBeNull();
		(await store.ReleaseExecutionAsync(owner, CancellationToken.None)).ShouldBeTrue();
		await store.CompleteAsync(message.Id, CancellationToken.None);
		await store.StoreAsync(message, CancellationToken.None);
		var third = (await store.GetExecutionSnapshotsAsync(CancellationToken.None)).Single();
		third.Generation.ShouldNotBe(first.Generation);
		third.Generation.ShouldNotBe(second.Generation);
		third.Message.Enabled.ShouldBeTrue();
		(await store.TryAcquireExecutionAsync(message.Id, third.Generation, due, CancellationToken.None)).ShouldNotBeNull();
	}

	[Fact]
	public async Task CommitProgressOnlyOnceAndReleaseUsingOriginalOccurrenceAfterAdvancingDue()
	{
		// A controllable clock, because this arm acquires a SECOND occurrence one minute after the first.
		// Admission refuses an occurrence that is not yet due by the store's own clock, so the advance has
		// to actually happen rather than be assumed -- on the system clock that second acquire is a minute
		// in the future and is correctly refused.
		var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(
			new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
		var store = new InMemoryScheduleStore(clock);
		var due = clock.GetUtcNow();
		var message = Message(due);
		await store.StoreAsync(message, CancellationToken.None);
		var snapshot = (await store.GetExecutionSnapshotsAsync(CancellationToken.None)).Single();
		var lease = (await store.TryAcquireExecutionAsync(message.Id, snapshot.Generation, due, CancellationToken.None)).ShouldNotBeNull();
		(await store.TryUpdateProgressAsync(lease, new(due, due.AddMinutes(1)), CancellationToken.None)).ShouldBeTrue();
		(await store.TryUpdateProgressAsync(lease, new(due.AddMinutes(-1), due), CancellationToken.None)).ShouldBeFalse();
		var stored = (await store.GetAllAsync(CancellationToken.None)).Single();
		stored.NextExecutionUtc.ShouldBe(due.AddMinutes(1));
		stored.MessageBody.ShouldBe("body");
		stored.Enabled.ShouldBeTrue();
		(await store.ReleaseExecutionAsync(lease, CancellationToken.None)).ShouldBeTrue();
		(await store.ReleaseExecutionAsync(lease, CancellationToken.None)).ShouldBeFalse();

		// Time actually passes before the advanced occurrence is acquired, which is what makes it due.
		clock.Advance(TimeSpan.FromMinutes(1));

		var newer = (await store.TryAcquireExecutionAsync(message.Id, snapshot.Generation, due.AddMinutes(1), CancellationToken.None)).ShouldNotBeNull();
		(await store.ReleaseExecutionAsync(lease, CancellationToken.None)).ShouldBeFalse();
		(await store.ReleaseExecutionAsync(newer, CancellationToken.None)).ShouldBeTrue();
	}

	[Fact]
	public async Task ValidateEntireBearerTupleIncludingLogicalStore()
	{
		var store = new InMemoryScheduleStore();
		var other = new InMemoryScheduleStore();
		var due = DateTimeOffset.UtcNow;
		var message = Message(due);
		await store.StoreAsync(message, CancellationToken.None);
		var snapshot = (await store.GetExecutionSnapshotsAsync(CancellationToken.None)).Single();
		var lease = (await store.TryAcquireExecutionAsync(message.Id, snapshot.Generation, due, CancellationToken.None)).ShouldNotBeNull();
		var wrongOccurrence = new ScheduleExecutionLease(lease.StoreId, lease.ScheduleId, lease.Generation, due.AddTicks(1), lease.OwnerToken);
		(await store.TryUpdateProgressAsync(wrongOccurrence, new(due, null), CancellationToken.None)).ShouldBeFalse();
		(await store.ReleaseExecutionAsync(wrongOccurrence, CancellationToken.None)).ShouldBeFalse();
		(await other.ReleaseExecutionAsync(lease, CancellationToken.None)).ShouldBeFalse();
		var copiedBearer = new ScheduleExecutionLease(lease.StoreId, lease.ScheduleId, lease.Generation, lease.OccurrenceUtc, lease.OwnerToken);
		(await store.ReleaseExecutionAsync(copiedBearer, CancellationToken.None)).ShouldBeTrue();
	}

	[Fact]
	public async Task HonorCancellationBeforeEveryMutationAndKeepOwnershipRetryable()
	{
		var store = new InMemoryScheduleStore();
		var due = DateTimeOffset.UtcNow;
		var message = Message(due);
		await store.StoreAsync(message, CancellationToken.None);
		var snapshot = (await store.GetExecutionSnapshotsAsync(CancellationToken.None)).Single();
		using var source = new CancellationTokenSource();
		await source.CancelAsync();
		await Should.ThrowAsync<OperationCanceledException>(() => store.StoreAsync(Message(due), source.Token));
		await Should.ThrowAsync<OperationCanceledException>(() => store.CompleteAsync(message.Id, source.Token));
		await Should.ThrowAsync<OperationCanceledException>(() => store.TryAcquireExecutionAsync(message.Id, snapshot.Generation, due, source.Token));
		var lease = (await store.TryAcquireExecutionAsync(message.Id, snapshot.Generation, due, CancellationToken.None)).ShouldNotBeNull();
		await Should.ThrowAsync<OperationCanceledException>(() => store.TryUpdateProgressAsync(lease, new(due, null, true), source.Token));
		await Should.ThrowAsync<OperationCanceledException>(() => store.ReleaseExecutionAsync(lease, source.Token));
		(await store.TryUpdateProgressAsync(lease, new(due, null, true), CancellationToken.None)).ShouldBeTrue();
		(await store.ReleaseExecutionAsync(lease, CancellationToken.None)).ShouldBeTrue();
		(await store.GetAllAsync(CancellationToken.None)).Single().Enabled.ShouldBeFalse();
	}

	[Fact]
	public async Task CopyEveryMessageFieldAndKeepExecutionSnapshotsDetached()
	{
		var store = new InMemoryScheduleStore();
		var due = DateTimeOffset.UtcNow;
		var original = Message(due);
		await store.StoreAsync(original, CancellationToken.None);
		var snapshot = (await store.GetExecutionSnapshotsAsync(CancellationToken.None)).Single();
		var copy = snapshot.Message;
		copy.Id.ShouldBe(original.Id);
		copy.MessageName.ShouldBe(original.MessageName);
		copy.MessageBody.ShouldBe(original.MessageBody);
		copy.CorrelationId.ShouldBe(original.CorrelationId);
		copy.CronExpression.ShouldBe(original.CronExpression);
		copy.TimeZoneId.ShouldBe(original.TimeZoneId);
		copy.Interval.ShouldBe(original.Interval);
		copy.NextExecutionUtc.ShouldBe(original.NextExecutionUtc);
		copy.Enabled.ShouldBe(original.Enabled);
		copy.LastExecutionUtc.ShouldBe(original.LastExecutionUtc);
		copy.MissedExecutionBehavior.ShouldBe(original.MissedExecutionBehavior);
		copy.TenantId.ShouldBe(original.TenantId);
		copy.TraceParent.ShouldBe(original.TraceParent);
		copy.UserId.ShouldBe(original.UserId);
		copy.Id = Guid.NewGuid();
		copy.MessageBody = "mutated";
		copy.Enabled = false;
		var authoritative = (await store.GetExecutionSnapshotsAsync(CancellationToken.None)).Single();
		authoritative.Generation.ShouldBe(snapshot.Generation);
		authoritative.Message.Id.ShouldBe(original.Id);
		authoritative.Message.MessageBody.ShouldBe(original.MessageBody);
		authoritative.Message.Enabled.ShouldBeTrue();
	}
}
