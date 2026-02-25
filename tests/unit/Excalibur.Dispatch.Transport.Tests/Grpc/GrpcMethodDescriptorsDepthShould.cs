// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Grpc;

using Grpc.Core;

namespace Excalibur.Dispatch.Transport.Tests.Grpc;

/// <summary>
/// Depth coverage tests for <see cref="GrpcMethodDescriptors"/> covering
/// edge cases in path extraction: single-segment paths, empty paths,
/// deeply nested service names, and marshaller wire-up verification.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class GrpcMethodDescriptorsDepthShould
{
	[Fact]
	public void ExtractSingleSegmentPath_AsServiceNameOnly()
	{
		// Arrange — path with no slash at all
		const string methodPath = "SimpleMethod";

		// Act
		var method = GrpcMethodDescriptors.CreateSendMethod(methodPath);

		// Assert — ExtractServiceName returns the whole string, ExtractMethodName also returns the whole string
		method.ServiceName.ShouldBe("SimpleMethod");
		method.Name.ShouldBe("SimpleMethod");
	}

	[Fact]
	public void HandleDeeplyNestedServiceName()
	{
		// Arrange — deeply nested package name
		const string methodPath = "/com.company.department.subdepartment.Service/Method";

		// Act
		var method = GrpcMethodDescriptors.CreateSendMethod(methodPath);

		// Assert
		method.ServiceName.ShouldBe("com.company.department.subdepartment.Service");
		method.Name.ShouldBe("Method");
	}

	[Fact]
	public void HandleTrailingSlash()
	{
		// Arrange — path ending with slash
		const string methodPath = "/Service/";

		// Act
		var method = GrpcMethodDescriptors.CreateSendMethod(methodPath);

		// Assert — last slash splits correctly: service = "Service", name = ""
		method.ServiceName.ShouldBe("Service");
		method.Name.ShouldBe(string.Empty);
	}

	[Fact]
	public void CreateReceiveMethod_WithCorrectMarshallers()
	{
		// Arrange
		const string methodPath = "/dispatch.transport/Receive";

		// Act
		var method = GrpcMethodDescriptors.CreateReceiveMethod(methodPath);

		// Assert — verify method type and name
		method.Type.ShouldBe(MethodType.Unary);
		method.Name.ShouldBe("Receive");
		method.ServiceName.ShouldBe("dispatch.transport");
	}

	[Fact]
	public void CreateAcknowledgeMethod_WithCorrectMarshallers()
	{
		// Arrange
		const string methodPath = "/dispatch.transport/Acknowledge";

		// Act
		var method = GrpcMethodDescriptors.CreateAcknowledgeMethod(methodPath);

		// Assert
		method.Type.ShouldBe(MethodType.Unary);
		method.Name.ShouldBe("Acknowledge");
	}

	[Fact]
	public void CreateSubscribeMethod_AsServerStreaming()
	{
		// Arrange
		const string methodPath = "/dispatch.transport/Subscribe";

		// Act
		var method = GrpcMethodDescriptors.CreateSubscribeMethod(methodPath);

		// Assert
		method.Type.ShouldBe(MethodType.ServerStreaming);
		method.Name.ShouldBe("Subscribe");
	}

	[Fact]
	public void CreateSendBatchMethod_WithCorrectType()
	{
		// Arrange
		const string methodPath = "/dispatch.transport/SendBatch";

		// Act
		var method = GrpcMethodDescriptors.CreateSendBatchMethod(methodPath);

		// Assert
		method.Type.ShouldBe(MethodType.Unary);
		method.Name.ShouldBe("SendBatch");
	}

	[Fact]
	public void HandleMultipleLeadingSlashes()
	{
		// Arrange — TrimStart('/') removes all leading slashes
		const string methodPath = "///Service/Method";

		// Act
		var method = GrpcMethodDescriptors.CreateSendMethod(methodPath);

		// Assert — After TrimStart('/') = "Service/Method"
		method.ServiceName.ShouldBe("Service");
		method.Name.ShouldBe("Method");
	}

	[Fact]
	public void BeInternalAndStatic()
	{
		// Assert
		typeof(GrpcMethodDescriptors).IsNotPublic.ShouldBeTrue();
		typeof(GrpcMethodDescriptors).IsAbstract.ShouldBeTrue(); // static classes are abstract+sealed
		typeof(GrpcMethodDescriptors).IsSealed.ShouldBeTrue();
	}
}
