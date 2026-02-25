// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Migration;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Core.Migration;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class MigrationRunnerShould
{
	private readonly IEventBatchMigrator _migrator;
	private readonly MigrationRunner _sut;

	public MigrationRunnerShould()
	{
		_migrator = A.Fake<IEventBatchMigrator>();
		_sut = new MigrationRunner(_migrator, NullLogger<MigrationRunner>.Instance);
	}

	[Fact]
	public async Task RunAsync_ReturnEmptyResult_WhenNoPlansFound()
	{
		// Arrange
		A.CallTo(() => _migrator.CreatePlanAsync(A<MigrationOptions>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<MigrationPlan>>([]));

		var options = new MigrationRunnerOptions();

		// Act
		var result = await _sut.RunAsync(options, CancellationToken.None);

		// Assert
		result.EventsMigrated.ShouldBe(0);
		result.EventsSkipped.ShouldBe(0);
		result.StreamsMigrated.ShouldBe(0);
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public async Task RunAsync_ExecutePlansSequentially_WhenParallelIs1()
	{
		// Arrange
		var plan1 = new MigrationPlan("s1", "t1");
		var plan2 = new MigrationPlan("s2", "t2");

		A.CallTo(() => _migrator.CreatePlanAsync(A<MigrationOptions>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<MigrationPlan>>([plan1, plan2]));

		A.CallTo(() => _migrator.MigrateAsync(A<MigrationPlan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new EventMigrationResult(10, 1, 1, false, [])));

		var options = new MigrationRunnerOptions { ParallelStreams = 1 };

		// Act
		var result = await _sut.RunAsync(options, CancellationToken.None);

		// Assert
		result.EventsMigrated.ShouldBe(20);
		result.EventsSkipped.ShouldBe(2);
		result.StreamsMigrated.ShouldBe(2);
	}

	[Fact]
	public async Task RunAsync_ExecutePlansInParallel_WhenParallelGreaterThan1()
	{
		// Arrange
		var plan1 = new MigrationPlan("s1", "t1");
		var plan2 = new MigrationPlan("s2", "t2");

		A.CallTo(() => _migrator.CreatePlanAsync(A<MigrationOptions>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<MigrationPlan>>([plan1, plan2]));

		A.CallTo(() => _migrator.MigrateAsync(A<MigrationPlan>._, A<CancellationToken>._))
			.Returns(Task.FromResult(new EventMigrationResult(5, 0, 1, false, [])));

		var options = new MigrationRunnerOptions { ParallelStreams = 4 };

		// Act
		var result = await _sut.RunAsync(options, CancellationToken.None);

		// Assert
		result.EventsMigrated.ShouldBe(10);
		result.StreamsMigrated.ShouldBe(2);
	}

	[Fact]
	public async Task RunAsync_StopOnError_WhenContinueOnErrorIsFalse()
	{
		// Arrange
		var plan1 = new MigrationPlan("s1", "t1");
		var plan2 = new MigrationPlan("s2", "t2");

		A.CallTo(() => _migrator.CreatePlanAsync(A<MigrationOptions>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<MigrationPlan>>([plan1, plan2]));

		A.CallTo(() => _migrator.MigrateAsync(plan1, A<CancellationToken>._))
			.Returns(Task.FromResult(new EventMigrationResult(5, 0, 1, false, ["error"])));

		var options = new MigrationRunnerOptions { ParallelStreams = 1, ContinueOnError = false };

		// Act
		var result = await _sut.RunAsync(options, CancellationToken.None);

		// Assert
		result.Errors.Count.ShouldBe(1);
		A.CallTo(() => _migrator.MigrateAsync(plan2, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task RunAsync_PassDryRunOption()
	{
		// Arrange
		A.CallTo(() => _migrator.CreatePlanAsync(A<MigrationOptions>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<MigrationPlan>>([]));

		var options = new MigrationRunnerOptions { DryRun = true };

		// Act
		var result = await _sut.RunAsync(options, CancellationToken.None);

		// Assert
		result.IsDryRun.ShouldBeTrue();
	}

	[Fact]
	public async Task RunAsync_ThrowOnNullOptions()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.RunAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ValidateAsync_ReturnTrue_WhenAllPlansValid()
	{
		// Arrange
		A.CallTo(() => _migrator.CreatePlanAsync(A<MigrationOptions>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<MigrationPlan>>(
				[new MigrationPlan("source", "target")]));

		// Act
		var result = await _sut.ValidateAsync(CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task ValidateAsync_ReturnFalse_WhenSourceStreamIsEmpty()
	{
		// Arrange
		A.CallTo(() => _migrator.CreatePlanAsync(A<MigrationOptions>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<MigrationPlan>>(
				[new MigrationPlan("", "target")]));

		// Act
		var result = await _sut.ValidateAsync(CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task ValidateAsync_ReturnFalse_WhenTargetStreamIsEmpty()
	{
		// Arrange
		A.CallTo(() => _migrator.CreatePlanAsync(A<MigrationOptions>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<MigrationPlan>>(
				[new MigrationPlan("source", "")]));

		// Act
		var result = await _sut.ValidateAsync(CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task ValidateAsync_ReturnFalse_WhenSourceEqualsTarget()
	{
		// Arrange
		A.CallTo(() => _migrator.CreatePlanAsync(A<MigrationOptions>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<MigrationPlan>>(
				[new MigrationPlan("same", "same")]));

		// Act
		var result = await _sut.ValidateAsync(CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task ValidateAsync_ReturnFalse_WhenMigratorThrows()
	{
		// Arrange
		A.CallTo(() => _migrator.CreatePlanAsync(A<MigrationOptions>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("failed"));

		// Act
		var result = await _sut.ValidateAsync(CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ThrowOnNullConstructorArgs()
	{
		var logger = NullLogger<MigrationRunner>.Instance;
		Should.Throw<ArgumentNullException>(() => new MigrationRunner(null!, logger));
		Should.Throw<ArgumentNullException>(() => new MigrationRunner(_migrator, null!));
	}
}
