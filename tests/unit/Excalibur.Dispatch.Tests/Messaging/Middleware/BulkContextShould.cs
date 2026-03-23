// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Features;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware.Versioning;
using Excalibur.Dispatch.Middleware.Batch;
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

		// Assert - core IMessageContext properties
		sut.MessageId.ShouldBe(primary.MessageId);
		sut.CorrelationId.ShouldBe(primary.CorrelationId);
		sut.CausationId.ShouldBe(primary.CausationId);
		sut.Result.ShouldBeNull(); // BulkContext doesn't copy Result from primary
		sut.Message.ShouldBeNull(); // BulkContext doesn't copy Message from primary
		sut.RequestServices.ShouldBe(primary.RequestServices);

		// Assert - Items-based well-known properties
		sut.SerializerVersion.ShouldBe("serializer-v1");
		sut.MessageVersion.ShouldBe("message-v2");
		sut.ContractVersion.ShouldBe("contract-v3");
		sut.DesiredVersion.ShouldBe(7);
		sut.MessageType.ShouldBe(primary.GetMessageType());
		sut.ContentType.ShouldBe("application/json");
		sut.PartitionKey.ShouldBe("partition-a");
		sut.ReplyTo.ShouldBe("reply-queue");

		// Assert - features are copied from primary
		sut.GetUserId().ShouldBe("user-1");
		sut.GetExternalId().ShouldBe("ext-1");
		sut.GetTraceParent().ShouldBe("trace-1");
		sut.GetTenantId().ShouldBe("tenant-1");
		sut.GetSource().ShouldBe("source-1");
	}

	[Fact]
	public void ThrowWhenCreatedWithEmptyContextList()
	{
		// Act & Assert -- T.2: empty context list is a framework bug
		Should.Throw<InvalidOperationException>(() => new BulkContext([]));
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
		var primary = CreatePrimaryContext();
		var sut = new BulkContext([primary]);
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
	public void ProxyItemsBehavior()
	{
		// Arrange
		var primary = CreatePrimaryContext();
		var sut = new BulkContext([primary]);

		// Act
		sut.SetItem("count", 123);
		var value = sut.GetItem<int>("count");
		var fallback = sut.GetItem("missing", "fallback");

		// Assert
		value.ShouldBe(123);
		fallback.ShouldBe("fallback");
		sut.ContainsItem("count").ShouldBeTrue();
		sut.Items["count"].ShouldBe(123);

		sut.RemoveItem("count");
		sut.ContainsItem("count").ShouldBeFalse();
	}

	[Fact]
	public void CreateChildContextFromPrimaryContext()
	{
		// Arrange
		var primary = CreatePrimaryContext();
		var sut = new BulkContext([primary]);

		// Act
		var child = sut.CreateChildContext();

		// Assert
		child.ShouldNotBeNull();
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
			CorrelationId = "corr-1",
			CausationId = "cause-1",
			Result = "ok",
		};

		// Set identity features
		var identity = context.GetOrCreateIdentityFeature();
		identity.ExternalId = "ext-1";
		identity.UserId = "user-1";
		identity.TraceParent = "trace-1";
		identity.TenantId = "tenant-1";

		// Set routing features
		var routingFeature = context.GetOrCreateRoutingFeature();
		routingFeature.Source = "source-1";
		routingFeature.RoutingDecision = routing;

		// Set Items-based properties
		context.SetMessageType("OrderPlaced");
		context.SetContentType("application/json");
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
