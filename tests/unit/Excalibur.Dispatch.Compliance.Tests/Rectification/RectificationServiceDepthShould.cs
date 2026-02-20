using Excalibur.Dispatch.Compliance.Rectification;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Rectification;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class RectificationServiceDepthShould
{
	private readonly RectificationOptions _rectificationOptions = new();
	private readonly NullLogger<RectificationService> _logger = NullLogger<RectificationService>.Instance;

	[Fact]
	public async Task Rectify_and_retrieve_history()
	{
		var sut = CreateService();
		var request = new RectificationRequest(
			SubjectId: "user-1",
			FieldName: "Email",
			OldValue: "old@test.com",
			NewValue: "new@test.com",
			Reason: "Subject requested correction");

		await sut.RectifyAsync(request, CancellationToken.None).ConfigureAwait(false);

		var history = await sut.GetRectificationHistoryAsync("user-1", CancellationToken.None).ConfigureAwait(false);

		history.ShouldHaveSingleItem();
		history[0].SubjectId.ShouldBe("user-1");
		history[0].FieldName.ShouldBe("Email");
		history[0].OldValue.ShouldBe("old@test.com");
		history[0].NewValue.ShouldBe("new@test.com");
		history[0].Reason.ShouldBe("Subject requested correction");
		history[0].RectifiedAt.ShouldNotBe(default);
	}

	[Fact]
	public async Task Return_empty_history_for_unknown_subject()
	{
		var sut = CreateService();

		var history = await sut.GetRectificationHistoryAsync("nonexistent", CancellationToken.None).ConfigureAwait(false);

		history.ShouldBeEmpty();
	}

	[Fact]
	public async Task Return_history_sorted_by_rectified_at()
	{
		var sut = CreateService();

		await sut.RectifyAsync(new RectificationRequest("user-1", "Field1", "a", "b", "reason1"), CancellationToken.None).ConfigureAwait(false);
		await sut.RectifyAsync(new RectificationRequest("user-1", "Field2", "c", "d", "reason2"), CancellationToken.None).ConfigureAwait(false);
		await sut.RectifyAsync(new RectificationRequest("user-1", "Field3", "e", "f", "reason3"), CancellationToken.None).ConfigureAwait(false);

		var history = await sut.GetRectificationHistoryAsync("user-1", CancellationToken.None).ConfigureAwait(false);

		history.Count.ShouldBe(3);
		for (var i = 1; i < history.Count; i++)
		{
			history[i].RectifiedAt.ShouldBeGreaterThanOrEqualTo(history[i - 1].RectifiedAt);
		}
	}

	[Fact]
	public async Task Rectify_with_audit_all_changes_enabled()
	{
		_rectificationOptions.AuditAllChanges = true;
		var sut = CreateService();

		var request = new RectificationRequest("user-1", "Name", "Old Name", "New Name", "Correction");

		await sut.RectifyAsync(request, CancellationToken.None).ConfigureAwait(false);

		// Should not throw; audit logging path exercised
		var history = await sut.GetRectificationHistoryAsync("user-1", CancellationToken.None).ConfigureAwait(false);
		history.ShouldHaveSingleItem();
	}

	[Fact]
	public async Task Rectify_with_audit_all_changes_disabled()
	{
		_rectificationOptions.AuditAllChanges = false;
		var sut = CreateService();

		var request = new RectificationRequest("user-1", "Name", "Old", "New", "Reason");

		await sut.RectifyAsync(request, CancellationToken.None).ConfigureAwait(false);

		var history = await sut.GetRectificationHistoryAsync("user-1", CancellationToken.None).ConfigureAwait(false);
		history.ShouldHaveSingleItem();
	}

	[Fact]
	public async Task Track_subject_count()
	{
		var sut = CreateService();

		sut.SubjectCount.ShouldBe(0);

		await sut.RectifyAsync(new RectificationRequest("user-1", "F", "a", "b", "r"), CancellationToken.None).ConfigureAwait(false);
		sut.SubjectCount.ShouldBe(1);

		await sut.RectifyAsync(new RectificationRequest("user-2", "F", "a", "b", "r"), CancellationToken.None).ConfigureAwait(false);
		sut.SubjectCount.ShouldBe(2);

		// Multiple rectifications for same subject don't increase count
		await sut.RectifyAsync(new RectificationRequest("user-1", "G", "c", "d", "r2"), CancellationToken.None).ConfigureAwait(false);
		sut.SubjectCount.ShouldBe(2);
	}

	[Fact]
	public async Task Clear_removes_all_history()
	{
		var sut = CreateService();

		await sut.RectifyAsync(new RectificationRequest("user-1", "F", "a", "b", "r"), CancellationToken.None).ConfigureAwait(false);
		await sut.RectifyAsync(new RectificationRequest("user-2", "F", "a", "b", "r"), CancellationToken.None).ConfigureAwait(false);

		sut.Clear();

		sut.SubjectCount.ShouldBe(0);
		var history = await sut.GetRectificationHistoryAsync("user-1", CancellationToken.None).ConfigureAwait(false);
		history.ShouldBeEmpty();
	}

	[Fact]
	public async Task Throw_for_null_request()
	{
		var sut = CreateService();

		await Should.ThrowAsync<ArgumentNullException>(
			() => sut.RectifyAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_for_null_or_empty_subject_id_in_history()
	{
		var sut = CreateService();

		await Should.ThrowAsync<ArgumentException>(
			() => sut.GetRectificationHistoryAsync(null!, CancellationToken.None)).ConfigureAwait(false);
		await Should.ThrowAsync<ArgumentException>(
			() => sut.GetRectificationHistoryAsync("", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public void Throw_for_null_options_in_constructor()
	{
		Should.Throw<ArgumentNullException>(
			() => new RectificationService(null!, _logger));
	}

	[Fact]
	public void Throw_for_null_logger_in_constructor()
	{
		Should.Throw<ArgumentNullException>(
			() => new RectificationService(
				Microsoft.Extensions.Options.Options.Create(_rectificationOptions), null!));
	}

	private RectificationService CreateService() =>
		new(Microsoft.Extensions.Options.Options.Create(_rectificationOptions), _logger);
}
