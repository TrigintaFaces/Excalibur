// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Transactions;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Middleware.Transaction;
using Excalibur.Dispatch.Middleware.Outbox;
using Excalibur.Dispatch.Options.Middleware;
using Excalibur.Dispatch.Serialization;
using Tests.Shared.TestFakes;

using Microsoft.Extensions.Logging.Abstractions;

using DispatchTransactionOptions = Excalibur.Dispatch.Options.Middleware.TransactionOptions;
using MessageResult = Excalibur.Dispatch.MessageResult;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for the <see cref="TransactionMiddleware"/> class.
/// </summary>
/// <remarks>
/// Sprint 554 - Task S554.40: TransactionMiddleware tests.
/// Tests transaction scope creation, commit on success, rollback on exception, and nested transaction handling.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Middleware)]
public sealed class TransactionMiddlewareShould
{
	private readonly ILogger<TransactionMiddleware> _logger;
	private readonly ITransactionService _transactionService;
	private readonly ITransaction _fakeTransaction;

	public TransactionMiddlewareShould()
	{
		_logger = NullLoggerFactory.Instance.CreateLogger<TransactionMiddleware>();
		_transactionService = A.Fake<ITransactionService>();
		_fakeTransaction = A.Fake<ITransaction>();

		_ = A.CallTo(() => _fakeTransaction.Id).Returns("txn-001");
		_ = A.CallTo(() => _transactionService.BeginTransactionAsync(A<object>._, A<CancellationToken>._))
			.Returns(Task.FromResult(_fakeTransaction));
	}

	private TransactionMiddleware CreateMiddleware(DispatchTransactionOptions options)
	{
		return new TransactionMiddleware(MsOptions.Create(options), _transactionService, _logger);
	}

	private static DispatchRequestDelegate CreateSuccessDelegate()
	{
		return (msg, ctx, ct) => new ValueTask<IMessageResult>(MessageResult.Success());
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new TransactionMiddleware(null!, _transactionService, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenTransactionServiceIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new DispatchTransactionOptions());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new TransactionMiddleware(options, null!, _logger));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new DispatchTransactionOptions());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new TransactionMiddleware(options, _transactionService, null!));
	}

	#endregion

	#region Stage and ApplicableMessageKinds Tests

	[Fact]
	public void HaveProcessingStage()
	{
		// Arrange
		var middleware = CreateMiddleware(new DispatchTransactionOptions());

		// Assert
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.Processing);
	}

	[Fact]
	public void HaveActionApplicableMessageKinds()
	{
		// Arrange
		var middleware = CreateMiddleware(new DispatchTransactionOptions());

		// Assert
		middleware.ApplicableMessageKinds.ShouldBe(MessageKinds.Action);
	}

	/// <summary>
	/// The Action-only boundary is deliberate, and this states it WHOLE: transactions do not span event
	/// handlers (safety), AND events still have an atomicity story via outbox staging (liveness).
	/// Without the liveness arm the suite reads as "transactions skip events" and a reader concludes
	/// events have no atomicity guarantee at all. Both arms read ApplicableMessageKinds -- the property
	/// the pipeline itself evaluates -- never reflection over [AppliesTo], so a change to the shipped
	/// behaviour cannot pass by leaving the attribute untouched. Widening the boundary must therefore be
	/// a deliberate, visible edit to this test, not a silent property change.
	/// </summary>
	[Fact]
	public void ExcludeEventsFromTransactions_WhileOutboxStagingStillCoversThem()
	{
		// Arrange
		var transaction = CreateMiddleware(new DispatchTransactionOptions());
		var outboxStaging = new OutboxStagingMiddleware(
			MsOptions.Create(new OutboxStagingOptions { Enabled = false }),
			outboxStore: null,
			new DispatchJsonSerializer(),
			NullLogger<OutboxStagingMiddleware>.Instance);

		// Assert -- safety: an Event never enrols in the producer's transaction.
		transaction.ApplicableMessageKinds.HasFlag(MessageKinds.Event).ShouldBeFalse(
			"TransactionMiddleware must not apply to Events: enrolling event handlers in the producer's " +
			"transaction would extend it across handler execution and let a subscriber's failure roll back " +
			"the producer's command.");

		transaction.ApplicableMessageKinds.HasFlag(MessageKinds.Action).ShouldBeTrue(
			"TransactionMiddleware must still apply to Actions -- that is the scope the boundary keeps.");

		// Assert -- liveness: the atomicity an Event does need is provided, by outbox staging.
		outboxStaging.ApplicableMessageKinds.HasFlag(MessageKinds.Event).ShouldBeTrue(
			"OutboxStagingMiddleware must apply to Events: an Event becomes durable if and only if the " +
			"producer's state change committed, because it is staged inside that transaction and delivered " +
			"after commit. This is what makes the Action-only transaction boundary correct rather than a gap.");
	}

	#endregion

	#region InvokeAsync Parameter Validation Tests

	[Fact]
	public async Task ThrowArgumentNullException_WhenMessageIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware(new DispatchTransactionOptions());
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(null!, context, CreateSuccessDelegate(), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware(new DispatchTransactionOptions());
		var message = new FakeActionMessage();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(message, null!, CreateSuccessDelegate(), CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenNextDelegateIsNull()
	{
		// Arrange
		var middleware = CreateMiddleware(new DispatchTransactionOptions());
		var message = new FakeActionMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			middleware.InvokeAsync(message, context, null!, CancellationToken.None).AsTask());
	}

	#endregion

	#region Disabled Middleware Tests

	[Fact]
	public async Task PassThroughDirectly_WhenDisabled()
	{
		// Arrange
		var middleware = CreateMiddleware(new DispatchTransactionOptions { Enabled = false });
		var message = new FakeActionMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		var nextCalled = false;

		DispatchRequestDelegate next = (msg, ctx, ct) =>
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		};

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
		result.Succeeded.ShouldBeTrue();
		A.CallTo(() => _transactionService.BeginTransactionAsync(A<object>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	#endregion

	#region Transaction Scope Creation Tests

	[Fact]
	public async Task BeginTransaction_WhenProcessingActionMessage()
	{
		// Arrange
		var middleware = CreateMiddleware(new DispatchTransactionOptions
		{
			Enabled = true,
			RequireTransactionByDefault = true,
		});
		var message = new FakeActionMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act
		_ = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		A.CallTo(() => _transactionService.BeginTransactionAsync(A<object>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SetTransactionInContext_WhenTransactionCreated()
	{
		// Arrange
		var middleware = CreateMiddleware(new DispatchTransactionOptions
		{
			Enabled = true,
			RequireTransactionByDefault = true,
		});
		var message = new FakeActionMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		ITransaction? capturedTransaction = null;

		DispatchRequestDelegate next = (msg, ctx, ct) =>
		{
			capturedTransaction = ctx.GetItem<ITransaction>("Transaction");
			return new ValueTask<IMessageResult>(MessageResult.Success());
		};

		// Act
		_ = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		capturedTransaction.ShouldNotBeNull();
		context.GetItem<string>("TransactionId").ShouldBe("txn-001");
	}

	#endregion

	#region Commit on Success Tests

	[Fact]
	public async Task CommitTransaction_WhenNextDelegateSucceeds()
	{
		// Arrange
		var middleware = CreateMiddleware(new DispatchTransactionOptions
		{
			Enabled = true,
			RequireTransactionByDefault = true,
		});
		var message = new FakeActionMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act
		var result = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		A.CallTo(() => _fakeTransaction.CommitAsync(A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _fakeTransaction.RollbackAsync(A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	#endregion

	#region Rollback on Failure Tests

	[Fact]
	public async Task RollbackTransaction_WhenNextDelegateReturnsFailedResult()
	{
		// Arrange
		var middleware = CreateMiddleware(new DispatchTransactionOptions
		{
			Enabled = true,
			RequireTransactionByDefault = true,
		});
		var message = new FakeActionMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		DispatchRequestDelegate next = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(MessageResult.Failed("processing error"));

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeFalse();
		A.CallTo(() => _fakeTransaction.RollbackAsync(A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _fakeTransaction.CommitAsync(A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	#endregion

	#region Rollback on Exception Tests

	[Fact]
	public async Task RollbackTransaction_WhenNextDelegateThrowsException()
	{
		// Arrange
		var middleware = CreateMiddleware(new DispatchTransactionOptions
		{
			Enabled = true,
			RequireTransactionByDefault = true,
		});
		var message = new FakeActionMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		DispatchRequestDelegate next = (msg, ctx, ct) =>
			throw new InvalidOperationException("handler exploded");

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			middleware.InvokeAsync(message, context, next, CancellationToken.None).AsTask());

		A.CallTo(() => _fakeTransaction.RollbackAsync(A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _fakeTransaction.CommitAsync(A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task RethrowOriginalException_EvenIfRollbackFails()
	{
		// Arrange
		var middleware = CreateMiddleware(new DispatchTransactionOptions
		{
			Enabled = true,
			RequireTransactionByDefault = true,
		});
		var message = new FakeActionMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		A.CallTo(() => _fakeTransaction.RollbackAsync(A<CancellationToken>._))
			.Throws(new InvalidOperationException("rollback failed"));

		DispatchRequestDelegate next = (msg, ctx, ct) =>
			throw new InvalidOperationException("original error");

		// Act & Assert - Original exception should be rethrown even if rollback fails
		_ = await Should.ThrowAsync<InvalidOperationException>(
			middleware.InvokeAsync(message, context, next, CancellationToken.None).AsTask());
	}

	#endregion

	#region Bypass Transaction Tests

	[Fact]
	public async Task SkipTransaction_WhenMessageTypeIsInBypassList()
	{
		// Arrange
		var middleware = CreateMiddleware(new DispatchTransactionOptions
		{
			Enabled = true,
			RequireTransactionByDefault = true,
			BypassTransactionForTypes = ["FakeActionMessage"],
		});
		var message = new FakeActionMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };
		var nextCalled = false;

		DispatchRequestDelegate next = (msg, ctx, ct) =>
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		};

		// Act
		_ = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
		A.CallTo(() => _transactionService.BeginTransactionAsync(A<object>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task SkipTransaction_WhenMessageHasNoTransactionAttribute()
	{
		// Arrange
		var middleware = CreateMiddleware(new DispatchTransactionOptions
		{
			Enabled = true,
			RequireTransactionByDefault = false, // Default is not to require transactions
		});
		var message = new FakeActionMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act
		_ = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert - No transaction should be created when default is false and no attribute
		A.CallTo(() => _transactionService.BeginTransactionAsync(A<object>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task SkipTransaction_WhenMessageHasNoTransactionAttributeDecoration()
	{
		// Arrange
		var middleware = CreateMiddleware(new DispatchTransactionOptions
		{
			Enabled = true,
			RequireTransactionByDefault = true,
		});
		var message = new NoTransactionMessage();
		var context = new FakeMessageContext { MessageId = "test-msg-1" };

		// Act
		_ = await middleware.InvokeAsync(message, context, CreateSuccessDelegate(), CancellationToken.None);

		// Assert
		A.CallTo(() => _transactionService.BeginTransactionAsync(A<object>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	#endregion

	#region Default Options Tests

	[Fact]
	public void HaveCorrectDefaultOptionValues()
	{
		// Arrange
		var options = new DispatchTransactionOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.RequireTransactionByDefault.ShouldBeTrue();
		options.EnableDistributedTransactions.ShouldBeFalse();
		options.DefaultIsolationLevel.ShouldBe(IsolationLevel.ReadCommitted);
		options.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.BypassTransactionForTypes.ShouldBeNull();
	}

	#endregion

	#region Test Message Types

	/// <summary>
	/// Test message implementing IDispatchAction for transaction tests.
	/// </summary>
	private sealed class FakeActionMessage : IDispatchAction;

	/// <summary>
	/// Test message with [NoTransaction] attribute.
	/// </summary>
	[NoTransaction]
	private sealed class NoTransactionMessage : IDispatchAction;

	/// <summary>
	/// Test message with [RequireTransaction] attribute.
	/// </summary>
	[RequireTransaction]
	private sealed class RequireTransactionMessage : IDispatchAction;

	#endregion
}
