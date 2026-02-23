// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Tests.TestFakes;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class BulkContextShould
{
	[Fact]
	public void CopyPrimaryContextValuesOnConstruction()
	{
		// Arrange
		var primary = CreatePrimaryContext();

		// Act
		var sut = new BulkContext([primary]);

		// Assert
		sut.MessageId.ShouldBe(primary.MessageId);
		sut.ExternalId.ShouldBe(primary.ExternalId);
		sut.UserId.ShouldBe(primary.UserId);
		sut.CorrelationId.ShouldBe(primary.CorrelationId);
		sut.CausationId.ShouldBe(primary.CausationId);
		sut.TraceParent.ShouldBe(primary.TraceParent);
		sut.SerializerVersion.ShouldBe("serializer-v1");
		sut.MessageVersion.ShouldBe("message-v2");
		sut.ContractVersion.ShouldBe("contract-v3");
		sut.DesiredVersion.ShouldBe(7);
		sut.TenantId.ShouldBe(primary.TenantId);
		sut.Source.ShouldBe(primary.Source);
		sut.MessageType.ShouldBe(primary.MessageType);
		sut.ContentType.ShouldBe(primary.ContentType);
		sut.DeliveryCount.ShouldBe(primary.DeliveryCount);
		sut.PartitionKey.ShouldBe("partition-a");
		sut.ReplyTo.ShouldBe("reply-queue");
		sut.Result.ShouldBe(primary.Result);
		sut.Message.ShouldBe(primary.Message);
		sut.RoutingDecision.ShouldBe(primary.RoutingDecision);
		sut.RequestServices.ShouldBe(primary.RequestServices);
		sut.ReceivedTimestampUtc.ShouldBe(primary.ReceivedTimestampUtc);
		sut.SentTimestampUtc.ShouldBe(primary.SentTimestampUtc);
	}

	[Fact]
	public void DefaultToSafeValuesWhenNoPrimaryContext()
	{
		// Act
		var sut = new BulkContext([]);

		// Assert
		sut.Contexts.Count.ShouldBe(0);
		sut.VersionMetadata.ShouldNotBeNull();
		((object?)sut.ValidationResult).ShouldNotBeNull();
		sut.AuthorizationResult.ShouldNotBeNull();
		sut.RoutingDecision.ShouldNotBeNull();
		sut.RoutingDecision.IsSuccess.ShouldBeTrue();
		sut.Success.ShouldBeTrue();
	}

	[Fact]
	public void ParseDesiredVersionFromPrimaryContextExtensionValue()
	{
		// Arrange
		var primary = new MessageContext(new FakeDispatchMessage(), A.Fake<IServiceProvider>());
		primary.DesiredVersion("42");

		// Act
		var sut = new BulkContext([primary]);

		// Assert
		sut.DesiredVersion.ShouldBe(42);
	}

	[Fact]
	public void KeepDesiredVersionNullWhenPrimaryValueIsInvalid()
	{
		// Arrange
		var primary = new MessageContext(new FakeDispatchMessage(), A.Fake<IServiceProvider>());
		primary.DesiredVersion("not-an-int");

		// Act
		var sut = new BulkContext([primary]);

		// Assert
		sut.DesiredVersion.ShouldBeNull();
	}

	[Fact]
	public void EvaluateSuccessFromValidationAuthorizationAndRouting()
	{
		// Arrange
		var validation = SerializableValidationResult.Success();
		var authorization = Excalibur.Dispatch.Abstractions.AuthorizationResult.Success();
		var routing = RoutingDecision.Success("local", []);
		var sut = new BulkContext([]);
		sut.ValidationResult = validation;
		sut.AuthorizationResult = authorization;
		sut.RoutingDecision = routing;

		// Act/Assert
		sut.Success.ShouldBeTrue();

		// Validation failure
		sut.ValidationResult = SerializableValidationResult.Failed("failed");
		sut.Success.ShouldBeFalse();

		// Authorization failure
		sut.ValidationResult = SerializableValidationResult.Success();
		sut.AuthorizationResult = Excalibur.Dispatch.Abstractions.AuthorizationResult.Failed("nope");
		sut.Success.ShouldBeFalse();

		// Routing failure
		sut.AuthorizationResult = Excalibur.Dispatch.Abstractions.AuthorizationResult.Success();
		sut.RoutingDecision = RoutingDecision.Failure("route-failed");
		sut.Success.ShouldBeFalse();
	}

	[Fact]
	public void ProxyItemsAndPropertiesBehavior()
	{
		// Arrange
		var sut = new BulkContext([]);

		// Act
		sut.SetItem("count", 123);
		var value = sut.GetItem<int>("count");
		var fallback = sut.GetItem("missing", "fallback");
		var properties = sut.Properties;

		// Assert
		value.ShouldBe(123);
		fallback.ShouldBe("fallback");
		sut.ContainsItem("count").ShouldBeTrue();
		properties["count"].ShouldBe(123);
		ReferenceEquals(properties, sut.Properties).ShouldBeTrue();

		sut.RemoveItem("count");
		sut.ContainsItem("count").ShouldBeFalse();
	}

	[Fact]
	public void DelegateChildContextCreationToPrimaryContext()
	{
		// Arrange
		var primary = A.Fake<IMessageContext>();
		A.CallTo(() => primary.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		A.CallTo(() => primary.Properties).Returns(new Dictionary<string, object?>(StringComparer.Ordinal));
		var expectedChild = new MessageContext();
		A.CallTo(() => primary.CreateChildContext()).Returns(expectedChild);
		var sut = new BulkContext([primary]);

		// Act
		var child = sut.CreateChildContext();

		// Assert
		child.ShouldBe(expectedChild);
	}

	[Fact]
	public void CreateFreshMessageContextWhenPrimaryContextMissing()
	{
		// Arrange
		var sut = new BulkContext([]);

		// Act
		var child = sut.CreateChildContext();

		// Assert
		child.ShouldBeOfType<MessageContext>();
	}

	private static MessageContext CreatePrimaryContext()
	{
		var requestServices = A.Fake<IServiceProvider>();
		var message = new FakeDispatchMessage();
		var validation = SerializableValidationResult.Success();
		var authorization = Excalibur.Dispatch.Abstractions.AuthorizationResult.Success();
		var routing = RoutingDecision.Success("in-memory", []);
		var context = new MessageContext(message, requestServices)
		{
			MessageId = "msg-1",
			ExternalId = "ext-1",
			UserId = "user-1",
			CorrelationId = "corr-1",
			CausationId = "cause-1",
			TraceParent = "trace-1",
			TenantId = "tenant-1",
			Source = "source-1",
			MessageType = "OrderPlaced",
			ContentType = "application/json",
			DeliveryCount = 3,
			Result = "ok",
			RoutingDecision = routing,
			ReceivedTimestampUtc = new DateTimeOffset(2026, 2, 1, 10, 0, 0, TimeSpan.Zero),
			SentTimestampUtc = new DateTimeOffset(2026, 2, 1, 10, 1, 0, TimeSpan.Zero)
		};

		context.SerializerVersion("serializer-v1");
		context.MessageVersion("message-v2");
		context.ContractVersion("contract-v3");
		context.DesiredVersion("7");
		context.PartitionKey("partition-a");
		context.ReplyTo("reply-queue");
		context.VersionMetadata(new MessageVersionMetadata { Version = 3, SerializerVersion = 2, SchemaVersion = 1 });
		context.ValidationResult(validation);
		context.AuthorizationResult(authorization);

		return context;
	}
}
