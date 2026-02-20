using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Erasure;

/// <summary>
/// Tests the cascade erasure service BFS traversal with cycle detection,
/// depth limiting, and multi-level relationship graphs.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class CascadeErasureServiceWorkflowShould
{
	private readonly IErasureService _erasureService = A.Fake<IErasureService>();
	private readonly ICascadeRelationshipResolver _resolver = A.Fake<ICascadeRelationshipResolver>();
	private readonly CascadeErasureService _sut;

	public CascadeErasureServiceWorkflowShould()
	{
		A.CallTo(() => _erasureService.RequestErasureAsync(
				A<ErasureRequest>._, A<CancellationToken>._))
			.Returns(ErasureResult.Scheduled(Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(72), null));

		_sut = new CascadeErasureService(
			_erasureService,
			_resolver,
			NullLogger<CascadeErasureService>.Instance);
	}

	[Fact]
	public async Task Handle_circular_references_without_infinite_loop()
	{
		// Arrange - A -> B -> A (cycle)
		A.CallTo(() => _resolver.GetRelatedSubjectsAsync("user-A", A<CancellationToken>._))
			.Returns(new List<string> { "user-B" });
		A.CallTo(() => _resolver.GetRelatedSubjectsAsync("user-B", A<CancellationToken>._))
			.Returns(new List<string> { "user-A" }); // Cycle back to A

		var options = new CascadeErasureOptions
		{
			IncludeRelatedRecords = true,
			RelationshipDepth = 10, // Deep enough that without cycle detection it would hang
		};

		// Act
		var result = await _sut.EraseWithCascadeAsync("user-A", options, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - should erase both but not loop infinitely
		result.Success.ShouldBeTrue();
		result.SubjectsErased.ShouldBe(2);
		result.RelatedSubjectsErased.ShouldContain("user-B");
	}

	[Fact]
	public async Task Limit_depth_when_graph_is_deeper()
	{
		// Arrange - A -> B -> C -> D, depth limit = 1
		A.CallTo(() => _resolver.GetRelatedSubjectsAsync("user-A", A<CancellationToken>._))
			.Returns(new List<string> { "user-B" });
		A.CallTo(() => _resolver.GetRelatedSubjectsAsync("user-B", A<CancellationToken>._))
			.Returns(new List<string> { "user-C" });
		A.CallTo(() => _resolver.GetRelatedSubjectsAsync("user-C", A<CancellationToken>._))
			.Returns(new List<string> { "user-D" });

		var options = new CascadeErasureOptions
		{
			IncludeRelatedRecords = true,
			RelationshipDepth = 1, // Only go 1 level deep from root
		};

		// Act
		var result = await _sut.EraseWithCascadeAsync("user-A", options, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - should erase A and B only (depth 1 from A)
		result.Success.ShouldBeTrue();
		result.SubjectsErased.ShouldBe(2);
		result.RelatedSubjectsErased.ShouldHaveSingleItem();
		result.RelatedSubjectsErased.ShouldContain("user-B");
	}

	[Fact]
	public async Task Traverse_multi_level_graph()
	{
		// Arrange - A -> [B, C], B -> [D], C -> [E]
		A.CallTo(() => _resolver.GetRelatedSubjectsAsync("user-A", A<CancellationToken>._))
			.Returns(new List<string> { "user-B", "user-C" });
		A.CallTo(() => _resolver.GetRelatedSubjectsAsync("user-B", A<CancellationToken>._))
			.Returns(new List<string> { "user-D" });
		A.CallTo(() => _resolver.GetRelatedSubjectsAsync("user-C", A<CancellationToken>._))
			.Returns(new List<string> { "user-E" });
		A.CallTo(() => _resolver.GetRelatedSubjectsAsync("user-D", A<CancellationToken>._))
			.Returns(new List<string>());
		A.CallTo(() => _resolver.GetRelatedSubjectsAsync("user-E", A<CancellationToken>._))
			.Returns(new List<string>());

		var options = new CascadeErasureOptions
		{
			IncludeRelatedRecords = true,
			RelationshipDepth = 3,
		};

		// Act
		var result = await _sut.EraseWithCascadeAsync("user-A", options, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - all 5 subjects erased
		result.Success.ShouldBeTrue();
		result.SubjectsErased.ShouldBe(5);
		result.RelatedSubjectsErased.Count.ShouldBe(4);
	}

	[Fact]
	public async Task Dry_run_discovers_all_related_without_actual_erasure()
	{
		// Arrange
		A.CallTo(() => _resolver.GetRelatedSubjectsAsync("user-A", A<CancellationToken>._))
			.Returns(new List<string> { "user-B", "user-C" });
		A.CallTo(() => _resolver.GetRelatedSubjectsAsync("user-B", A<CancellationToken>._))
			.Returns(new List<string>());
		A.CallTo(() => _resolver.GetRelatedSubjectsAsync("user-C", A<CancellationToken>._))
			.Returns(new List<string>());

		var options = new CascadeErasureOptions
		{
			IncludeRelatedRecords = true,
			RelationshipDepth = 2,
			DryRun = true,
		};

		// Act
		var result = await _sut.EraseWithCascadeAsync("user-A", options, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();
		result.IsDryRun.ShouldBeTrue();
		result.SubjectsErased.ShouldBe(3);
		result.PrimarySubjectId.ShouldBe("user-A");

		// No actual erasure should have been requested
		A.CallTo(() => _erasureService.RequestErasureAsync(
				A<ErasureRequest>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task Handle_diamond_dependency_without_duplicate_erasure()
	{
		// Arrange - A -> [B, C], B -> [D], C -> [D] (diamond)
		A.CallTo(() => _resolver.GetRelatedSubjectsAsync("user-A", A<CancellationToken>._))
			.Returns(new List<string> { "user-B", "user-C" });
		A.CallTo(() => _resolver.GetRelatedSubjectsAsync("user-B", A<CancellationToken>._))
			.Returns(new List<string> { "user-D" });
		A.CallTo(() => _resolver.GetRelatedSubjectsAsync("user-C", A<CancellationToken>._))
			.Returns(new List<string> { "user-D" });
		A.CallTo(() => _resolver.GetRelatedSubjectsAsync("user-D", A<CancellationToken>._))
			.Returns(new List<string>());

		var options = new CascadeErasureOptions
		{
			IncludeRelatedRecords = true,
			RelationshipDepth = 3,
		};

		// Act
		var result = await _sut.EraseWithCascadeAsync("user-A", options, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - D should only appear once despite being reachable from B and C
		result.Success.ShouldBeTrue();
		result.SubjectsErased.ShouldBe(4);
	}

	[Fact]
	public async Task Handle_resolver_exception_gracefully()
	{
		// Arrange
		A.CallTo(() => _resolver.GetRelatedSubjectsAsync("user-A", A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Resolver service unavailable"));

		var options = new CascadeErasureOptions
		{
			IncludeRelatedRecords = true,
			RelationshipDepth = 2,
		};

		// Act
		var result = await _sut.EraseWithCascadeAsync("user-A", options, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - should fail gracefully
		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("Resolver service unavailable");
		result.PrimarySubjectId.ShouldBe("user-A");
	}

	[Fact]
	public async Task Create_erasure_request_for_each_subject()
	{
		// Arrange
		A.CallTo(() => _resolver.GetRelatedSubjectsAsync("user-A", A<CancellationToken>._))
			.Returns(new List<string> { "user-B" });
		A.CallTo(() => _resolver.GetRelatedSubjectsAsync("user-B", A<CancellationToken>._))
			.Returns(new List<string>());

		var options = new CascadeErasureOptions
		{
			IncludeRelatedRecords = true,
			RelationshipDepth = 2,
		};

		// Act
		await _sut.EraseWithCascadeAsync("user-A", options, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - one RequestErasureAsync per subject
		A.CallTo(() => _erasureService.RequestErasureAsync(
				A<ErasureRequest>.That.Matches(r => r.DataSubjectId == "user-A"),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _erasureService.RequestErasureAsync(
				A<ErasureRequest>.That.Matches(r => r.DataSubjectId == "user-B"),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}
}
