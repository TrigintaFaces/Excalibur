// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Migration;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Core.Migration;

/// <summary>
/// Functional tests for <see cref="MigrationRunner"/> covering sequential/parallel execution,
/// validation, error handling, and cancellation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MigrationRunnerFunctionalShould
{
	private readonly IEventBatchMigrator _migrator = A.Fake<IEventBatchMigrator>();
	private readonly MigrationRunner _sut;

	public MigrationRunnerFunctionalShould()
	{
		_sut = new MigrationRunner(_migrator, NullLogger<MigrationRunner>.Instance);
	}

	[Fact]
	public async Task RunAsync_WithNoPlans_ShouldReturnZeroResults()
	{
		// Arrange
		A.CallTo(() => _migrator.CreatePlanAsync(A<MigrationOptions>._, A<CancellationToken>._))
			.Returns(new List<MigrationPlan>());

		var options = new MigrationRunnerOptions { DryRun = false };

		// Act
		var result = await _sut.RunAsync(options, CancellationToken.None);

		// Assert
		result.EventsMigrated.ShouldBe(0);
		result.EventsSkipped.ShouldBe(0);
		result.StreamsMigrated.ShouldBe(0);
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public async Task RunAsync_SequentialExecution_ShouldProcessAllPlans()
	{
		// Arrange
		var plans = new List<MigrationPlan>
		{
			new("stream-1", "target-1"),
			new("stream-2", "target-2"),
		};

		A.CallTo(() => _migrator.CreatePlanAsync(A<MigrationOptions>._, A<CancellationToken>._))
			.Returns(plans);

		A.CallTo(() => _migrator.MigrateAsync(A<MigrationPlan>._, A<CancellationToken>._))
			.Returns(new EventMigrationResult(10, 2, 1, false, []));

		var options = new MigrationRunnerOptions
		{
			ParallelStreams = 1, // sequential
			DryRun = false,
		};

		// Act
		var result = await _sut.RunAsync(options, CancellationToken.None);

		// Assert
		result.EventsMigrated.ShouldBe(20);
		result.EventsSkipped.ShouldBe(4);
		result.StreamsMigrated.ShouldBe(2);
		result.Errors.ShouldBeEmpty();
		A.CallTo(() => _migrator.MigrateAsync(A<MigrationPlan>._, A<CancellationToken>._))
			.MustHaveHappened(2, Times.Exactly);
	}

	[Fact]
	public async Task RunAsync_ParallelExecution_ShouldProcessAllPlans()
	{
		// Arrange
		var plans = new List<MigrationPlan>
		{
			new("stream-1", "target-1"),
			new("stream-2", "target-2"),
			new("stream-3", "target-3"),
		};

		A.CallTo(() => _migrator.CreatePlanAsync(A<MigrationOptions>._, A<CancellationToken>._))
			.Returns(plans);

		A.CallTo(() => _migrator.MigrateAsync(A<MigrationPlan>._, A<CancellationToken>._))
			.Returns(new EventMigrationResult(5, 1, 1, false, []));

		var options = new MigrationRunnerOptions
		{
			ParallelStreams = 4, // parallel
			DryRun = false,
		};

		// Act
		var result = await _sut.RunAsync(options, CancellationToken.None);

		// Assert
		result.EventsMigrated.ShouldBe(15);
		result.EventsSkipped.ShouldBe(3);
		result.StreamsMigrated.ShouldBe(3);
	}

	[Fact]
	public async Task RunAsync_Sequential_ShouldStopOnErrorWhenContinueOnErrorIsFalse()
	{
		// Arrange
		var plans = new List<MigrationPlan>
		{
			new("stream-1", "target-1"),
			new("stream-2", "target-2"),
		};

		A.CallTo(() => _migrator.CreatePlanAsync(A<MigrationOptions>._, A<CancellationToken>._))
			.Returns(plans);

		var firstResult = new EventMigrationResult(5, 0, 1, false, ["Error in stream-1"]);
		A.CallTo(() => _migrator.MigrateAsync(plans[0], A<CancellationToken>._))
			.Returns(firstResult);

		var options = new MigrationRunnerOptions
		{
			ParallelStreams = 1,
			ContinueOnError = false,
		};

		// Act
		var result = await _sut.RunAsync(options, CancellationToken.None);

		// Assert
		result.Errors.Count.ShouldBe(1);
		result.Errors[0].ShouldBe("Error in stream-1");
		// Second plan should not have been executed
		A.CallTo(() => _migrator.MigrateAsync(plans[1], A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task RunAsync_DryRunMode_ShouldPassDryRunToOptions()
	{
		// Arrange
		A.CallTo(() => _migrator.CreatePlanAsync(A<MigrationOptions>._, A<CancellationToken>._))
			.Returns(new List<MigrationPlan> { new("src", "tgt") });

		A.CallTo(() => _migrator.MigrateAsync(A<MigrationPlan>._, A<CancellationToken>._))
			.Returns(new EventMigrationResult(3, 0, 1, true, []));

		var options = new MigrationRunnerOptions { DryRun = true };

		// Act
		var result = await _sut.RunAsync(options, CancellationToken.None);

		// Assert
		result.IsDryRun.ShouldBeTrue();
		// Verify the migration options were created with DryRun=true
		A.CallTo(() => _migrator.CreatePlanAsync(
			A<MigrationOptions>.That.Matches(o => o.DryRun), A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ValidateAsync_WithValidPlans_ShouldReturnTrue()
	{
		// Arrange
		A.CallTo(() => _migrator.CreatePlanAsync(A<MigrationOptions>._, A<CancellationToken>._))
			.Returns(new List<MigrationPlan>
			{
				new("source-stream", "target-stream"),
			});

		// Act
		var result = await _sut.ValidateAsync(CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task ValidateAsync_WithSameSourceAndTarget_ShouldReturnFalse()
	{
		// Arrange
		A.CallTo(() => _migrator.CreatePlanAsync(A<MigrationOptions>._, A<CancellationToken>._))
			.Returns(new List<MigrationPlan>
			{
				new("same-stream", "same-stream"),
			});

		// Act
		var result = await _sut.ValidateAsync(CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task ValidateAsync_WithEmptySourceStream_ShouldReturnFalse()
	{
		// Arrange
		A.CallTo(() => _migrator.CreatePlanAsync(A<MigrationOptions>._, A<CancellationToken>._))
			.Returns(new List<MigrationPlan>
			{
				new("", "target-stream"),
			});

		// Act
		var result = await _sut.ValidateAsync(CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task ValidateAsync_WhenExceptionOccurs_ShouldReturnFalse()
	{
		// Arrange
		A.CallTo(() => _migrator.CreatePlanAsync(A<MigrationOptions>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Store unavailable"));

		// Act
		var result = await _sut.ValidateAsync(CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void Constructor_ShouldThrowOnNullMigrator()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MigrationRunner(null!, NullLogger<MigrationRunner>.Instance));
	}

	[Fact]
	public void Constructor_ShouldThrowOnNullLogger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MigrationRunner(_migrator, null!));
	}

	[Fact]
	public async Task RunAsync_ShouldThrowOnNullOptions()
	{
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.RunAsync(null!, CancellationToken.None));
	}
}
