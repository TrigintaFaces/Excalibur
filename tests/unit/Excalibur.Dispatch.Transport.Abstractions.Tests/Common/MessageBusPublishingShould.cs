using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Common;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class MessageBusPublishingShould
{
	[Fact]
	public async Task PublishActionAsync_UsesCustomRoutingKey_WhenProvidedInContextItems()
	{
		var action = new TestAction();
		var context = new MessageEnvelope();
		context.Items["RoutingKey"] = "custom.action";
		var serializer = A.Fake<IPayloadSerializer>();
		var expectedBody = new byte[] { 1, 2, 3 };
		A.CallTo(() => serializer.SerializeObject(action, action.GetType())).Returns(expectedBody);

		string? routingKey = null;
		byte[]? body = null;
		IMessageContext? callbackContext = null;

		await MessageBusPublishing.PublishActionAsync(
			action,
			context,
			(key, payload, ctx, _) =>
			{
				routingKey = key;
				body = payload;
				callbackContext = ctx;
				return Task.CompletedTask;
			},
			serializer,
			CancellationToken.None);

		routingKey.ShouldBe("custom.action");
		body.ShouldBe(expectedBody);
		callbackContext.ShouldBeSameAs(context);
	}

	[Fact]
	public async Task PublishEventAsync_FallsBackToTypeNameRoutingKey_WhenNotProvided()
	{
		var evt = new TestEvent();
		var context = new MessageEnvelope();
		var serializer = A.Fake<IPayloadSerializer>();
		A.CallTo(() => serializer.SerializeObject(evt, evt.GetType())).Returns([9, 8, 7]);

		string? routingKey = null;
		await MessageBusPublishing.PublishEventAsync(
			evt,
			context,
			(key, _, _, _) =>
			{
				routingKey = key;
				return Task.CompletedTask;
			},
			serializer,
			CancellationToken.None);

		routingKey.ShouldBe(nameof(TestEvent));
	}

	[Fact]
	public async Task PublishDocumentAsync_UsesSerializerAndPublishCallback()
	{
		var doc = new TestDocument();
		var context = new MessageEnvelope();
		var serializer = A.Fake<IPayloadSerializer>();
		var expected = new byte[] { 4, 5, 6, 7 };
		A.CallTo(() => serializer.SerializeObject(doc, doc.GetType())).Returns(expected);

		byte[]? observedBody = null;
		await MessageBusPublishing.PublishDocumentAsync(
			doc,
			context,
			(_, payload, _, _) =>
			{
				observedBody = payload;
				return Task.CompletedTask;
			},
			serializer,
			CancellationToken.None);

		observedBody.ShouldBe(expected);
	}

	[Fact]
	public async Task PublishActionAsync_Throws_WhenArgumentsAreNull()
	{
		var context = new MessageEnvelope();
		var serializer = A.Fake<IPayloadSerializer>();
		Func<string, byte[], IMessageContext, CancellationToken, Task> publish = (_, _, _, _) => Task.CompletedTask;

		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			MessageBusPublishing.PublishActionAsync(null!, context, publish, serializer, CancellationToken.None));
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			MessageBusPublishing.PublishActionAsync(new TestAction(), null!, publish, serializer, CancellationToken.None));
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			MessageBusPublishing.PublishActionAsync(new TestAction(), context, publish, null!, CancellationToken.None));
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			MessageBusPublishing.PublishActionAsync(new TestAction(), context, null!, serializer, CancellationToken.None));
	}

	[Fact]
	public async Task PublishEventAsync_Throws_WhenArgumentsAreNull()
	{
		var context = new MessageEnvelope();
		var serializer = A.Fake<IPayloadSerializer>();
		Func<string, byte[], IMessageContext, CancellationToken, Task> publish = (_, _, _, _) => Task.CompletedTask;

		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			MessageBusPublishing.PublishEventAsync(null!, context, publish, serializer, CancellationToken.None));
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			MessageBusPublishing.PublishEventAsync(new TestEvent(), null!, publish, serializer, CancellationToken.None));
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			MessageBusPublishing.PublishEventAsync(new TestEvent(), context, publish, null!, CancellationToken.None));
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			MessageBusPublishing.PublishEventAsync(new TestEvent(), context, null!, serializer, CancellationToken.None));
	}

	[Fact]
	public async Task PublishDocumentAsync_Throws_WhenArgumentsAreNull()
	{
		var context = new MessageEnvelope();
		var serializer = A.Fake<IPayloadSerializer>();
		Func<string, byte[], IMessageContext, CancellationToken, Task> publish = (_, _, _, _) => Task.CompletedTask;

		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			MessageBusPublishing.PublishDocumentAsync(null!, context, publish, serializer, CancellationToken.None));
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			MessageBusPublishing.PublishDocumentAsync(new TestDocument(), null!, publish, serializer, CancellationToken.None));
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			MessageBusPublishing.PublishDocumentAsync(new TestDocument(), context, publish, null!, CancellationToken.None));
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			MessageBusPublishing.PublishDocumentAsync(new TestDocument(), context, null!, serializer, CancellationToken.None));
	}

	private sealed class TestAction : IDispatchAction;

	private sealed class TestEvent : IDispatchEvent;

	private sealed class TestDocument : IDispatchDocument;
}
