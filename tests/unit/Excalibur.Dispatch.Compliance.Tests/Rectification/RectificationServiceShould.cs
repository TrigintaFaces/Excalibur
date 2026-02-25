using Excalibur.Dispatch.Compliance.Rectification;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance.Tests.Rectification;

public class RectificationServiceShould
{
    private readonly RectificationService _sut;
    private readonly RectificationOptions _options = new();

    public RectificationServiceShould()
    {
        _sut = new RectificationService(
            Microsoft.Extensions.Options.Options.Create(_options),
            NullLogger<RectificationService>.Instance);
    }

    [Fact]
    public async Task Rectify_and_record_history()
    {
        var request = new RectificationRequest(
            SubjectId: "user-1",
            FieldName: "Email",
            OldValue: "old@example.com",
            NewValue: "new@example.com",
            Reason: "Correction");

        await _sut.RectifyAsync(request, CancellationToken.None);

        var history = await _sut.GetRectificationHistoryAsync("user-1", CancellationToken.None);
        history.Count.ShouldBe(1);
        history[0].FieldName.ShouldBe("Email");
        history[0].OldValue.ShouldBe("old@example.com");
        history[0].NewValue.ShouldBe("new@example.com");
    }

    [Fact]
    public async Task Return_empty_list_for_unknown_subject()
    {
        var history = await _sut.GetRectificationHistoryAsync("unknown", CancellationToken.None);

        history.ShouldBeEmpty();
    }

    [Fact]
    public async Task Track_multiple_rectifications_for_same_subject()
    {
        await _sut.RectifyAsync(new RectificationRequest("user-1", "Email", "a", "b", "test"), CancellationToken.None);
        await _sut.RectifyAsync(new RectificationRequest("user-1", "Phone", "c", "d", "test"), CancellationToken.None);

        var history = await _sut.GetRectificationHistoryAsync("user-1", CancellationToken.None);
        history.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Order_history_by_rectification_time()
    {
        await _sut.RectifyAsync(new RectificationRequest("user-1", "First", "a", "b", "test"), CancellationToken.None);
        await _sut.RectifyAsync(new RectificationRequest("user-1", "Second", "c", "d", "test"), CancellationToken.None);

        var history = await _sut.GetRectificationHistoryAsync("user-1", CancellationToken.None);

        history[0].FieldName.ShouldBe("First");
        history[1].FieldName.ShouldBe("Second");
    }

    [Fact]
    public async Task Track_subject_count()
    {
        _sut.SubjectCount.ShouldBe(0);

        await _sut.RectifyAsync(new RectificationRequest("user-1", "Email", "a", "b", "test"), CancellationToken.None);

        _sut.SubjectCount.ShouldBe(1);
    }

    [Fact]
    public void Clear_all_history()
    {
        _sut.Clear();

        _sut.SubjectCount.ShouldBe(0);
    }

    [Fact]
    public async Task Throw_when_request_is_null()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.RectifyAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Throw_when_subject_id_is_null_for_history()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.GetRectificationHistoryAsync(null!, CancellationToken.None));
    }

    [Fact]
    public void Throw_when_options_are_null()
    {
        Should.Throw<ArgumentNullException>(
            () => new RectificationService(null!, NullLogger<RectificationService>.Instance));
    }

    [Fact]
    public void Throw_when_logger_is_null()
    {
        Should.Throw<ArgumentNullException>(
            () => new RectificationService(Microsoft.Extensions.Options.Options.Create(new RectificationOptions()), null!));
    }
}
