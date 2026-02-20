using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Breach;

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
	public async Task Transition_from_reported_to_subjects_notified()
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

		var notified = await sut.NotifyAffectedSubjectsAsync("b-transition", CancellationToken.None);

		// Assert
		notified.Status.ShouldBe(BreachNotificationStatus.SubjectsNotified);
		notified.SubjectsNotifiedAt.ShouldNotBeNull();
		notified.SubjectsNotifiedAt.Value.ShouldBeGreaterThanOrEqualTo(reported.ReportedAt!.Value);
	}

	[Fact]
	public async Task Auto_notify_bypasses_manual_notification_step()
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
		var result = await sut.ReportBreachAsync(report, CancellationToken.None);

		// Assert - already notified upon reporting
		result.Status.ShouldBe(BreachNotificationStatus.SubjectsNotified);
		result.SubjectsNotifiedAt.ShouldNotBeNull();
		result.ReportedAt.ShouldNotBeNull();
	}

	[Fact]
	public async Task Prevent_double_notification()
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

		await sut.ReportBreachAsync(report, CancellationToken.None);
		await sut.NotifyAffectedSubjectsAsync("b-double", CancellationToken.None);

		// Act & Assert - second notification should throw
		await Should.ThrowAsync<InvalidOperationException>(
			() => sut.NotifyAffectedSubjectsAsync("b-double", CancellationToken.None));
	}

	[Fact]
	public async Task Prevent_notification_of_auto_notified_breach()
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

		await sut.ReportBreachAsync(report, CancellationToken.None);

		// Act & Assert - manual notification after auto should throw
		await Should.ThrowAsync<InvalidOperationException>(
			() => sut.NotifyAffectedSubjectsAsync("b-auto-double", CancellationToken.None));
	}

	[Fact]
	public async Task Update_breach_status_in_store_after_notification()
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

		await sut.ReportBreachAsync(report, CancellationToken.None);
		await sut.NotifyAffectedSubjectsAsync("b-status", CancellationToken.None);

		// Act
		var status = await sut.GetBreachStatusAsync("b-status", CancellationToken.None);

		// Assert - status should reflect notification
		status.ShouldNotBeNull();
		status.Status.ShouldBe(BreachNotificationStatus.SubjectsNotified);
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
