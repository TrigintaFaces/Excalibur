// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
/// Unit tests for <see cref="GrpcMappingContext"/>.
/// </summary>
/// <remarks>
/// Tests the gRPC mapping context implementation.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Priority", "0")]
public sealed class GrpcMappingContextShould
{
	#region Default Values Tests

	[Fact]
	public void Default_HasNullMethodName()
	{
		// Arrange & Act
		var context = new GrpcMappingContext();

		// Assert
		context.MethodName.ShouldBeNull();
	}

	[Fact]
	public void Default_HasNullDeadline()
	{
		// Arrange & Act
		var context = new GrpcMappingContext();

		// Assert
		context.Deadline.ShouldBeNull();
	}

	[Fact]
	public void Default_HasEmptyHeaders()
	{
		// Arrange & Act
		var context = new GrpcMappingContext();

		// Assert
		context.Headers.ShouldBeEmpty();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void MethodName_CanBeSet()
	{
		// Arrange
		var context = new GrpcMappingContext();

		// Act
		context.MethodName = "/myservice.MyService/GetOrder";

		// Assert
		context.MethodName.ShouldBe("/myservice.MyService/GetOrder");
	}

	[Fact]
	public void Deadline_CanBeSet()
	{
		// Arrange
		var context = new GrpcMappingContext();
		var deadline = TimeSpan.FromSeconds(30);

		// Act
		context.Deadline = deadline;

		// Assert
		context.Deadline.ShouldBe(deadline);
	}

	[Fact]
	public void Deadline_CanBeSetToZero()
	{
		// Arrange
		var context = new GrpcMappingContext();

		// Act
		context.Deadline = TimeSpan.Zero;

		// Assert
		context.Deadline.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void Deadline_CanBeSetToLargeValue()
	{
		// Arrange
		var context = new GrpcMappingContext();
		var deadline = TimeSpan.FromHours(24);

		// Act
		context.Deadline = deadline;

		// Assert
		context.Deadline.ShouldBe(deadline);
	}

	#endregion

	#region SetHeader Tests

	[Fact]
	public void SetHeader_AddsHeader()
	{
		// Arrange
		var context = new GrpcMappingContext();

		// Act
		context.SetHeader("authorization", "Bearer token123");

		// Assert
		context.Headers.ShouldContainKey("authorization");
		context.Headers["authorization"].ShouldBe("Bearer token123");
	}

	[Fact]
	public void SetHeader_WithSameKey_OverwritesValue()
	{
		// Arrange
		var context = new GrpcMappingContext();
		context.SetHeader("x-request-id", "id1");

		// Act
		context.SetHeader("x-request-id", "id2");

		// Assert
		context.Headers["x-request-id"].ShouldBe("id2");
		context.Headers.Count.ShouldBe(1);
	}

	[Fact]
	public void SetHeader_IsCaseInsensitive()
	{
		// Arrange
		var context = new GrpcMappingContext();
		context.SetHeader("X-Header", "value1");

		// Act
		context.SetHeader("x-header", "value2");

		// Assert
		context.Headers.Count.ShouldBe(1);
	}

	[Fact]
	public void SetHeader_WithNullKey_ThrowsArgumentException()
	{
		// Arrange
		var context = new GrpcMappingContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.SetHeader(null!, "value"));
	}

	[Fact]
	public void SetHeader_WithEmptyKey_ThrowsArgumentException()
	{
		// Arrange
		var context = new GrpcMappingContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.SetHeader(string.Empty, "value"));
	}

	[Fact]
	public void SetHeader_WithWhitespaceKey_ThrowsArgumentException()
	{
		// Arrange
		var context = new GrpcMappingContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.SetHeader("   ", "value"));
	}

	[Fact]
	public void SetHeader_CanAddMultipleHeaders()
	{
		// Arrange
		var context = new GrpcMappingContext();

		// Act
		context.SetHeader("authorization", "Bearer token");
		context.SetHeader("x-request-id", "req-123");
		context.SetHeader("x-correlation-id", "corr-456");

		// Assert
		context.Headers.Count.ShouldBe(3);
	}

	[Fact]
	public void SetHeader_WithNullValue_SetsNullValue()
	{
		// Arrange
		var context = new GrpcMappingContext();

		// Act
		context.SetHeader("header", null!);

		// Assert
		context.Headers.ShouldContainKey("header");
		context.Headers["header"].ShouldBeNull();
	}

	#endregion

	#region ApplyTo Tests

	[Fact]
	public void ApplyTo_WithNullContext_ThrowsArgumentNullException()
	{
		// Arrange
		var context = new GrpcMappingContext();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => context.ApplyTo(null!));
	}

	[Fact]
	public void ApplyTo_AppliesHeadersToTransportContext()
	{
		// Arrange
		var mappingContext = new GrpcMappingContext();
		mappingContext.SetHeader("authorization", "Bearer token");
		var messageContext = new TransportMessageContext();

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.GetTransportProperty<string>("grpc.authorization").ShouldBe("Bearer token");
	}

	[Fact]
	public void ApplyTo_AppliesMultipleHeaders()
	{
		// Arrange
		var mappingContext = new GrpcMappingContext();
		mappingContext.SetHeader("header1", "value1");
		mappingContext.SetHeader("header2", "value2");
		var messageContext = new TransportMessageContext();

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.GetTransportProperty<string>("grpc.header1").ShouldBe("value1");
		messageContext.GetTransportProperty<string>("grpc.header2").ShouldBe("value2");
	}

	[Fact]
	public void ApplyTo_WithNoHeaders_DoesNothing()
	{
		// Arrange
		var mappingContext = new GrpcMappingContext();
		var messageContext = new TransportMessageContext();

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.GetAllTransportProperties().ShouldBeEmpty();
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsIGrpcMappingContext()
	{
		// Arrange & Act
		var context = new GrpcMappingContext();

		// Assert
		_ = context.ShouldBeAssignableTo<IGrpcMappingContext>();
	}

	#endregion
}
