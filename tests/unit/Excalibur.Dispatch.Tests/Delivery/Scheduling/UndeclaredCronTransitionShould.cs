// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Scheduling;
using Excalibur.Dispatch.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Dispatch.Tests.Delivery.Scheduling;

// Current-API semantic RED control: no new profile/API types are referenced.
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class UndeclaredCronTransitionShould
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefuseImplicitTransitionPolicyBeforeStorage(bool configuredDefault)
    {
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); }
        catch (TimeZoneNotFoundException) { zone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
        zone.IsInvalidTime(new DateTime(2026, 3, 8, 2, 30, 0, DateTimeKind.Unspecified)).ShouldBeTrue();
        var store = A.Fake<IScheduleStore>();
        using var serializer = new DispatchJsonSerializer();
        var options = Microsoft.Extensions.Options.Options.Create(new SchedulerOptions());
        var cronOptions = Microsoft.Extensions.Options.Options.Create(new CronScheduleOptions { DefaultTimeZone = zone });
        var scheduler = new RecurringDispatchScheduler(store, serializer, options,
            new CronScheduler(cronOptions, NullLogger<CronScheduler>.Instance), cronOptions,
            NullLogger<RecurringDispatchScheduler>.Instance,
            new FakeTimeProvider(new DateTimeOffset(2026, 3, 8, 6, 59, 0, TimeSpan.Zero)));
        var message = new Payload("undeclared");
        if (configuredDefault)
        {
            await Should.ThrowAsync<ArgumentException>(() => scheduler.ScheduleRecurringAsync("30 2 * * *", message, CancellationToken.None));
        }
        else
        {
            await Should.ThrowAsync<ArgumentException>(() => scheduler.ScheduleRecurringAsync("30 2 * * *", zone, message, CancellationToken.None));
        }
        A.CallTo(() => store.StoreAsync(A<IScheduledMessage>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task KeepOrdinaryUtcSchedulingLiveWithoutATransitionDeclaration()
    {
        var store = A.Fake<IScheduleStore>();
        IScheduledMessage? stored = null;
        A.CallTo(() => store.StoreAsync(A<IScheduledMessage>._, A<CancellationToken>._))
            .Invokes((IScheduledMessage row, CancellationToken _) => stored = row).Returns(Task.CompletedTask);
        using var serializer = new DispatchJsonSerializer();
        var options = Microsoft.Extensions.Options.Options.Create(new SchedulerOptions());
        var cronOptions = Microsoft.Extensions.Options.Options.Create(new CronScheduleOptions { DefaultTimeZone = TimeZoneInfo.Utc });
        var scheduler = new RecurringDispatchScheduler(store, serializer, options,
            new CronScheduler(cronOptions, NullLogger<CronScheduler>.Instance), cronOptions,
            NullLogger<RecurringDispatchScheduler>.Instance,
            new FakeTimeProvider(new DateTimeOffset(2026, 3, 8, 6, 59, 0, TimeSpan.Zero)));
        IDispatchScheduler publicScheduler = scheduler;
        await publicScheduler.ScheduleRecurringAsync("* * * * *", new Payload("ordinary"), CancellationToken.None);
        stored.ShouldNotBeNull();
        stored.TimeZoneId.ShouldBe(TimeZoneInfo.Utc.Id);
        stored.NextExecutionUtc.ShouldBe(new DateTimeOffset(2026, 3, 8, 7, 0, 0, TimeSpan.Zero));
        stored.MessageName.ShouldBe(typeof(Payload).FullName);
        stored.MessageBody.ShouldContain("ordinary");
    }

    private sealed record Payload(string Value);
}
