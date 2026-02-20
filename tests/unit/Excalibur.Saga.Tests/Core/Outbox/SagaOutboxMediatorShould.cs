// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Saga.Outbox;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Saga.Tests.Core.Outbox;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SagaOutboxMediatorShould
{
	[Fact]
	public async Task PublishThroughOutboxAsync_InvokeDelegate()
	{
		// Arrange
		var invoked = false;
		string? capturedSagaId = null;
		IReadOnlyList<IDomainEvent>? capturedEvents = null;

		var options = new SagaOutboxOptions
		{
			PublishDelegate = (events, sagaId, ct) =>
			{
				invoked = true;
				capturedSagaId = sagaId;
				capturedEvents = events;
				return Task.CompletedTask;
			}
		};

		var sut = CreateMediator(options);
		var domainEvent = A.Fake<IDomainEvent>();

		// Act
		await sut.PublishThroughOutboxAsync([domainEvent], "saga-1", CancellationToken.None);

		// Assert
		invoked.ShouldBeTrue();
		capturedSagaId.ShouldBe("saga-1");
		capturedEvents.ShouldNotBeNull();
		capturedEvents.Count.ShouldBe(1);
	}

	[Fact]
	public async Task PublishThroughOutboxAsync_SkipEmpty()
	{
		// Arrange
		var invoked = false;
		var options = new SagaOutboxOptions
		{
			PublishDelegate = (_, _, _) =>
			{
				invoked = true;
				return Task.CompletedTask;
			}
		};

		var sut = CreateMediator(options);

		// Act
		await sut.PublishThroughOutboxAsync([], "saga-1", CancellationToken.None);

		// Assert
		invoked.ShouldBeFalse();
	}

	[Fact]
	public async Task PublishThroughOutboxAsync_ThrowWhenDelegateNotConfigured()
	{
		// Arrange
		var options = new SagaOutboxOptions { PublishDelegate = null };
		var sut = CreateMediator(options);
		var domainEvent = A.Fake<IDomainEvent>();

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => sut.PublishThroughOutboxAsync([domainEvent], "saga-1", CancellationToken.None));
	}

	[Fact]
	public async Task PublishThroughOutboxAsync_ThrowOnNullEvents()
	{
		var options = new SagaOutboxOptions { PublishDelegate = (_, _, _) => Task.CompletedTask };
		var sut = CreateMediator(options);

		await Should.ThrowAsync<ArgumentNullException>(
			() => sut.PublishThroughOutboxAsync(null!, "saga-1", CancellationToken.None));
	}

	[Fact]
	public async Task PublishThroughOutboxAsync_ThrowOnNullOrEmptySagaId()
	{
		var options = new SagaOutboxOptions { PublishDelegate = (_, _, _) => Task.CompletedTask };
		var sut = CreateMediator(options);
		var domainEvent = A.Fake<IDomainEvent>();

		await Should.ThrowAsync<ArgumentException>(
			() => sut.PublishThroughOutboxAsync([domainEvent], null!, CancellationToken.None));
		await Should.ThrowAsync<ArgumentException>(
			() => sut.PublishThroughOutboxAsync([domainEvent], "", CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullConstructorArgs()
	{
		var logger = NullLogger<SagaOutboxMediator>.Instance;
		var opts = Microsoft.Extensions.Options.Options.Create(new SagaOutboxOptions());

		Should.Throw<ArgumentNullException>(() => new SagaOutboxMediator(null!, opts));
		Should.Throw<ArgumentNullException>(() => new SagaOutboxMediator(logger, null!));
	}

	private static SagaOutboxMediator CreateMediator(SagaOutboxOptions options) =>
		new(
			NullLogger<SagaOutboxMediator>.Instance,
			Microsoft.Extensions.Options.Options.Create(options));
}
