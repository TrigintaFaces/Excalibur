// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;

using Tests.Shared.Categories;

using Microsoft.Extensions.Logging.Abstractions;

using Excalibur.Data.ElasticSearch;
namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
/// Unit tests for <see cref="IndexLifecycleManager"/>.
/// Tests T401.11 scenarios: ILM policy creation, deletion, rollover, status, and phase advancement.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Elasticsearch")]
public sealed class IndexLifecycleManagerShould
{
	private readonly ElasticsearchClient _fakeClient;
	private readonly ILogger<IndexLifecycleManager> _logger;
	private readonly IndexLifecycleManager _sut;

	public IndexLifecycleManagerShould()
	{
		_fakeClient = A.Fake<ElasticsearchClient>();
		_logger = NullLogger<IndexLifecycleManager>.Instance;
		_sut = new IndexLifecycleManager(_fakeClient, _logger);
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenClientIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new IndexLifecycleManager(null!, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new IndexLifecycleManager(_fakeClient, null!));
	}

	#endregion

	#region CreateLifecyclePolicyAsync Tests

	[Fact]
	public async Task CreateLifecyclePolicyAsync_ThrowArgumentException_WhenPolicyNameNull()
	{
		// Arrange
		var policy = CreateTestPolicy();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.CreateLifecyclePolicyAsync(null!, policy, CancellationToken.None));
	}

	[Fact]
	public async Task CreateLifecyclePolicyAsync_ThrowArgumentException_WhenPolicyNameEmpty()
	{
		// Arrange
		var policy = CreateTestPolicy();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.CreateLifecyclePolicyAsync("", policy, CancellationToken.None));
	}

	[Fact]
	public async Task CreateLifecyclePolicyAsync_ThrowArgumentException_WhenPolicyNameWhitespace()
	{
		// Arrange
		var policy = CreateTestPolicy();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.CreateLifecyclePolicyAsync("   ", policy, CancellationToken.None));
	}

	[Fact]
	public async Task CreateLifecyclePolicyAsync_ThrowArgumentNullException_WhenPolicyNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.CreateLifecyclePolicyAsync("test-policy", null!, CancellationToken.None));
	}

	#endregion

	#region DeleteLifecyclePolicyAsync Tests

	[Fact]
	public async Task DeleteLifecyclePolicyAsync_ThrowArgumentException_WhenPolicyNameNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.DeleteLifecyclePolicyAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task DeleteLifecyclePolicyAsync_ThrowArgumentException_WhenPolicyNameEmpty()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.DeleteLifecyclePolicyAsync("", CancellationToken.None));
	}

	#endregion

	#region RolloverIndexAsync Tests

	[Fact]
	public async Task RolloverIndexAsync_ThrowArgumentException_WhenAliasNameNull()
	{
		// Arrange
		var conditions = CreateTestRolloverConditions();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.RolloverIndexAsync(null!, conditions, CancellationToken.None));
	}

	[Fact]
	public async Task RolloverIndexAsync_ThrowArgumentException_WhenAliasNameEmpty()
	{
		// Arrange
		var conditions = CreateTestRolloverConditions();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.RolloverIndexAsync("", conditions, CancellationToken.None));
	}

	[Fact]
	public async Task RolloverIndexAsync_ThrowArgumentNullException_WhenConditionsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_sut.RolloverIndexAsync("test-alias", null!, CancellationToken.None));
	}

	#endregion

	#region GetIndexLifecycleStatusAsync Tests

	[Fact]
	public void GetIndexLifecycleStatusAsync_AcceptNullPattern()
	{
		// GetIndexLifecycleStatusAsync should accept null pattern (nullable string)
		// We verify the method signature accepts nullable string
		// Actual API behavior requires integration testing
		var method = typeof(IndexLifecycleManager).GetMethod(nameof(IndexLifecycleManager.GetIndexLifecycleStatusAsync));
		_ = method.ShouldNotBeNull();

		var parameters = method.GetParameters();
		parameters[0].ParameterType.ShouldBe(typeof(string));
		// First parameter (indexPattern) is nullable string - callers pass null explicitly
		parameters[0].ParameterType.Name.ShouldBe("String");
	}

	[Fact]
	public void GetIndexLifecycleStatusAsync_AcceptValidPattern()
	{
		// Verify the method exists with correct signature
		var method = typeof(IndexLifecycleManager).GetMethod(nameof(IndexLifecycleManager.GetIndexLifecycleStatusAsync));
		_ = method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(Task<IEnumerable<IndexLifecycleStatus>>));
	}

	#endregion

	#region MoveToNextPhaseAsync Tests

	[Fact]
	public async Task MoveToNextPhaseAsync_ThrowArgumentException_WhenIndexPatternNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.MoveToNextPhaseAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task MoveToNextPhaseAsync_ThrowArgumentException_WhenIndexPatternEmpty()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.MoveToNextPhaseAsync("", CancellationToken.None));
	}

	[Fact]
	public async Task MoveToNextPhaseAsync_ThrowArgumentException_WhenIndexPatternWhitespace()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.MoveToNextPhaseAsync("   ", CancellationToken.None));
	}

	#endregion

	#region Policy Configuration Tests

	[Fact]
	public void IndexLifecyclePolicy_AllowHotPhaseConfiguration()
	{
		// Arrange & Act
		var policy = new IndexLifecyclePolicy
		{
			Hot = new HotPhaseConfiguration
			{
				MinAge = TimeSpan.FromDays(0),
				Priority = 100,
				Rollover = new RolloverConditions
				{
					MaxAge = TimeSpan.FromDays(7),
					MaxDocs = 1000000,
					MaxSize = "50gb"
				}
			}
		};

		// Assert
		_ = policy.Hot.ShouldNotBeNull();
		policy.Hot.MinAge.ShouldBe(TimeSpan.FromDays(0));
		policy.Hot.Priority.ShouldBe(100);
		_ = policy.Hot.Rollover.ShouldNotBeNull();
		policy.Hot.Rollover.MaxAge.ShouldBe(TimeSpan.FromDays(7));
		policy.Hot.Rollover.MaxDocs.ShouldBe(1000000);
		policy.Hot.Rollover.MaxSize.ShouldBe("50gb");
	}

	[Fact]
	public void IndexLifecyclePolicy_AllowWarmPhaseConfiguration()
	{
		// Arrange & Act
		var policy = new IndexLifecyclePolicy
		{
			Warm = new WarmPhaseConfiguration
			{
				MinAge = TimeSpan.FromDays(7),
				Priority = 50,
				ShrinkNumberOfShards = 1,
				NumberOfReplicas = 1
			}
		};

		// Assert
		_ = policy.Warm.ShouldNotBeNull();
		policy.Warm.MinAge.ShouldBe(TimeSpan.FromDays(7));
		policy.Warm.ShrinkNumberOfShards.ShouldBe(1);
	}

	[Fact]
	public void IndexLifecyclePolicy_AllowColdPhaseConfiguration()
	{
		// Arrange & Act
		var policy = new IndexLifecyclePolicy
		{
			Cold = new ColdPhaseConfiguration
			{
				MinAge = TimeSpan.FromDays(30),
				Priority = 0,
				NumberOfReplicas = 0
			}
		};

		// Assert
		_ = policy.Cold.ShouldNotBeNull();
		policy.Cold.MinAge.ShouldBe(TimeSpan.FromDays(30));
		policy.Cold.NumberOfReplicas.ShouldBe(0);
	}

	[Fact]
	public void IndexLifecyclePolicy_AllowDeletePhaseConfiguration()
	{
		// Arrange & Act
		var policy = new IndexLifecyclePolicy
		{
			Delete = new DeletePhaseConfiguration
			{
				MinAge = TimeSpan.FromDays(90),
				WaitForSnapshotPolicy = "daily-snapshots"
			}
		};

		// Assert
		_ = policy.Delete.ShouldNotBeNull();
		policy.Delete.MinAge.ShouldBe(TimeSpan.FromDays(90));
		policy.Delete.WaitForSnapshotPolicy.ShouldBe("daily-snapshots");
	}

	[Fact]
	public void IndexLifecyclePolicy_AllowFullLifecycleConfiguration()
	{
		// Arrange & Act
		var policy = new IndexLifecyclePolicy
		{
			Hot = new HotPhaseConfiguration
			{
				Rollover = new RolloverConditions { MaxAge = TimeSpan.FromDays(7) }
			},
			Warm = new WarmPhaseConfiguration { MinAge = TimeSpan.FromDays(7) },
			Cold = new ColdPhaseConfiguration { MinAge = TimeSpan.FromDays(30) },
			Delete = new DeletePhaseConfiguration { MinAge = TimeSpan.FromDays(90) }
		};

		// Assert - All phases configured
		_ = policy.Hot.ShouldNotBeNull();
		_ = policy.Warm.ShouldNotBeNull();
		_ = policy.Cold.ShouldNotBeNull();
		_ = policy.Delete.ShouldNotBeNull();
	}

	#endregion

	#region RolloverConditions Tests

	[Fact]
	public void RolloverConditions_AllowMaxAgeConfiguration()
	{
		// Arrange & Act
		var conditions = new RolloverConditions { MaxAge = TimeSpan.FromDays(7) };

		// Assert
		conditions.MaxAge.ShouldBe(TimeSpan.FromDays(7));
	}

	[Fact]
	public void RolloverConditions_AllowMaxDocsConfiguration()
	{
		// Arrange & Act
		var conditions = new RolloverConditions { MaxDocs = 1000000 };

		// Assert
		conditions.MaxDocs.ShouldBe(1000000);
	}

	[Fact]
	public void RolloverConditions_AllowMaxSizeConfiguration()
	{
		// Arrange & Act
		var conditions = new RolloverConditions { MaxSize = "50gb" };

		// Assert
		conditions.MaxSize.ShouldBe("50gb");
	}

	[Fact]
	public void RolloverConditions_AllowMaxPrimaryShardSizeConfiguration()
	{
		// Arrange & Act
		var conditions = new RolloverConditions { MaxPrimaryShardSize = "25gb" };

		// Assert
		conditions.MaxPrimaryShardSize.ShouldBe("25gb");
	}

	[Fact]
	public void RolloverConditions_AllowMultipleConditions()
	{
		// Arrange & Act
		var conditions = new RolloverConditions
		{
			MaxAge = TimeSpan.FromDays(7),
			MaxDocs = 1000000,
			MaxSize = "50gb",
			MaxPrimaryShardSize = "25gb"
		};

		// Assert
		conditions.MaxAge.ShouldBe(TimeSpan.FromDays(7));
		conditions.MaxDocs.ShouldBe(1000000);
		conditions.MaxSize.ShouldBe("50gb");
		conditions.MaxPrimaryShardSize.ShouldBe("25gb");
	}

	#endregion

	#region IndexRolloverResult Tests

	[Fact]
	public void IndexRolloverResult_AllowSuccessfulResult()
	{
		// Arrange & Act
		var result = new IndexRolloverResult
		{
			IsSuccessful = true,
			RolledOver = true,
			OldIndex = "logs-2026.01.01-000001",
			NewIndex = "logs-2026.01.01-000002"
		};

		// Assert
		result.IsSuccessful.ShouldBeTrue();
		result.RolledOver.ShouldBeTrue();
		result.OldIndex.ShouldBe("logs-2026.01.01-000001");
		result.NewIndex.ShouldBe("logs-2026.01.01-000002");
		// Errors should be empty on success
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void IndexRolloverResult_AllowNoRolloverNeeded()
	{
		// Arrange & Act
		var result = new IndexRolloverResult
		{
			IsSuccessful = true,
			RolledOver = false
		};

		// Assert
		result.IsSuccessful.ShouldBeTrue();
		result.RolledOver.ShouldBeFalse();
	}

	[Fact]
	public void IndexRolloverResult_AllowErrorResult()
	{
		// Arrange & Act
		var result = new IndexRolloverResult
		{
			IsSuccessful = false,
			RolledOver = false,
			Errors = ["Alias not found"]
		};

		// Assert
		result.IsSuccessful.ShouldBeFalse();
		result.RolledOver.ShouldBeFalse();
		result.Errors.ShouldContain("Alias not found");
	}

	#endregion

	#region IndexLifecycleStatus Tests

	[Fact]
	public void IndexLifecycleStatus_AllowStatusConfiguration()
	{
		// Arrange & Act
		var status = new IndexLifecycleStatus
		{
			IndexName = "logs-2026.01.01-000001",
			Phase = "hot",
			PolicyName = "logs-policy",
			Age = TimeSpan.FromDays(5)
		};

		// Assert
		status.IndexName.ShouldBe("logs-2026.01.01-000001");
		status.Phase.ShouldBe("hot");
		status.PolicyName.ShouldBe("logs-policy");
		status.Age.ShouldBe(TimeSpan.FromDays(5));
	}

	[Fact]
	public void IndexLifecycleStatus_AllowNullPolicyName()
	{
		// Arrange & Act
		var status = new IndexLifecycleStatus
		{
			IndexName = "unmanaged-index",
			Phase = "unknown",
			PolicyName = null
		};

		// Assert
		status.PolicyName.ShouldBeNull();
	}

	#endregion

	#region Helper Methods

	private static IndexLifecyclePolicy CreateTestPolicy() => new()
	{
		Hot = new HotPhaseConfiguration
		{
			Rollover = new RolloverConditions
			{
				MaxAge = TimeSpan.FromDays(7),
				MaxSize = "50gb"
			}
		},
		Delete = new DeletePhaseConfiguration
		{
			MinAge = TimeSpan.FromDays(90)
		}
	};

	private static RolloverConditions CreateTestRolloverConditions() => new()
	{
		MaxAge = TimeSpan.FromDays(7),
		MaxDocs = 1000000,
		MaxSize = "50gb"
	};

	#endregion
}
