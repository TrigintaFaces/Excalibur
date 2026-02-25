// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Grpc;

using Grpc.Core;

namespace Excalibur.Dispatch.Transport.Tests.Grpc;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class GrpcMethodDescriptorsShould
{
	[Fact]
	public void CreateSendMethodWithCorrectServiceAndMethodName()
	{
		// Arrange
		const string methodPath = "/dispatch.transport.DispatchTransport/Send";

		// Act
		var method = GrpcMethodDescriptors.CreateSendMethod(methodPath);

		// Assert
		method.Type.ShouldBe(MethodType.Unary);
		method.ServiceName.ShouldBe("dispatch.transport.DispatchTransport");
		method.Name.ShouldBe("Send");
	}

	[Fact]
	public void CreateSendBatchMethodWithCorrectServiceAndMethodName()
	{
		// Arrange
		const string methodPath = "/dispatch.transport.DispatchTransport/SendBatch";

		// Act
		var method = GrpcMethodDescriptors.CreateSendBatchMethod(methodPath);

		// Assert
		method.Type.ShouldBe(MethodType.Unary);
		method.ServiceName.ShouldBe("dispatch.transport.DispatchTransport");
		method.Name.ShouldBe("SendBatch");
	}

	[Fact]
	public void CreateReceiveMethodWithCorrectServiceAndMethodName()
	{
		// Arrange
		const string methodPath = "/dispatch.transport.DispatchTransport/Receive";

		// Act
		var method = GrpcMethodDescriptors.CreateReceiveMethod(methodPath);

		// Assert
		method.Type.ShouldBe(MethodType.Unary);
		method.ServiceName.ShouldBe("dispatch.transport.DispatchTransport");
		method.Name.ShouldBe("Receive");
	}

	[Fact]
	public void CreateAcknowledgeMethodWithCorrectServiceAndMethodName()
	{
		// Arrange
		const string methodPath = "/dispatch.transport.DispatchTransport/Acknowledge";

		// Act
		var method = GrpcMethodDescriptors.CreateAcknowledgeMethod(methodPath);

		// Assert
		method.Type.ShouldBe(MethodType.Unary);
		method.ServiceName.ShouldBe("dispatch.transport.DispatchTransport");
		method.Name.ShouldBe("Acknowledge");
	}

	[Fact]
	public void CreateSubscribeMethodAsServerStreaming()
	{
		// Arrange
		const string methodPath = "/dispatch.transport.DispatchTransport/Subscribe";

		// Act
		var method = GrpcMethodDescriptors.CreateSubscribeMethod(methodPath);

		// Assert
		method.Type.ShouldBe(MethodType.ServerStreaming);
		method.ServiceName.ShouldBe("dispatch.transport.DispatchTransport");
		method.Name.ShouldBe("Subscribe");
	}

	[Fact]
	public void HandleCustomServiceNamesCorrectly()
	{
		// Arrange
		const string methodPath = "/my.custom.namespace.MyService/MyMethod";

		// Act
		var method = GrpcMethodDescriptors.CreateSendMethod(methodPath);

		// Assert
		method.ServiceName.ShouldBe("my.custom.namespace.MyService");
		method.Name.ShouldBe("MyMethod");
	}

	[Fact]
	public void HandlePathWithNoLeadingSlash()
	{
		// Arrange
		const string methodPath = "dispatch.transport.DispatchTransport/Send";

		// Act
		var method = GrpcMethodDescriptors.CreateSendMethod(methodPath);

		// Assert
		method.ServiceName.ShouldBe("dispatch.transport.DispatchTransport");
		method.Name.ShouldBe("Send");
	}
}
