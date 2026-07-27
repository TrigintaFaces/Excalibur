// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Compliance.Tests.Breach;

/// <summary>
/// Binds the rule that the framework never reports a statutory notification it did not perform.
/// </summary>
/// <remarks>
/// The defect this closes was not a missing feature but a false affirmative: the shipped default set
/// <c>SubjectsNotified</c>, stamped a timestamp and logged "notification sent" while sending nothing, so
/// an operator, an auditor and a dashboard all saw a discharged GDPR obligation that had never been met.
/// Every arm below resolves the service from a real container built through the production registration
/// path and observes what it does — a test asserting that some provider is registered proves nothing here,
/// because the defect was a registered provider that lied.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class BreachNotificationFailClosedShould
{
	// ---------- SAFETY ----------

	[Fact]
	public async Task Refuse_to_notify_when_the_default_has_no_transport()
	{
		var service = Resolve();
		var breachId = await ReportBreachAsync(service);

		_ = await Should.ThrowAsync<NotSupportedException>(
			() => service.NotifyAffectedSubjectsAsync(breachId, CancellationToken.None));
	}

	[Fact]
	public async Task Leave_the_status_unadvanced_after_refusing()
	{
		// The load-bearing arm: throwing is not enough if the record still claims the duty was discharged.
		var service = Resolve();
		var breachId = await ReportBreachAsync(service);

		_ = await Should.ThrowAsync<NotSupportedException>(
			() => service.NotifyAffectedSubjectsAsync(breachId, CancellationToken.None));

		var after = await service.GetBreachStatusAsync(breachId, CancellationToken.None);

		after.ShouldNotBeNull();
		after.Status.ShouldNotBe(
			BreachNotificationStatus.SubjectsNotified,
			"a refused notification must not leave a record asserting that subjects were notified");
		after.SubjectsNotifiedAt.ShouldBeNull(
			"a timestamp is an affirmative claim that notification happened at that moment");
	}

	[Fact]
	public async Task Name_the_obligation_and_the_remedy_when_it_refuses()
	{
		var service = Resolve();
		var breachId = await ReportBreachAsync(service);

		var error = await Should.ThrowAsync<NotSupportedException>(
			() => service.NotifyAffectedSubjectsAsync(breachId, CancellationToken.None));

		error.Message.ShouldContain(nameof(IBreachNotificationService));
		error.Message.ShouldContain("33");
	}

	[Fact]
	public async Task Refuse_the_AutoNotify_path_too_but_still_record_the_breach()
	{
		// The second false-affirmative path, reached automatically rather than by an explicit call and
		// therefore quieter. The bead named only NotifyAffectedSubjectsAsync; closing one path and leaving
		// this one open would have shipped half a fix.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddBreachNotification(o => o.AutoNotify = true);

		var service = services.BuildServiceProvider().GetRequiredService<IBreachNotificationService>();

		_ = await Should.ThrowAsync<NotSupportedException>(() => ReportBreachAsync(service));

		// The record must survive the refusal — losing the breach and its Article 33 deadline would be a
		// worse outcome than the defect being fixed.
		var recorded = await service.GetBreachStatusAsync("breach-1", CancellationToken.None);

		recorded.ShouldNotBeNull("the breach must still be recorded even though auto-notification failed");
		recorded.NotificationDeadline.ShouldNotBeNull();
		recorded.Status.ShouldNotBe(BreachNotificationStatus.SubjectsNotified);
		recorded.SubjectsNotifiedAt.ShouldBeNull();
	}

	// ---------- LIVENESS ----------

	[Fact]
	public async Task Still_record_and_report_breaches()
	{
		// Without this, "refuse everything" would satisfy every safety arm and make the service useless.
		var service = Resolve();
		var breachId = await ReportBreachAsync(service);

		var status = await service.GetBreachStatusAsync(breachId, CancellationToken.None);

		status.ShouldNotBeNull();
		status.BreachId.ShouldBe(breachId);
		status.NotificationDeadline.ShouldNotBeNull(
			"the 72-hour authority deadline is the part this service genuinely provides");
	}

	[Fact]
	public void Let_a_consumer_implementation_win_over_the_fallback()
	{
		// The escape hatch must work: a deployment that can really notify registers its own service.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton<IBreachNotificationService, RealNotifyingService>();
		_ = services.AddBreachNotification();

		using var provider = services.BuildServiceProvider();

		provider.GetRequiredService<IBreachNotificationService>()
			.ShouldBeOfType<RealNotifyingService>(
				"the fallback is TryAdd-registered, so an explicit consumer registration must win");
	}

	private static IBreachNotificationService Resolve()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddBreachNotification();

		return services.BuildServiceProvider().GetRequiredService<IBreachNotificationService>();
	}

	private static async Task<string> ReportBreachAsync(IBreachNotificationService service)
	{
		var result = await service.ReportBreachAsync(
			new BreachReport
			{
				BreachId = "breach-1",
				DetectedAt = DateTimeOffset.UnixEpoch,
				Description = "test",
				AffectedSubjectCount = 1,
			},
			CancellationToken.None);

		return result.BreachId;
	}

	private sealed class RealNotifyingService : IBreachNotificationService
	{
		public Task<BreachNotificationResult> ReportBreachAsync(BreachReport report, CancellationToken ct) =>
			throw new NotImplementedException();

		public Task<BreachNotificationResult?> GetBreachStatusAsync(string breachId, CancellationToken ct) =>
			throw new NotImplementedException();

		public Task<BreachNotificationResult> NotifyAffectedSubjectsAsync(string breachId, CancellationToken ct) =>
			throw new NotImplementedException();
	}
}
