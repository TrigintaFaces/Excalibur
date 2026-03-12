// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.Jobs.Jobs;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Jobs.Tests.Jobs;

/// <summary>
/// Unit tests for <see cref="SnapshotCreationJob{TAggregate, TKey}"/>.
/// Verifies constructor guards, execute flow, missing dependency handling, and cancellation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class SnapshotCreationJobShould
{
	private readonly IServiceScopeFactory _fakeScopeFactory;
	private readonly IServiceScope _fakeScope;
	private readonly IServiceProvider _fakeScopeProvider;
	private readonly IEventSourcedRepository<StubAggregate, Guid> _fakeRepository;
	private readonly ISnapshotManager _fakeSnapshotManager;
	private readonly StubSnapshotCreationJob _sut;

	public SnapshotCreationJobShould()
	{
		_fakeScopeFactory = A.Fake<IServiceScopeFactory>();
		_fakeScope = A.Fake<IServiceScope>();
		_fakeScopeProvider = A.Fake<IServiceProvider>();
		_fakeRepository = A.Fake<IEventSourcedRepository<StubAggregate, Guid>>();
		_fakeSnapshotManager = A.Fake<ISnapshotManager>();

		A.CallTo(() => _fakeScopeFactory.CreateScope()).Returns(_fakeScope);
		A.CallTo(() => _fakeScope.ServiceProvider).Returns(_fakeScopeProvider);
		A.CallTo(() => _fakeScopeProvider.GetService(typeof(IEventSourcedRepository<StubAggregate, Guid>)))
			.Returns(_fakeRepository);
		A.CallTo(() => _fakeScopeProvider.GetService(typeof(ISnapshotManager)))
			.Returns(_fakeSnapshotManager);

		_sut = new StubSnapshotCreationJob(_fakeScopeFactory, NullLogger<StubSnapshotCreationJob>.Instance);
	}

	// --- Constructor null guards ---

	[Fact]
	public void ThrowWhenScopeFactoryIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new StubSnapshotCreationJob(null!, NullLogger<StubSnapshotCreationJob>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new StubSnapshotCreationJob(_fakeScopeFactory, null!));
	}

	// --- ExecuteAsync ---

	[Fact]
	public async Task ExecuteSuccessfully_WhenAggregatesExist()
	{
		// Arrange
		var id1 = Guid.NewGuid();
		var id2 = Guid.NewGuid();
		_sut.AggregateIdsToReturn = [id1, id2];

		var aggregate1 = new StubAggregate(id1);
		var aggregate2 = new StubAggregate(id2);
		var fakeSnapshot = A.Fake<ISnapshot>();

		A.CallTo(() => _fakeRepository.GetByIdAsync(id1, A<CancellationToken>._))
			.Returns(aggregate1);
		A.CallTo(() => _fakeRepository.GetByIdAsync(id2, A<CancellationToken>._))
			.Returns(aggregate2);
		A.CallTo(() => _fakeSnapshotManager.CreateSnapshotAsync(A<StubAggregate>._, A<CancellationToken>._))
			.Returns(fakeSnapshot);

		// Act
		await _sut.ExecuteAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => _fakeSnapshotManager.CreateSnapshotAsync(A<StubAggregate>._, A<CancellationToken>._))
			.MustHaveHappened(2, Times.Exactly);
		A.CallTo(() => _fakeSnapshotManager.SaveSnapshotAsync(A<string>._, fakeSnapshot, A<CancellationToken>._))
			.MustHaveHappened(2, Times.Exactly);
	}

	[Fact]
	public async Task SkipNullAggregates()
	{
		// Arrange
		var id1 = Guid.NewGuid();
		_sut.AggregateIdsToReturn = [id1];

		A.CallTo(() => _fakeRepository.GetByIdAsync(id1, A<CancellationToken>._))
			.Returns((StubAggregate?)null);

		// Act
		await _sut.ExecuteAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => _fakeSnapshotManager.CreateSnapshotAsync(A<StubAggregate>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ReturnEarly_WhenRepositoryIsNull()
	{
		// Arrange
		A.CallTo(() => _fakeScopeProvider.GetService(typeof(IEventSourcedRepository<StubAggregate, Guid>)))
			.Returns(null);

		// Act -- should not throw
		await Should.NotThrowAsync(() => _sut.ExecuteAsync(CancellationToken.None));

		// Assert
		A.CallTo(() => _fakeSnapshotManager.CreateSnapshotAsync(A<StubAggregate>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ReturnEarly_WhenSnapshotManagerIsNull()
	{
		// Arrange
		A.CallTo(() => _fakeScopeProvider.GetService(typeof(ISnapshotManager)))
			.Returns(null);

		// Act -- should not throw
		await Should.NotThrowAsync(() => _sut.ExecuteAsync(CancellationToken.None));
	}

	[Fact]
	public async Task RethrowOperationCanceledException()
	{
		// Arrange
		var id1 = Guid.NewGuid();
		_sut.AggregateIdsToReturn = [id1];

		var aggregate = new StubAggregate(id1);
		A.CallTo(() => _fakeRepository.GetByIdAsync(id1, A<CancellationToken>._))
			.Returns(aggregate);

		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(() =>
			_sut.ExecuteAsync(cts.Token));
	}

	[Fact]
	public async Task RethrowGeneralException()
	{
		// Arrange
		var id1 = Guid.NewGuid();
		_sut.AggregateIdsToReturn = [id1];

		var aggregate = new StubAggregate(id1);
		A.CallTo(() => _fakeRepository.GetByIdAsync(id1, A<CancellationToken>._))
			.Returns(aggregate);
		A.CallTo(() => _fakeSnapshotManager.CreateSnapshotAsync(A<StubAggregate>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Snapshot failed"));

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.ExecuteAsync(CancellationToken.None));
	}

	[Fact]
	public async Task HandleEmptyAggregateIdList()
	{
		// Arrange
		_sut.AggregateIdsToReturn = [];

		// Act
		await _sut.ExecuteAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => _fakeRepository.GetByIdAsync(A<Guid>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}
}

/// <summary>
/// Concrete stub aggregate for testing SnapshotCreationJob.
/// </summary>
public sealed class StubAggregate(Guid id) : IAggregateRoot<Guid>, IAggregateSnapshotSupport
{
	public Guid Id { get; } = id;
	string IAggregateRoot.Id => Id.ToString();
	public long Version => 1;
	public IReadOnlyList<IDomainEvent> GetUncommittedEvents() => [];
	public void MarkEventsAsCommitted() { }
	public void ApplyEvent(IDomainEvent eventData) { }
	public object? GetService(Type serviceType) => null;
	public string AggregateType => "StubAggregate";
	public string? ETag { get; set; }
	public void LoadFromHistory(IEnumerable<IDomainEvent> history) { }
	public void LoadFromSnapshot(ISnapshot snapshot) { }

	[RequiresUnreferencedCode("Test")]
	[RequiresDynamicCode("Test")]
	public ISnapshot CreateSnapshot() => throw new NotSupportedException();
}

/// <summary>
/// Concrete test subclass of SnapshotCreationJob for testing.
/// </summary>
[RequiresUnreferencedCode("Test")]
[RequiresDynamicCode("Test")]
public sealed class StubSnapshotCreationJob(
	IServiceScopeFactory scopeFactory,
	ILogger logger)
	: SnapshotCreationJob<StubAggregate, Guid>(scopeFactory, logger)
{
	public IReadOnlyList<Guid> AggregateIdsToReturn { get; set; } = [];

	protected override Task<IReadOnlyList<Guid>> GetAggregateIdsAsync(
		IServiceProvider serviceProvider,
		CancellationToken cancellationToken)
		=> Task.FromResult(AggregateIdsToReturn);
}
