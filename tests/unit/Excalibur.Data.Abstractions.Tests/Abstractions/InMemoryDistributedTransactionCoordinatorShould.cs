// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Transactions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.Abstractions;

[Trait("Category", "Unit")]
[Trait("Component", "Data.Abstractions")]
public sealed class InMemoryDistributedTransactionCoordinatorShould
{
	private readonly IOptions<DistributedTransactionOptions> _options;

	public InMemoryDistributedTransactionCoordinatorShould()
	{
		_options = Options.Create(new DistributedTransactionOptions
		{
			Timeout = TimeSpan.FromSeconds(30),
			MaxParticipants = 10,
			AutoRollbackOnPrepareFailure = true,
		});
	}

	[Fact]
	public async Task BeginTransactionAndReturnId()
	{
		// Arrange
		await using var sut = CreateSut();

		// Act
		var transactionId = await sut.BeginAsync(CancellationToken.None);

		// Assert
		transactionId.ShouldNotBeNullOrWhiteSpace();
		sut.ActiveTransactionCount.ShouldBe(1);
	}

	[Fact]
	public async Task EnlistParticipantSuccessfully()
	{
		// Arrange
		await using var sut = CreateSut();
		await sut.BeginAsync(CancellationToken.None);
		var participant = A.Fake<ITransactionParticipant>();
		A.CallTo(() => participant.ParticipantId).Returns("participant-1");

		// Act & Assert - should not throw
		await Should.NotThrowAsync(() => sut.EnlistAsync(participant, CancellationToken.None));
	}

	[Fact]
	public async Task CommitWithNoParticipants()
	{
		// Arrange
		await using var sut = CreateSut();
		await sut.BeginAsync(CancellationToken.None);

		// Act & Assert - should not throw
		await Should.NotThrowAsync(() => sut.CommitAsync(CancellationToken.None));
		sut.ActiveTransactionCount.ShouldBe(0);
	}

	[Fact]
	public async Task CommitWithSuccessfulParticipants()
	{
		// Arrange
		await using var sut = CreateSut();
		await sut.BeginAsync(CancellationToken.None);

		var participant = A.Fake<ITransactionParticipant>();
		A.CallTo(() => participant.ParticipantId).Returns("p1");
		A.CallTo(() => participant.PrepareAsync(A<CancellationToken>._)).Returns(true);
		await sut.EnlistAsync(participant, CancellationToken.None);

		// Act
		await sut.CommitAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => participant.PrepareAsync(A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => participant.CommitAsync(A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RollbackWhenParticipantVotesNo()
	{
		// Arrange
		await using var sut = CreateSut();
		await sut.BeginAsync(CancellationToken.None);

		var participant = A.Fake<ITransactionParticipant>();
		A.CallTo(() => participant.ParticipantId).Returns("p1");
		A.CallTo(() => participant.PrepareAsync(A<CancellationToken>._)).Returns(false);
		await sut.EnlistAsync(participant, CancellationToken.None);

		// Act & Assert
		var ex = await Should.ThrowAsync<DistributedTransactionException>(
			() => sut.CommitAsync(CancellationToken.None));
		ex.Message.ShouldContain("aborted");

		// Participant should have been rolled back due to AutoRollbackOnPrepareFailure
		A.CallTo(() => participant.RollbackAsync(A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RollbackAllParticipants()
	{
		// Arrange
		await using var sut = CreateSut();
		await sut.BeginAsync(CancellationToken.None);

		var p1 = A.Fake<ITransactionParticipant>();
		A.CallTo(() => p1.ParticipantId).Returns("p1");
		var p2 = A.Fake<ITransactionParticipant>();
		A.CallTo(() => p2.ParticipantId).Returns("p2");

		await sut.EnlistAsync(p1, CancellationToken.None);
		await sut.EnlistAsync(p2, CancellationToken.None);

		// Act
		await sut.RollbackAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => p1.RollbackAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => p2.RollbackAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowWhenNoActiveTransaction()
	{
		// Arrange
		await using var sut = CreateSut();

		// Act & Assert
		await Should.ThrowAsync<DistributedTransactionException>(
			() => sut.CommitAsync(CancellationToken.None));
	}

	[Fact]
	public async Task ThrowAfterDisposal()
	{
		// Arrange
		var sut = CreateSut();
		await sut.DisposeAsync();

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(
			() => sut.BeginAsync(CancellationToken.None));
	}

	[Fact]
	public async Task HandleDoubleDispose()
	{
		// Arrange
		var sut = CreateSut();

		// Act & Assert - should not throw
		await sut.DisposeAsync();
		await sut.DisposeAsync();
	}

	[Fact]
	public async Task ThrowWhenEnlistingNullParticipant()
	{
		// Arrange
		await using var sut = CreateSut();
		await sut.BeginAsync(CancellationToken.None);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => sut.EnlistAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenMaxParticipantsExceeded()
	{
		// Arrange
		var limitedOptions = Options.Create(new DistributedTransactionOptions
		{
			Timeout = TimeSpan.FromSeconds(30),
			MaxParticipants = 1,
		});
		await using var sut = new InMemoryDistributedTransactionCoordinator(
			limitedOptions,
			NullLogger<InMemoryDistributedTransactionCoordinator>.Instance);

		await sut.BeginAsync(CancellationToken.None);

		var p1 = A.Fake<ITransactionParticipant>();
		A.CallTo(() => p1.ParticipantId).Returns("p1");
		await sut.EnlistAsync(p1, CancellationToken.None);

		var p2 = A.Fake<ITransactionParticipant>();
		A.CallTo(() => p2.ParticipantId).Returns("p2");

		// Act & Assert
		await Should.ThrowAsync<DistributedTransactionException>(
			() => sut.EnlistAsync(p2, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenRollbackingCommittedTransaction()
	{
		// Arrange
		await using var sut = CreateSut();
		await sut.BeginAsync(CancellationToken.None);
		await sut.CommitAsync(CancellationToken.None);

		// Start a new transaction for the rollback to target
		// but since committed is removed, this would throw "no active transaction"
		await Should.ThrowAsync<DistributedTransactionException>(
			() => sut.RollbackAsync(CancellationToken.None));
	}

	private InMemoryDistributedTransactionCoordinator CreateSut() =>
		new(_options, NullLogger<InMemoryDistributedTransactionCoordinator>.Instance);
}
