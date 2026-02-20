using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Erasure;

public class CascadeErasureServiceShould
{
    private readonly IErasureService _erasureService;
    private readonly ICascadeRelationshipResolver _resolver;
    private readonly CascadeErasureService _sut;

    public CascadeErasureServiceShould()
    {
        _erasureService = A.Fake<IErasureService>();
        _resolver = A.Fake<ICascadeRelationshipResolver>();

        A.CallTo(() => _erasureService.RequestErasureAsync(
            A<ErasureRequest>._, A<CancellationToken>._))
            .Returns(ErasureResult.Scheduled(Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(72), null));

        _sut = new CascadeErasureService(
            _erasureService,
            _resolver,
            NullLogger<CascadeErasureService>.Instance);
    }

    [Fact]
    public async Task Erase_primary_subject_only_when_no_related()
    {
        A.CallTo(() => _resolver.GetRelatedSubjectsAsync(
            A<string>._, A<CancellationToken>._))
            .Returns(new List<string>());

        var options = new CascadeErasureOptions
        {
            IncludeRelatedRecords = true,
            RelationshipDepth = 2
        };

        var result = await _sut.EraseWithCascadeAsync("user-1", options, CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.SubjectsErased.ShouldBe(1);
        result.PrimarySubjectId.ShouldBe("user-1");
    }

    [Fact]
    public async Task Erase_related_subjects_via_bfs()
    {
        A.CallTo(() => _resolver.GetRelatedSubjectsAsync("user-1", A<CancellationToken>._))
            .Returns(new List<string> { "related-1", "related-2" });
        A.CallTo(() => _resolver.GetRelatedSubjectsAsync("related-1", A<CancellationToken>._))
            .Returns(new List<string>());
        A.CallTo(() => _resolver.GetRelatedSubjectsAsync("related-2", A<CancellationToken>._))
            .Returns(new List<string>());

        var options = new CascadeErasureOptions
        {
            IncludeRelatedRecords = true,
            RelationshipDepth = 2
        };

        var result = await _sut.EraseWithCascadeAsync("user-1", options, CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.SubjectsErased.ShouldBe(3);
        result.RelatedSubjectsErased.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Perform_dry_run_without_actual_erasure()
    {
        A.CallTo(() => _resolver.GetRelatedSubjectsAsync("user-1", A<CancellationToken>._))
            .Returns(new List<string> { "related-1" });
        A.CallTo(() => _resolver.GetRelatedSubjectsAsync("related-1", A<CancellationToken>._))
            .Returns(new List<string>());

        var options = new CascadeErasureOptions
        {
            IncludeRelatedRecords = true,
            RelationshipDepth = 2,
            DryRun = true
        };

        var result = await _sut.EraseWithCascadeAsync("user-1", options, CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.IsDryRun.ShouldBeTrue();
        result.SubjectsErased.ShouldBe(2);

        A.CallTo(() => _erasureService.RequestErasureAsync(
            A<ErasureRequest>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Skip_related_records_when_not_included()
    {
        var options = new CascadeErasureOptions
        {
            IncludeRelatedRecords = false,
            RelationshipDepth = 2
        };

        var result = await _sut.EraseWithCascadeAsync("user-1", options, CancellationToken.None);

        result.SubjectsErased.ShouldBe(1);

        A.CallTo(() => _resolver.GetRelatedSubjectsAsync(
            A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Handle_exception_during_erasure()
    {
        A.CallTo(() => _erasureService.RequestErasureAsync(
            A<ErasureRequest>._, A<CancellationToken>._))
            .Throws(new InvalidOperationException("Erasure failed"));

        var options = new CascadeErasureOptions
        {
            IncludeRelatedRecords = false,
            RelationshipDepth = 1
        };

        var result = await _sut.EraseWithCascadeAsync("user-1", options, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Throw_when_subject_id_is_null()
    {
        var options = new CascadeErasureOptions
        {
            IncludeRelatedRecords = false,
            RelationshipDepth = 1
        };

        await Should.ThrowAsync<ArgumentException>(
            () => _sut.EraseWithCascadeAsync(null!, options, CancellationToken.None));
    }

    [Fact]
    public async Task Throw_when_options_are_null()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.EraseWithCascadeAsync("user-1", null!, CancellationToken.None));
    }

    [Fact]
    public void Throw_when_erasure_service_is_null()
    {
        Should.Throw<ArgumentNullException>(
            () => new CascadeErasureService(null!, _resolver, NullLogger<CascadeErasureService>.Instance));
    }

    [Fact]
    public void Throw_when_resolver_is_null()
    {
        Should.Throw<ArgumentNullException>(
            () => new CascadeErasureService(_erasureService, null!, NullLogger<CascadeErasureService>.Instance));
    }
}
