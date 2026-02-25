using System.Text;

using Amazon.EventBridge;
using Amazon.EventBridge.Model;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Transport.Aws;

using FakeItEasy;

using Microsoft.Extensions.Logging;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs;

/// <summary>
/// Unit tests for <see cref="AwsEventBridgeMessageBus" />.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class AwsEventBridgeMessageBusShould : UnitTestBase
{
	[Fact]
	public async Task PublishAsync_UsesDefaultsAndCreatesArchive()
	{
		// Arrange
		var eventBridgeClient = A.Fake<IAmazonEventBridge>();
		var serializer = A.Fake<IPayloadSerializer>();
		PutEventsRequest? capturedPutRequest = null;
		CreateArchiveRequest? capturedArchiveRequest = null;

		_ = A.CallTo(() => serializer.SerializeObject(A<object>._, A<Type>._))
				.Returns(Encoding.UTF8.GetBytes("payload"));

		_ = A.CallTo(() => eventBridgeClient.DescribeArchiveAsync(A<DescribeArchiveRequest>._, A<CancellationToken>._))
				.ThrowsAsync(new ResourceNotFoundException("missing"));

		_ = A.CallTo(() => eventBridgeClient.DescribeEventBusAsync(A<DescribeEventBusRequest>._, A<CancellationToken>._))
				.Returns(new DescribeEventBusResponse { Arn = "arn:aws:events:us-east-1:123456789:event-bus/dispatch" });

		_ = A.CallTo(() => eventBridgeClient.CreateArchiveAsync(A<CreateArchiveRequest>._, A<CancellationToken>._))
				.Invokes((CreateArchiveRequest request, CancellationToken _) => capturedArchiveRequest = request)
				.Returns(new CreateArchiveResponse());

		_ = A.CallTo(() => eventBridgeClient.PutEventsAsync(A<PutEventsRequest>._, A<CancellationToken>._))
				.Invokes((PutEventsRequest request, CancellationToken _) => capturedPutRequest = request)
				.Returns(new PutEventsResponse { Entries = [] });

		var options = new AwsEventBridgeOptions
		{
			EventBusName = "dispatch",
			DefaultSource = "dispatch-default",
			DefaultDetailType = "dispatch.detail",
			EnableArchiving = true,
			ArchiveName = "dispatch-archive",
			ArchiveRetentionDays = 10,
		};

		var bus = new AwsEventBridgeMessageBus(
				eventBridgeClient,
				serializer,
				options,
				A.Fake<ILogger<AwsEventBridgeMessageBus>>());

		var context = new MessageContext
		{
			Source = null,
			MessageType = null,
		};

		// Act
		await bus.PublishAsync(new TestAction(), context, CancellationToken.None);

		// Assert
		_ = capturedPutRequest.ShouldNotBeNull();
		_ = capturedPutRequest.Entries.ShouldNotBeNull();
		capturedPutRequest.Entries.Count.ShouldBe(1);
		capturedPutRequest.Entries[0].Source.ShouldBe("dispatch-default");
		capturedPutRequest.Entries[0].DetailType.ShouldBe("dispatch.detail");

		_ = capturedArchiveRequest.ShouldNotBeNull();
		capturedArchiveRequest.ArchiveName.ShouldBe("dispatch-archive");
		capturedArchiveRequest.EventPattern.ShouldBe("{}");
		capturedArchiveRequest.RetentionDays.ShouldBe(10);
		capturedArchiveRequest.EventSourceArn.ShouldBe("arn:aws:events:us-east-1:123456789:event-bus/dispatch");
	}

	private sealed class TestAction : IDispatchAction
	{
	}
}
