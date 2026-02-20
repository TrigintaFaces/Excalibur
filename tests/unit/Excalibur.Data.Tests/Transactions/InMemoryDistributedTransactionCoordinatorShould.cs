// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Transactions;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.Transactions;

/// <summary>
/// Unit tests for <see cref="InMemoryDistributedTransactionCoordinator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class InMemoryDistributedTransactionCoordinatorShould : IAsyncDisposable
{
	private readonly InMemoryDistributedTransactionCoordinator _sut;

	public InMemoryDistributedTransactionCoordinatorShould()
	{
		_sut = new InMemoryDistributedTransactionCoordinator(
			Microsoft.Extensions.Options.Options.Create(new DistributedTransactionOptions()),
			NullLogger<InMemoryDistributedTransactionCoordinator>.Instance);
	}

	public ValueTask DisposeAsync() => _sut.DisposeAsync();

	#region BeginAsync Tests

	[Fact]
	public async Task BeginAsync_ShouldReturnTransactionId()
	{
		var txId = await _sut.BeginAsync(CancellationToken.None);

		txId.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public async Task BeginAsync_ShouldReturnUniqueIds()
	{
		var txId1 = await _sut.BeginAsync(CancellationToken.None);
		await _sut.CommitAsync(CancellationToken.None); // Complete first before starting next

		var txId2 = await _sut.BeginAsync(CancellationToken.None);

		txId1.ShouldNotBe(txId2);
	}

	[Fact]
	public async Task BeginAsync_ShouldIncrementActiveTransactionCount()
	{
		_sut.ActiveTransactionCount.ShouldBe(0);

		await _sut.BeginAsync(CancellationToken.None);

		_sut.ActiveTransactionCount.ShouldBe(1);
	}

	#endregion

	#region EnlistAsync Tests

	[Fact]
	public async Task EnlistAsync_ShouldAddParticipant()
	{
		// Arrange
		await _sut.BeginAsync(CancellationToken.None);
		var participant = CreateParticipant("p1");

		// Act — should not throw
		await _sut.EnlistAsync(participant, CancellationToken.None);
	}

	[Fact]
	public async Task EnlistAsync_ShouldThrow_WhenParticipantIsNull()
	{
		await _sut.BeginAsync(CancellationToken.None);

		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.EnlistAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task EnlistAsync_ShouldThrow_WhenMaxParticipantsReached()
	{
		// Arrange — set max participants to 2
		await using var coordinator = new InMemoryDistributedTransactionCoordinator(
			Microsoft.Extensions.Options.Options.Create(new DistributedTransactionOptions { MaxParticipants = 2 }),
			NullLogger<InMemoryDistributedTransactionCoordinator>.Instance);

		await coordinator.BeginAsync(CancellationToken.None);
		await coordinator.EnlistAsync(CreateParticipant("p1"), CancellationToken.None);
		await coordinator.EnlistAsync(CreateParticipant("p2"), CancellationToken.None);

		// Act & Assert
		await Should.ThrowAsync<DistributedTransactionException>(
			() => coordinator.EnlistAsync(CreateParticipant("p3"), CancellationToken.None));
	}

	[Fact]
	public async Task EnlistAsync_ShouldThrow_WhenNoActiveTransaction()
	{
		await Should.ThrowAsync<DistributedTransactionException>(
			() => _sut.EnlistAsync(CreateParticipant("p1"), CancellationToken.None));
	}

	#endregion

	#region CommitAsync Tests — Full 2PC Lifecycle

	[Fact]
	public async Task CommitAsync_ShouldSucceed_WithNoParticipants()
	{
		// Arrange
		await _sut.BeginAsync(CancellationToken.None);

		// Act — should not throw
		await _sut.CommitAsync(CancellationToken.None);

		// Assert
		_sut.ActiveTransactionCount.ShouldBe(0);
	}

	[Fact]
	public async Task CommitAsync_ShouldCallPrepareAndCommitOnParticipants()
	{
		// Arrange
		await _sut.BeginAsync(CancellationToken.None);
		var participant = CreateParticipant("p1");
		await _sut.EnlistAsync(participant, CancellationToken.None);

		// Act
		await _sut.CommitAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => participant.PrepareAsync(A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => participant.CommitAsync(A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task CommitAsync_ShouldAutoRollback_WhenPrepareReturnsNo()
	{
		// Arrange
		await _sut.BeginAsync(CancellationToken.None);

		var failingParticipant = CreateParticipant("p1", preparesSuccessfully: false);
		var goodParticipant = CreateParticipant("p2");
		await _sut.EnlistAsync(failingParticipant, CancellationToken.None);
		await _sut.EnlistAsync(goodParticipant, CancellationToken.None);

		// Act & Assert
		var ex = await Should.ThrowAsync<DistributedTransactionException>(
			() => _sut.CommitAsync(CancellationToken.None));

		ex.FailedParticipantIds.ShouldContain("p1");

		// Both should have been rolled back (AutoRollbackOnPrepareFailure = true by default)
		A.CallTo(() => failingParticipant.RollbackAsync(A<CancellationToken>._))
			.MustHaveHappened();
		A.CallTo(() => goodParticipant.RollbackAsync(A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task CommitAsync_ShouldThrow_WhenPrepareThrows()
	{
		// Arrange
		await _sut.BeginAsync(CancellationToken.None);
		var participant = CreateParticipant("p1");
		A.CallTo(() => participant.PrepareAsync(A<CancellationToken>._))
			.Throws(new InvalidOperationException("Prepare failed"));
		await _sut.EnlistAsync(participant, CancellationToken.None);

		// Act & Assert
		await Should.ThrowAsync<DistributedTransactionException>(
			() => _sut.CommitAsync(CancellationToken.None));
	}

	[Fact]
	public async Task CommitAsync_ShouldThrow_WhenCommitPhaseFails()
	{
		// Arrange
		await _sut.BeginAsync(CancellationToken.None);
		var participant = CreateParticipant("p1");
		A.CallTo(() => participant.CommitAsync(A<CancellationToken>._))
			.Throws(new InvalidOperationException("Commit failed"));
		await _sut.EnlistAsync(participant, CancellationToken.None);

		// Act & Assert
		var ex = await Should.ThrowAsync<DistributedTransactionException>(
			() => _sut.CommitAsync(CancellationToken.None));

		ex.Message.ShouldContain("partially committed");
	}

	[Fact]
	public async Task CommitAsync_ShouldThrow_WhenNoActiveTransaction()
	{
		await Should.ThrowAsync<DistributedTransactionException>(
			() => _sut.CommitAsync(CancellationToken.None));
	}

	[Fact]
	public async Task CommitAsync_ShouldRemoveTransaction()
	{
		// Arrange
		await _sut.BeginAsync(CancellationToken.None);
		await _sut.EnlistAsync(CreateParticipant("p1"), CancellationToken.None);

		// Act
		await _sut.CommitAsync(CancellationToken.None);

		// Assert
		_sut.ActiveTransactionCount.ShouldBe(0);
	}

	#endregion

	#region RollbackAsync Tests

	[Fact]
	public async Task RollbackAsync_ShouldCallRollbackOnAllParticipants()
	{
		// Arrange
		await _sut.BeginAsync(CancellationToken.None);
		var p1 = CreateParticipant("p1");
		var p2 = CreateParticipant("p2");
		await _sut.EnlistAsync(p1, CancellationToken.None);
		await _sut.EnlistAsync(p2, CancellationToken.None);

		// Act
		await _sut.RollbackAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => p1.RollbackAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => p2.RollbackAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RollbackAsync_ShouldRemoveTransaction()
	{
		// Arrange
		await _sut.BeginAsync(CancellationToken.None);

		// Act
		await _sut.RollbackAsync(CancellationToken.None);

		// Assert
		_sut.ActiveTransactionCount.ShouldBe(0);
	}

	[Fact]
	public async Task RollbackAsync_ShouldThrow_WhenNoActiveTransaction()
	{
		await Should.ThrowAsync<DistributedTransactionException>(
			() => _sut.RollbackAsync(CancellationToken.None));
	}

	[Fact]
	public async Task RollbackAsync_ShouldContinue_WhenParticipantRollbackFails()
	{
		// Arrange
		await _sut.BeginAsync(CancellationToken.None);
		var failingP = CreateParticipant("p1");
		A.CallTo(() => failingP.RollbackAsync(A<CancellationToken>._))
			.Throws(new InvalidOperationException("Rollback failed"));
		var goodP = CreateParticipant("p2");
		await _sut.EnlistAsync(failingP, CancellationToken.None);
		await _sut.EnlistAsync(goodP, CancellationToken.None);

		// Act — should not throw even if one participant fails rollback
		await Should.NotThrowAsync(
			() => _sut.RollbackAsync(CancellationToken.None));

		// Assert — good participant should still have been rolled back
		A.CallTo(() => goodP.RollbackAsync(A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region DisposeAsync Tests

	[Fact]
	public async Task DisposeAsync_ShouldClearTransactions()
	{
		// Arrange
		await _sut.BeginAsync(CancellationToken.None);
		_sut.ActiveTransactionCount.ShouldBe(1);

		// Act
		await _sut.DisposeAsync();

		// Assert
		_sut.ActiveTransactionCount.ShouldBe(0);
	}

	[Fact]
	public async Task DisposeAsync_ShouldBeIdempotent()
	{
		await _sut.DisposeAsync();
		await _sut.DisposeAsync(); // second call should not throw
	}

	[Fact]
	public async Task BeginAsync_ShouldThrow_AfterDispose()
	{
		await _sut.DisposeAsync();

		await Should.ThrowAsync<ObjectDisposedException>(
			() => _sut.BeginAsync(CancellationToken.None));
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ShouldImplementIDistributedTransactionCoordinator()
	{
		_sut.ShouldBeAssignableTo<IDistributedTransactionCoordinator>();
	}

	[Fact]
	public void ShouldImplementIAsyncDisposable()
	{
		_sut.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	#endregion

	#region Helpers

#pragma warning disable CA2012 // FakeItEasy Returns() with ValueTask
	private static ITransactionParticipant CreateParticipant(
		string participantId,
		bool preparesSuccessfully = true)
	{
		var participant = A.Fake<ITransactionParticipant>();
		A.CallTo(() => participant.ParticipantId).Returns(participantId);
		A.CallTo(() => participant.PrepareAsync(A<CancellationToken>._))
			.Returns(Task.FromResult(preparesSuccessfully));
		A.CallTo(() => participant.CommitAsync(A<CancellationToken>._))
			.Returns(Task.CompletedTask);
		A.CallTo(() => participant.RollbackAsync(A<CancellationToken>._))
			.Returns(Task.CompletedTask);
		return participant;
	}
#pragma warning restore CA2012

	#endregion
}
