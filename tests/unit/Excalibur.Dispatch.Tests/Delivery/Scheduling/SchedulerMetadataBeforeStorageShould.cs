// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Scheduling;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Tests.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Delivery.Scheduling;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class SchedulerMetadataBeforeStorageShould
{
	[Theory]
	[InlineData("once")]
	[InlineData("interval")]
	[InlineData("cron")]
	public async Task RefuseMissingNonNullMetadataBeforeStoringASchedule(string entryPoint)
	{
		var store = A.Fake<IScheduleStore>();
		using var serializer = new DispatchJsonSerializer(options => options.TypeInfoResolver = MetadataTestJsonContext.Default);
		var scheduler = CreateScheduler(store, serializer);
		var error = await Should.ThrowAsync<NotSupportedException>(async () =>
		{
			switch (entryPoint)
			{
				case "once": await scheduler.ScheduleOnceAsync(DateTimeOffset.UtcNow.AddMinutes(1), new MissingMetadataPayload("missing"), CancellationToken.None); break;
				case "interval": await scheduler.ScheduleRecurringAsync(TimeSpan.FromMinutes(1), new MissingMetadataPayload("missing"), CancellationToken.None); break;
				default: await scheduler.ScheduleRecurringAsync("* * * * *", new MissingMetadataPayload("missing"), CancellationToken.None); break;
			}
		});
		error.Message.ShouldContain(nameof(MissingMetadataPayload));
		A.CallTo(() => store.StoreAsync(A<IScheduledMessage>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task PersistTheDeclaredTypeAndBodyTogether()
	{
		var store = A.Fake<IScheduleStore>();
		using var serializer = new DispatchJsonSerializer(options => options.TypeInfoResolver = MetadataTestJsonContext.Default);
		var scheduler = CreateScheduler(store, serializer);
		MetadataBasePayload payload = new MetadataDerivedPayload { BaseValue = "base", DerivedValue = "derived" };
		IScheduledMessage? stored = null;
		A.CallTo(() => store.StoreAsync(A<IScheduledMessage>._, A<CancellationToken>._))
			.Invokes((IScheduledMessage message, CancellationToken _) => stored = message).Returns(Task.CompletedTask);
		await scheduler.ScheduleOnceAsync(DateTimeOffset.UtcNow.AddMinutes(1), payload, CancellationToken.None);
		stored.ShouldNotBeNull();
		stored.MessageName.ShouldBe(typeof(MetadataBasePayload).FullName);
		stored.MessageBody.ShouldBe("{\"baseValue\":\"base\"}");
	}

	private static RecurringDispatchScheduler CreateScheduler(IScheduleStore store, DispatchJsonSerializer serializer) =>
		new(store, serializer, Microsoft.Extensions.Options.Options.Create(new SchedulerOptions()),
			new CronScheduler(Microsoft.Extensions.Options.Options.Create(new CronScheduleOptions()), NullLogger<CronScheduler>.Instance),
			Microsoft.Extensions.Options.Options.Create(new CronScheduleOptions()), NullLogger<RecurringDispatchScheduler>.Instance);

	private sealed record MissingMetadataPayload(string Value);
}
