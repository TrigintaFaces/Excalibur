// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Scheduling;
using Excalibur.Dispatch.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Dispatch.Tests.Delivery.Scheduling;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class CronRecurrenceProfileShould
{
    private static readonly DateTimeOffset Now = new(2026, 3, 8, 6, 59, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PersistTheExplicitProfileThroughBothPublicZoneRoutes(bool configuredDefault)
    {
        var zone = InstalledZone();
        var store = new InMemoryScheduleStore(new FakeTimeProvider(Now));
        using var serializer = new DispatchJsonSerializer();
        IDispatchScheduler scheduler = Scheduler(store, serializer, zone);
        if (configuredDefault)
        {
            await scheduler.ScheduleRecurringAsync("0,30 2,3 * * *", CronRecurrenceProfile.CronosCompatible,
                new Payload("public-default"), CancellationToken.None);
        }
        else
        {
            await scheduler.ScheduleRecurringAsync("0,30 2,3 * * *", zone, CronRecurrenceProfile.CronosCompatible,
                new Payload("public-zone"), CancellationToken.None);
        }
        var row = (await store.GetAllAsync(CancellationToken.None)).ShouldHaveSingleItem();
        row.TimeZoneId.ShouldBe(zone.Id);
        row.CronRecurrenceProfileId.ShouldBe("cronos-compatible");
        row.CronRecurrenceProfileVersion.ShouldBe(1);
        row.NextExecutionUtc.ShouldBe(new DateTimeOffset(2026, 3, 8, 7, 0, 0, TimeSpan.Zero));
        var snapshot = (await store.GetExecutionSnapshotsAsync(CancellationToken.None)).ShouldHaveSingleItem();
        snapshot.Message.CronRecurrenceProfileId = "tampered";
        (await store.GetAllAsync(CancellationToken.None)).Single().CronRecurrenceProfileId.ShouldBe("cronos-compatible");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefuseAnUndeclaredTransitionZoneBeforeWriting(bool configuredDefault)
    {
        var zone = InstalledZone();
        var store = new InMemoryScheduleStore();
        using var serializer = new DispatchJsonSerializer();
        var scheduler = Scheduler(store, serializer, zone);
        if (configuredDefault)
        {
            await Should.ThrowAsync<ArgumentException>(() => scheduler.ScheduleRecurringAsync("30 2 * * *",
                new Payload("missing"), CancellationToken.None));
        }
        else
        {
            await Should.ThrowAsync<ArgumentException>(() => scheduler.ScheduleRecurringAsync("30 2 * * *", zone,
                new Payload("missing"), CancellationToken.None));
        }
        (await store.GetAllAsync(CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task RejectSameInstalledIdentifierWithDifferentCustomRules()
    {
        var installed = InstalledZone();
        var custom = TimeZoneInfo.CreateCustomTimeZone(installed.Id, TimeSpan.Zero, "different", "different");
        custom.HasSameRules(installed).ShouldBeFalse();
        var store = new InMemoryScheduleStore();
        using var serializer = new DispatchJsonSerializer();
        IDispatchScheduler scheduler = Scheduler(store, serializer, TimeZoneInfo.Utc);
        await Should.ThrowAsync<ArgumentException>(() => scheduler.ScheduleRecurringAsync("30 2 * * *", custom,
            CronRecurrenceProfile.CronosCompatible, new Payload("different rules"), CancellationToken.None));
        (await store.GetAllAsync(CancellationToken.None)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("future", 1)]
    [InlineData("cronos-compatible", 2)]
    [InlineData("cronos-compatible", null)]
    [InlineData(null, 1)]
    public async Task RejectUnknownOrPartialPairsWithoutReplacingExistingWork(string? id, int? version)
    {
        var store = new InMemoryScheduleStore();
        var row = new ScheduledMessage { Id = Guid.NewGuid(), MessageName = "payload", MessageBody = "original",
            CronExpression = "* * * * *", TimeZoneId = TimeZoneInfo.Utc.Id, NextExecutionUtc = Now };
        await store.StoreAsync(row, CancellationToken.None);
        var generation = (await store.GetExecutionSnapshotsAsync(CancellationToken.None)).Single().Generation;
        row.CronRecurrenceProfileId = id;
        row.CronRecurrenceProfileVersion = version;
        row.MessageBody = "replacement";
        await Should.ThrowAsync<ArgumentException>(() => store.StoreAsync(row, CancellationToken.None));
        var retained = (await store.GetExecutionSnapshotsAsync(CancellationToken.None)).Single();
        retained.Generation.ShouldBe(generation);
        retained.Message.MessageBody.ShouldBe("original");
    }

    [Fact]
    public async Task PreserveTheSupportedUnqualifiedUtcForm()
    {
        var store = new InMemoryScheduleStore();
        using var serializer = new DispatchJsonSerializer();
        IDispatchScheduler scheduler = Scheduler(store, serializer, TimeZoneInfo.Utc);
        await scheduler.ScheduleRecurringAsync("* * * * *", new Payload("UTC"), CancellationToken.None);
        var row = (await store.GetAllAsync(CancellationToken.None)).ShouldHaveSingleItem();
        row.TimeZoneId.ShouldBe(TimeZoneInfo.Utc.Id);
        row.CronRecurrenceProfileId.ShouldBeNull();
        row.CronRecurrenceProfileVersion.ShouldBeNull();
    }

    [Fact]
    public async Task KeepProfileInFailureAndRecoveryEnvelopeCopies()
    {
        var store = new InMemoryScheduleStore(new FakeTimeProvider(Now));
        var row = new ScheduledMessage { Id = Guid.NewGuid(), MessageName = "payload", MessageBody = "original",
            CronExpression = "* * * * *", TimeZoneId = InstalledZone().Id, NextExecutionUtc = Now,
            CronRecurrenceProfileId = "cronos-compatible", CronRecurrenceProfileVersion = 1 };
        await store.StoreAsync(row, CancellationToken.None);
        var generation = (await store.GetExecutionSnapshotsAsync(CancellationToken.None)).Single().Generation;
        var admission = (await store.TryAcquireExecutionAsync(row.Id, generation, Now, 1, CancellationToken.None)).ShouldNotBeNull();
        (await store.TryRecordFailureAsync(admission.Lease,
            new(ScheduleFailureReason.HandlerFailed, ScheduleFailureDisposition.Retrying), CancellationToken.None)).ShouldBeTrue();
        await store.ReleaseExecutionAsync(admission.Lease, CancellationToken.None);
        var exhausted = (await store.TryAcquireExecutionAsync(row.Id, generation, Now, 99, CancellationToken.None)).ShouldNotBeNull();
        (await store.TryRecordFailureAsync(exhausted.Lease,
            new(ScheduleFailureReason.AdmissionBudgetExhausted, ScheduleFailureDisposition.Quarantined), CancellationToken.None)).ShouldBeTrue();
        var failure = (await store.GetOccurrenceFailureAsync(row.Id, generation, Now, null, CancellationToken.None)).ShouldNotBeNull();
        failure.Message.CronRecurrenceProfileId.ShouldBe("cronos-compatible");
        failure.Message.CronRecurrenceProfileVersion.ShouldBe(1);
        failure.Message.CronRecurrenceProfileId = "tampered";
        var receipt = (await store.TryResumeQuarantinedOccurrenceAsync(failure.Token, Guid.NewGuid(), CancellationToken.None)).ShouldNotBeNull();
        receipt.MaxAdmissionAttempts.ShouldBe(1);
        var recovered = (await store.GetAllAsync(CancellationToken.None)).Single();
        recovered.CronRecurrenceProfileId.ShouldBe("cronos-compatible");
        recovered.CronRecurrenceProfileVersion.ShouldBe(1);
    }

    internal static TimeZoneInfo InstalledZone()
    {
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); }
        catch (TimeZoneNotFoundException) { zone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
        zone.IsInvalidTime(new DateTime(2026, 3, 8, 2, 0, 0, DateTimeKind.Unspecified)).ShouldBeTrue();
        zone.IsInvalidTime(new DateTime(2026, 3, 8, 2, 30, 0, DateTimeKind.Unspecified)).ShouldBeTrue();
        zone.IsInvalidTime(new DateTime(2026, 3, 8, 3, 0, 0, DateTimeKind.Unspecified)).ShouldBeFalse();
        zone.GetUtcOffset(new DateTime(2026, 3, 8, 3, 0, 0, DateTimeKind.Unspecified)).ShouldBe(TimeSpan.FromHours(-4));
        foreach (var minute in new[] { 0, 30 })
        {
            var local = new DateTime(2026, 11, 1, 1, minute, 0, DateTimeKind.Unspecified);
            zone.IsAmbiguousTime(local).ShouldBeTrue();
            zone.GetAmbiguousTimeOffsets(local).Order().ShouldBe([TimeSpan.FromHours(-5), TimeSpan.FromHours(-4)]);
        }
        return zone;
    }

    private static RecurringDispatchScheduler Scheduler(IScheduleStore store, DispatchJsonSerializer serializer, TimeZoneInfo zone)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new SchedulerOptions());
        var cronOptions = Microsoft.Extensions.Options.Options.Create(new CronScheduleOptions { DefaultTimeZone = zone });
        return new(store, serializer, options, new CronScheduler(cronOptions, NullLogger<CronScheduler>.Instance),
            cronOptions, NullLogger<RecurringDispatchScheduler>.Instance, new FakeTimeProvider(Now));
    }

    private sealed record Payload(string Value);
}
