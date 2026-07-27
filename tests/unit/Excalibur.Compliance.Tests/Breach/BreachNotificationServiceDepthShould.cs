using Excalibur.Compliance;
using Excalibur.Compliance.Breach;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Compliance.Tests.Breach;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class BreachNotificationServiceDepthShould
{
	private readonly BreachNotificationOptions _options = new();
	private readonly NullLogger<BreachNotificationService> _logger = NullLogger<BreachNotificationService>.Instance;

	[Fact]
	public async Task Report_breach_without_auto_notify()
	{
		_options.AutoNotify = false;
		var sut = CreateService();
		var report = CreateBreachReport();

		var result = await sut.ReportBreachAsync(report, CancellationToken.None).ConfigureAwait(false);

		result.Status.ShouldBe(BreachNotificationStatus.Reported);
		result.SubjectsNotifiedAt.ShouldBeNull();
		result.BreachId.ShouldBe(report.BreachId);
		result.ReportedAt.ShouldNotBe(default);
	}

	[Fact]
	public async Task Refuse_auto_notify_but_still_record_the_breach()
	{
		_options.AutoNotify = true;
		var sut = CreateService();
		var report = CreateBreachReport();

		// FLIPPED: asserted that AutoNotify stamps SubjectsNotified on a service with no transport. That
		// status + timestamp is the GDPR Art. 34 evidence a controller shows a regulator, so writing it
		// without notifying anyone fabricates a compliance record. Contract is now refusal.
		_ = await Should.ThrowAsync<NotSupportedException>(
			() => sut.ReportBreachAsync(report, CancellationToken.None)).ConfigureAwait(false);

		var recorded = await sut.GetBreachStatusAsync(report.BreachId, CancellationToken.None).ConfigureAwait(false);

		recorded.ShouldNotBeNull("the breach must still be recorded even when auto-notification is refused");
		recorded.Status.ShouldNotBe(BreachNotificationStatus.SubjectsNotified);
		recorded.SubjectsNotifiedAt.ShouldBeNull();
	}

	[Fact]
	public async Task Report_breach_calculates_notification_deadline()
	{
		_options.NotificationDeadlineHours = 72;
		_options.AutoNotify = false;
		var sut = CreateService();
		var detectedAt = DateTimeOffset.UtcNow;
		var report = CreateBreachReport(detectedAt: detectedAt);

		var result = await sut.ReportBreachAsync(report, CancellationToken.None).ConfigureAwait(false);

		var expectedDeadline = detectedAt.AddHours(72);
		result.NotificationDeadline.ShouldNotBeNull();
		result.NotificationDeadline.Value.ShouldBeInRange(
			expectedDeadline.AddSeconds(-1), expectedDeadline.AddSeconds(1));
	}

	[Fact]
	public async Task Get_breach_status_returns_null_for_unknown()
	{
		var sut = CreateService();

		var result = await sut.GetBreachStatusAsync("unknown", CancellationToken.None).ConfigureAwait(false);

		result.ShouldBeNull();
	}

	[Fact]
	public async Task Get_breach_status_returns_reported_breach()
	{
		_options.AutoNotify = false;
		var sut = CreateService();
		var report = CreateBreachReport();
		await sut.ReportBreachAsync(report, CancellationToken.None).ConfigureAwait(false);

		var status = await sut.GetBreachStatusAsync(report.BreachId, CancellationToken.None).ConfigureAwait(false);

		status.ShouldNotBeNull();
		status.BreachId.ShouldBe(report.BreachId);
	}

	[Fact]
	public async Task Refuse_to_write_the_notified_status_without_notifying()
	{
		_options.AutoNotify = false;
		var sut = CreateService();
		var report = CreateBreachReport();
		await sut.ReportBreachAsync(report, CancellationToken.None).ConfigureAwait(false);

		// FLIPPED: asserted the status transition that only a real notification may write.
		_ = await Should.ThrowAsync<NotSupportedException>(
			() => sut.NotifyAffectedSubjectsAsync(report.BreachId, CancellationToken.None)).ConfigureAwait(false);

		var after = await sut.GetBreachStatusAsync(report.BreachId, CancellationToken.None).ConfigureAwait(false);

		after.ShouldNotBeNull();
		after.Status.ShouldNotBe(BreachNotificationStatus.SubjectsNotified);
		after.SubjectsNotifiedAt.ShouldBeNull();
	}

	[Fact]
	public async Task Throw_when_notifying_unknown_breach()
	{
		var sut = CreateService();

		await Should.ThrowAsync<InvalidOperationException>(
			() => sut.NotifyAffectedSubjectsAsync("nonexistent", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Refuse_every_manual_notification_attempt()
	{
		_options.AutoNotify = false;
		var sut = CreateService();
		var report = CreateBreachReport();
		// FLIPPED: the double-notification guard was reachable only by first writing the false attestation.
		// Every attempt now refuses, which is strictly stronger — there is no state in which this service
		// reports a notification as done.
		await sut.ReportBreachAsync(report, CancellationToken.None).ConfigureAwait(false);

		_ = await Should.ThrowAsync<NotSupportedException>(
			() => sut.NotifyAffectedSubjectsAsync(report.BreachId, CancellationToken.None)).ConfigureAwait(false);

		_ = await Should.ThrowAsync<NotSupportedException>(
			() => sut.NotifyAffectedSubjectsAsync(report.BreachId, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Refuse_auto_notify_at_report_time()
	{
		_options.AutoNotify = true;
		var sut = CreateService();
		var report = CreateBreachReport();
		// FLIPPED: with AutoNotify refused, the report itself is the call that throws — the "already
		// auto-notified" state is no longer reachable, because it never represented a real notification.
		_ = await Should.ThrowAsync<NotSupportedException>(
			() => sut.ReportBreachAsync(report, CancellationToken.None)).ConfigureAwait(false);

		_ = await Should.ThrowAsync<NotSupportedException>(
			() => sut.NotifyAffectedSubjectsAsync(report.BreachId, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_for_null_report()
	{
		var sut = CreateService();

		await Should.ThrowAsync<ArgumentNullException>(
			() => sut.ReportBreachAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_for_null_or_whitespace_breach_id_in_status()
	{
		var sut = CreateService();

		await Should.ThrowAsync<ArgumentException>(
			() => sut.GetBreachStatusAsync(null!, CancellationToken.None)).ConfigureAwait(false);
		await Should.ThrowAsync<ArgumentException>(
			() => sut.GetBreachStatusAsync("", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_for_null_or_whitespace_breach_id_in_notify()
	{
		var sut = CreateService();

		await Should.ThrowAsync<ArgumentException>(
			() => sut.NotifyAffectedSubjectsAsync(null!, CancellationToken.None)).ConfigureAwait(false);
		await Should.ThrowAsync<ArgumentException>(
			() => sut.NotifyAffectedSubjectsAsync("", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public void Throw_for_null_options_in_constructor()
	{
		Should.Throw<ArgumentNullException>(
			() => new BreachNotificationService(null!, _logger));
	}

	[Fact]
	public void Throw_for_null_logger_in_constructor()
	{
		Should.Throw<ArgumentNullException>(
			() => new BreachNotificationService(
				Microsoft.Extensions.Options.Options.Create(_options), null!));
	}

	private BreachNotificationService CreateService() =>
		new(Microsoft.Extensions.Options.Options.Create(_options), _logger);

	private static BreachReport CreateBreachReport(DateTimeOffset? detectedAt = null) =>
		new()
		{
			BreachId = Guid.NewGuid().ToString("N"),
			DetectedAt = detectedAt ?? DateTimeOffset.UtcNow,
			AffectedSubjectCount = 100,
			Description = "Test breach",
			DataCategories = ["email", "name"]
		};
}
