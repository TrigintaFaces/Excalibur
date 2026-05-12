#pragma warning disable CA2012 // Use ValueTasks correctly - FakeItEasy needs stored ValueTask

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Audit;
using Excalibur.A3.Audit.Events;
using Excalibur.Application.Requests;
using Excalibur.Application.Requests.Commands;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Domain;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Tests.A3.Audit;

/// <summary>
/// Integration tests verifying that CQRS commands implementing <see cref="ICommand{TResult}"/>
/// combined with <see cref="IAmAuditable"/> flow correctly through <see cref="AuditMiddleware"/>.
/// This validates the CQRS uplift pattern: ICommand{T} + IAmAuditable → AuditMiddleware pipeline.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class CqrsAuditIntegrationShould
{
	private readonly IActivityContext _activityContext;
	private readonly IAuditMessagePublisher _auditPublisher;
	private readonly IOutboxDispatcher _outbox;
	private readonly AuditMiddleware _sut;

	public CqrsAuditIntegrationShould()
	{
		_activityContext = A.Fake<IActivityContext>();
		_auditPublisher = A.Fake<IAuditMessagePublisher>();
		_outbox = A.Fake<IOutboxDispatcher>();
		_sut = new AuditMiddleware(_activityContext, _auditPublisher, NullLogger<AuditMiddleware>.Instance, _outbox);
	}

	// ========================================
	// CQRS Command + Auditable — Happy Path
	// ========================================

	[Fact]
	public async Task PublishAuditEvent_ForAuditableCommand()
	{
		// Arrange — ICommand<string> + IAmAuditable
		var command = new CreateOrderCommand { OrderName = "Test Order" };
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();
		A.CallTo(() => expectedResult.Succeeded).Returns(true);
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(expectedResult);

		// Act
		var result = await _sut.InvokeAsync(command, context, next, CancellationToken.None);

		// Assert — audit published for the CQRS command
		result.ShouldBe(expectedResult);
		A.CallTo(() => _auditPublisher.PublishAsync(
			A<ActivityAudited>.Ignored,
			_activityContext,
			A<CancellationToken>.Ignored))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SkipAudit_ForNonAuditableCommand()
	{
		// Arrange — ICommand<string> without IAmAuditable
		var command = new NonAuditableCommand();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(expectedResult);

		// Act
		var result = await _sut.InvokeAsync(command, context, next, CancellationToken.None);

		// Assert — no audit for non-auditable commands
		result.ShouldBe(expectedResult);
		A.CallTo(() => _auditPublisher.PublishAsync(
			A<ActivityAudited>.Ignored,
			A<IActivityContext>.Ignored,
			A<CancellationToken>.Ignored))
			.MustNotHaveHappened();
	}

	// ========================================
	// Failure Paths
	// ========================================

	[Fact]
	public async Task PublishAuditEvent_EvenWhenHandlerThrows()
	{
		// Arrange
		var command = new CreateOrderCommand { OrderName = "Failing Order" };
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) =>
			throw new InvalidOperationException("Handler failed");

		// Act & Assert — exception propagates
		await Should.ThrowAsync<InvalidOperationException>(
			async () => await _sut.InvokeAsync(command, context, next, CancellationToken.None));

		// Audit still published (finally block)
		A.CallTo(() => _auditPublisher.PublishAsync(
			A<ActivityAudited>.Ignored,
			_activityContext,
			A<CancellationToken>.Ignored))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task FallbackToOutbox_WhenPublishFails_ForAuditableCommand()
	{
		// Arrange
		var command = new CreateOrderCommand { OrderName = "Outbox Fallback" };
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();
		A.CallTo(() => expectedResult.Succeeded).Returns(true);
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(expectedResult);

		A.CallTo(() => _auditPublisher.PublishAsync(
			A<ActivityAudited>.Ignored,
			A<IActivityContext>.Ignored,
			A<CancellationToken>.Ignored))
			.Throws(new InvalidOperationException("Publish failed"));

		A.CallTo(() => _outbox.SaveMessagesAsync(
			A<ICollection<IOutboxMessage>>.Ignored,
			A<CancellationToken>.Ignored))
			.Returns(1);

		// Act
		var result = await _sut.InvokeAsync(command, context, next, CancellationToken.None);

		// Assert — falls back to outbox
		result.ShouldBe(expectedResult);
		A.CallTo(() => _outbox.SaveMessagesAsync(
			A<ICollection<IOutboxMessage>>.Ignored,
			A<CancellationToken>.Ignored))
			.MustHaveHappenedOnceExactly();
	}

	// ========================================
	// Edge Cases — Void Command + Cancellation
	// ========================================

	[Fact]
	public async Task PublishAuditEvent_ForVoidAuditableCommand()
	{
		// Arrange — ICommand (void, no TResult) + IAmAuditable
		var command = new DeleteOrderCommand { OrderId = Guid.NewGuid() };
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();
		A.CallTo(() => expectedResult.Succeeded).Returns(true);
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(expectedResult);

		// Act
		var result = await _sut.InvokeAsync(command, context, next, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		A.CallTo(() => _auditPublisher.PublishAsync(
			A<ActivityAudited>.Ignored,
			_activityContext,
			A<CancellationToken>.Ignored))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PassNextDelegateResult_ForFailedAuditableCommand()
	{
		// Arrange
		var command = new CreateOrderCommand { OrderName = "Failed" };
		var context = A.Fake<IMessageContext>();
		var failedResult = A.Fake<IMessageResult>();
		A.CallTo(() => failedResult.Succeeded).Returns(false);
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(failedResult);

		// Act
		var result = await _sut.InvokeAsync(command, context, next, CancellationToken.None);

		// Assert — result passes through even on failure
		result.ShouldBe(failedResult);
		// Audit still published for failed operations (audit captures success or failure)
		A.CallTo(() => _auditPublisher.PublishAsync(
			A<ActivityAudited>.Ignored,
			_activityContext,
			A<CancellationToken>.Ignored))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvokeNextDelegate_ForAuditableCommand()
	{
		// Arrange
		var command = new CreateOrderCommand { OrderName = "Delegate Check" };
		var context = A.Fake<IMessageContext>();
		var nextCalled = false;
		DispatchRequestDelegate next = (_, _, _) =>
		{
			nextCalled = true;
			var result = A.Fake<IMessageResult>();
			A.CallTo(() => result.Succeeded).Returns(true);
			return ValueTask.FromResult(result);
		};

		// Act
		_ = await _sut.InvokeAsync(command, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
	}

	// ========================================
	// Test Types — CQRS Commands
	// ========================================

	/// <summary>
	/// A CQRS command with a result that is also auditable.
	/// This represents the primary CQRS uplift pattern: ICommand{T} + IAmAuditable.
	/// </summary>
	private sealed class CreateOrderCommand : ICommand<string>, IAmAuditable
	{
		public string OrderName { get; init; } = string.Empty;

		// IDispatchMessage
		public string MessageId { get; init; } = Guid.NewGuid().ToString();
		public Guid Id { get; init; } = Guid.NewGuid();
		public MessageKinds Kind => MessageKinds.Action;
		public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
		public IReadOnlyDictionary<string, object> Headers { get; init; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => nameof(CreateOrderCommand);
		public IMessageFeatures Features { get; init; } = new DefaultMessageFeatures();

		// IActivity
		public ActivityType ActivityType => ActivityType.Command;
		public string ActivityName => nameof(CreateOrderCommand);
		public string ActivityDisplayName => "Create Order";
		public string ActivityDescription => "Creates a new order";
		public Guid CorrelationId { get; init; } = Guid.NewGuid();
		public string? TenantId { get; init; } = "test-tenant";
	}

	/// <summary>
	/// A CQRS void command that is also auditable (no TResult).
	/// </summary>
	private sealed class DeleteOrderCommand : ICommand, IAmAuditable
	{
		public Guid OrderId { get; init; }

		// IDispatchMessage
		public string MessageId { get; init; } = Guid.NewGuid().ToString();
		public Guid Id { get; init; } = Guid.NewGuid();
		public MessageKinds Kind => MessageKinds.Action;
		public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
		public IReadOnlyDictionary<string, object> Headers { get; init; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => nameof(DeleteOrderCommand);
		public IMessageFeatures Features { get; init; } = new DefaultMessageFeatures();

		// IActivity
		public ActivityType ActivityType => ActivityType.Command;
		public string ActivityName => nameof(DeleteOrderCommand);
		public string ActivityDisplayName => "Delete Order";
		public string ActivityDescription => "Deletes an order";
		public Guid CorrelationId { get; init; } = Guid.NewGuid();
		public string? TenantId { get; init; } = "test-tenant";
	}

	/// <summary>
	/// A CQRS command that is NOT auditable — should be skipped by AuditMiddleware.
	/// </summary>
	private sealed class NonAuditableCommand : ICommand<int>
	{
		// IDispatchMessage
		public string MessageId { get; init; } = Guid.NewGuid().ToString();
		public Guid Id { get; init; } = Guid.NewGuid();
		public MessageKinds Kind => MessageKinds.Action;
		public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
		public IReadOnlyDictionary<string, object> Headers { get; init; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => nameof(NonAuditableCommand);
		public IMessageFeatures Features { get; init; } = new DefaultMessageFeatures();

		// IActivity
		public ActivityType ActivityType => ActivityType.Command;
		public string ActivityName => nameof(NonAuditableCommand);
		public string ActivityDisplayName => "Non-auditable";
		public string ActivityDescription => "Should not be audited";
		public Guid CorrelationId { get; init; } = Guid.NewGuid();
		public string? TenantId { get; init; }
	}
}
