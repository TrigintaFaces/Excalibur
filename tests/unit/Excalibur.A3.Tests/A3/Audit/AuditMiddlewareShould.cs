#pragma warning disable CA2012 // Use ValueTasks correctly - FakeItEasy needs stored ValueTask

using Excalibur.A3.Audit;
using Excalibur.A3.Audit.Events;
using Excalibur.Application.Requests;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Domain;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Tests.A3.Audit;

[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class AuditMiddlewareShould
{
	private readonly IActivityContext _activityContext;
	private readonly IAuditMessagePublisher _auditPublisher;
	private readonly IOutboxDispatcher _outbox;
	private readonly AuditMiddleware _sut;

	public AuditMiddlewareShould()
	{
		_activityContext = A.Fake<IActivityContext>();
		_auditPublisher = A.Fake<IAuditMessagePublisher>();
		_outbox = A.Fake<IOutboxDispatcher>();
		var logger = NullLogger<AuditMiddleware>.Instance;

		_sut = new AuditMiddleware(_activityContext, _auditPublisher, logger, _outbox);
	}

	[Fact]
	public void Implement_IDispatchMiddleware()
	{
		// Assert
		_sut.ShouldBeAssignableTo<IDispatchMiddleware>();
	}

	[Fact]
	public void Have_end_stage()
	{
		// Assert
		_sut.Stage.ShouldBe(DispatchMiddlewareStage.End);
	}

	[Fact]
	public async Task Throw_when_message_is_null()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate nextDelegate = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			async () => await _sut.InvokeAsync(null!, context, nextDelegate, CancellationToken.None));
	}

	[Fact]
	public async Task Throw_when_next_delegate_is_null()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			async () => await _sut.InvokeAsync(message, context, null!, CancellationToken.None));
	}

	[Fact]
	public async Task Skip_audit_when_message_is_not_auditable()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();
		DispatchRequestDelegate nextDelegate = (_, _, _) => ValueTask.FromResult(expectedResult);

		// Act
		var result = await _sut.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		A.CallTo(() => _auditPublisher.PublishAsync(
			A<ActivityAudited>.Ignored,
			A<IActivityContext>.Ignored,
			A<CancellationToken>.Ignored))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task Publish_audit_event_when_message_is_auditable()
	{
		// Arrange
		var message = new TestAuditableMessage();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();
		A.CallTo(() => expectedResult.Succeeded).Returns(true);
		DispatchRequestDelegate nextDelegate = (_, _, _) => ValueTask.FromResult(expectedResult);

		// Act
		var result = await _sut.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		A.CallTo(() => _auditPublisher.PublishAsync(
			A<ActivityAudited>.Ignored,
			_activityContext,
			A<CancellationToken>.Ignored))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Save_to_outbox_when_publish_fails()
	{
		// Arrange
		var message = new TestAuditableMessage();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();
		A.CallTo(() => expectedResult.Succeeded).Returns(true);
		DispatchRequestDelegate nextDelegate = (_, _, _) => ValueTask.FromResult(expectedResult);

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
		var result = await _sut.InvokeAsync(message, context, nextDelegate, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		A.CallTo(() => _outbox.SaveMessagesAsync(
			A<ICollection<IOutboxMessage>>.Ignored,
			A<CancellationToken>.Ignored))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Rethrow_exception_from_next_delegate()
	{
		// Arrange
		var message = new TestAuditableMessage();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate nextDelegate = (_, _, _) => throw new InvalidOperationException("Handler failed");

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			async () => await _sut.InvokeAsync(message, context, nextDelegate, CancellationToken.None));

		// Audit should still be published even on exception (in finally block)
		A.CallTo(() => _auditPublisher.PublishAsync(
			A<ActivityAudited>.Ignored,
			A<IActivityContext>.Ignored,
			A<CancellationToken>.Ignored))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Rethrow_api_exception_from_next_delegate()
	{
		// Arrange
		var message = new TestAuditableMessage();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate nextDelegate = (_, _, _) =>
			throw new ApiException(404, "Not found", null);

		// Act & Assert
		await Should.ThrowAsync<ApiException>(
			async () => await _sut.InvokeAsync(message, context, nextDelegate, CancellationToken.None));

		// Audit should still be published
		A.CallTo(() => _auditPublisher.PublishAsync(
			A<ActivityAudited>.Ignored,
			A<IActivityContext>.Ignored,
			A<CancellationToken>.Ignored))
			.MustHaveHappenedOnceExactly();
	}

	private sealed class TestAuditableMessage : IDispatchMessage, IAmAuditable
	{
		public string MessageId { get; init; } = Guid.NewGuid().ToString();
		public Guid Id { get; init; } = Guid.NewGuid();
		public MessageKinds Kind => MessageKinds.Action;
		public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
		public IReadOnlyDictionary<string, object> Headers { get; init; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().Name;
		public IMessageFeatures Features { get; init; } = new DefaultMessageFeatures();
	}
}
