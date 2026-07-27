using Microsoft.Extensions.Logging.Abstractions;

using Excalibur.Compliance.Breach;

using Excalibur.Compliance;namespace Excalibur.Compliance.Tests.Breach;

/// <summary>
/// Tests the breach notification service workflow including notification deadline
/// calculations, dual-report handling, and status transitions.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class BreachNotificationServiceWorkflowShould
{
	[Fact]
	public async Task Calculate_notification_deadline_from_detection_time()
	{
		// Arrange
		var options = new BreachNotificationOptions { NotificationDeadlineHours = 72 };
		var sut = CreateService(options);
		var detectedAt = DateTimeOffset.UtcNow.AddHours(-2);
		var report = new BreachReport
		{
			BreachId = "b-deadline",
			Description = "Test",
			DetectedAt = detectedAt,
			AffectedSubjectCount = 10,
		};

		// Act
		var result = await sut.ReportBreachAsync(report, CancellationToken.None);

		// Assert
		result.NotificationDeadline.ShouldNotBeNull();
		var expectedDeadline = detectedAt.AddHours(72);
		// Allow 1 second tolerance
		result.NotificationDeadline.Value.ShouldBeInRange(
			expectedDeadline.AddSeconds(-1), expectedDeadline.AddSeconds(1));
	}

	[Fact]
	public async Task Support_multiple_independent_breaches()
	{
		// Arrange
		var sut = CreateService();
		var breach1 = new BreachReport
		{
			BreachId = "breach-A",
			Description = "First breach",
			DetectedAt = DateTimeOffset.UtcNow,
			AffectedSubjectCount = 100,
		};
		var breach2 = new BreachReport
		{
			BreachId = "breach-B",
			Description = "Second breach",
			DetectedAt = DateTimeOffset.UtcNow,
			AffectedSubjectCount = 50,
		};

		// Act
		await sut.ReportBreachAsync(breach1, CancellationToken.None);
		await sut.ReportBreachAsync(breach2, CancellationToken.None);

		// Assert
		var status1 = await sut.GetBreachStatusAsync("breach-A", CancellationToken.None);
		var status2 = await sut.GetBreachStatusAsync("breach-B", CancellationToken.None);
		status1.ShouldNotBeNull();
		status2.ShouldNotBeNull();
		status1.BreachId.ShouldBe("breach-A");
		status2.BreachId.ShouldBe("breach-B");
	}

	[Fact]
	public async Task Refuse_the_transition_to_subjects_notified()
	{
		// Arrange
		var sut = CreateService();
		var report = new BreachReport
		{
			BreachId = "b-transition",
			Description = "Test",
			DetectedAt = DateTimeOffset.UtcNow,
			AffectedSubjectCount = 10,
		};

		// Act
		var reported = await sut.ReportBreachAsync(report, CancellationToken.None);
		reported.Status.ShouldBe(BreachNotificationStatus.Reported);

		// FLIPPED: the Reported -> SubjectsNotified transition may only be written by a path that actually
		// notified. This service has no transport, so the transition is refused and the record stays honest.
		_ = await Should.ThrowAsync<NotSupportedException>(
			() => sut.NotifyAffectedSubjectsAsync("b-transition", CancellationToken.None));

		var after = await sut.GetBreachStatusAsync("b-transition", CancellationToken.None);

		after.ShouldNotBeNull();
		after.Status.ShouldBe(BreachNotificationStatus.Reported, "the breach remains reported-but-unnotified");
		after.SubjectsNotifiedAt.ShouldBeNull();
	}

	[Fact]
	public async Task Refuse_auto_notify_but_keep_the_report()
	{
		// Arrange
		var options = new BreachNotificationOptions { AutoNotify = true };
		var sut = CreateService(options);
		var report = new BreachReport
		{
			BreachId = "b-auto",
			Description = "Auto-notified breach",
			DetectedAt = DateTimeOffset.UtcNow,
			AffectedSubjectCount = 500,
		};

		// Act
		// FLIPPED: "auto-notify bypasses the manual step" described a bypass of the notification itself.
		_ = await Should.ThrowAsync<NotSupportedException>(
			() => sut.ReportBreachAsync(report, CancellationToken.None));

		var recorded = await sut.GetBreachStatusAsync("b-auto", CancellationToken.None);

		recorded.ShouldNotBeNull("the report and its Art. 33 deadline survive the refusal");
		recorded.ReportedAt.ShouldNotBeNull();
		recorded.Status.ShouldNotBe(BreachNotificationStatus.SubjectsNotified);
		recorded.SubjectsNotifiedAt.ShouldBeNull();
	}

	[Fact]
	public async Task Refuse_every_notification_attempt()
	{
		// Arrange
		var sut = CreateService();
		var report = new BreachReport
		{
			BreachId = "b-double",
			Description = "Test",
			DetectedAt = DateTimeOffset.UtcNow,
			AffectedSubjectCount = 10,
		};

		// FLIPPED: reaching the double-notification guard required writing the false attestation first.
		await sut.ReportBreachAsync(report, CancellationToken.None);

		_ = await Should.ThrowAsync<NotSupportedException>(
			() => sut.NotifyAffectedSubjectsAsync("b-double", CancellationToken.None));

		_ = await Should.ThrowAsync<NotSupportedException>(
			() => sut.NotifyAffectedSubjectsAsync("b-double", CancellationToken.None));
	}

	[Fact]
	public async Task Refuse_both_auto_and_manual_notification()
	{
		// Arrange
		var options = new BreachNotificationOptions { AutoNotify = true };
		var sut = CreateService(options);
		var report = new BreachReport
		{
			BreachId = "b-auto-double",
			Description = "Auto-notified",
			DetectedAt = DateTimeOffset.UtcNow,
			AffectedSubjectCount = 10,
		};

		// FLIPPED: the auto-notified state is unreachable now — the report itself refuses.
		_ = await Should.ThrowAsync<NotSupportedException>(
			() => sut.ReportBreachAsync(report, CancellationToken.None));

		_ = await Should.ThrowAsync<NotSupportedException>(
			() => sut.NotifyAffectedSubjectsAsync("b-auto-double", CancellationToken.None));
	}

	[Fact]
	public async Task Never_persist_a_notified_status_without_notifying()
	{
		// Arrange
		var sut = CreateService();
		var report = new BreachReport
		{
			BreachId = "b-status",
			Description = "Test",
			DetectedAt = DateTimeOffset.UtcNow,
			AffectedSubjectCount = 10,
		};

		// FLIPPED: the store must NOT reflect a notification that never occurred. This is the arm that
		// most directly asserted the fabricated record, so it is the one most worth keeping — inverted.
		await sut.ReportBreachAsync(report, CancellationToken.None);

		_ = await Should.ThrowAsync<NotSupportedException>(
			() => sut.NotifyAffectedSubjectsAsync("b-status", CancellationToken.None));

		var status = await sut.GetBreachStatusAsync("b-status", CancellationToken.None);

		status.ShouldNotBeNull();
		status.Status.ShouldNotBe(
			BreachNotificationStatus.SubjectsNotified,
			"the persisted record is the evidence a controller shows a regulator; it must not claim a " +
			"notification that never happened");
		status.SubjectsNotifiedAt.ShouldBeNull();
	}

	[Fact]
	public async Task Throw_when_notifying_with_empty_breach_id()
	{
		var sut = CreateService();

		await Should.ThrowAsync<ArgumentException>(
			() => sut.NotifyAffectedSubjectsAsync("", CancellationToken.None));
	}

	[Fact]
	public async Task Throw_when_getting_status_with_empty_breach_id()
	{
		var sut = CreateService();

		await Should.ThrowAsync<ArgumentException>(
			() => sut.GetBreachStatusAsync("", CancellationToken.None));
	}

	[Fact]
	public async Task Overwrite_breach_when_same_id_reported_twice()
	{
		// Arrange
		var sut = CreateService();
		var report1 = new BreachReport
		{
			BreachId = "b-overwrite",
			Description = "First report",
			DetectedAt = DateTimeOffset.UtcNow.AddHours(-5),
			AffectedSubjectCount = 10,
		};
		var report2 = new BreachReport
		{
			BreachId = "b-overwrite",
			Description = "Updated report",
			DetectedAt = DateTimeOffset.UtcNow,
			AffectedSubjectCount = 200,
		};

		// Act
		await sut.ReportBreachAsync(report1, CancellationToken.None);
		var result = await sut.ReportBreachAsync(report2, CancellationToken.None);

		// Assert
		result.BreachId.ShouldBe("b-overwrite");
		result.Status.ShouldBe(BreachNotificationStatus.Reported);
	}

	private static BreachNotificationService CreateService(BreachNotificationOptions? options = null) =>
		new(
			Microsoft.Extensions.Options.Options.Create(options ?? new BreachNotificationOptions()),
			NullLogger<BreachNotificationService>.Instance);
}
