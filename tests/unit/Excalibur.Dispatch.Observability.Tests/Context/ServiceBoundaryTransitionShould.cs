// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Unit tests for <see cref="ServiceBoundaryTransition"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Context")]
public sealed class ServiceBoundaryTransitionShould
{
	#region Required Property Tests

	[Fact]
	public void RequireServiceName()
	{
		// Arrange & Act
		var transition = new ServiceBoundaryTransition
		{
			ServiceName = "OrderService",
		};

		// Assert
		transition.ServiceName.ShouldBe("OrderService");
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void HaveDefaultTimestamp()
	{
		// Arrange & Act
		var transition = new ServiceBoundaryTransition
		{
			ServiceName = "PaymentService",
		};

		// Assert
		transition.Timestamp.ShouldBe(default(DateTimeOffset));
	}

	[Fact]
	public void HaveNullTraceParentByDefault()
	{
		// Arrange & Act
		var transition = new ServiceBoundaryTransition
		{
			ServiceName = "InventoryService",
		};

		// Assert
		transition.TraceParent.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultContextPreserved()
	{
		// Arrange & Act
		var transition = new ServiceBoundaryTransition
		{
			ServiceName = "ShippingService",
		};

		// Assert
		transition.ContextPreserved.ShouldBeFalse();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void AllowSettingTimestamp()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var transition = new ServiceBoundaryTransition
		{
			ServiceName = "NotificationService",
			Timestamp = timestamp,
		};

		// Assert
		transition.Timestamp.ShouldBe(timestamp);
	}

	[Fact]
	public void AllowSettingTraceParent()
	{
		// Arrange & Act
		var transition = new ServiceBoundaryTransition
		{
			ServiceName = "AuthService",
			TraceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
		};

		// Assert
		transition.TraceParent.ShouldBe("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01");
	}

	[Fact]
	public void AllowSettingContextPreservedToTrue()
	{
		// Arrange & Act
		var transition = new ServiceBoundaryTransition
		{
			ServiceName = "CacheService",
			ContextPreserved = true,
		};

		// Assert
		transition.ContextPreserved.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingContextPreservedToFalse()
	{
		// Arrange & Act
		var transition = new ServiceBoundaryTransition
		{
			ServiceName = "ExternalApi",
			ContextPreserved = false,
		};

		// Assert
		transition.ContextPreserved.ShouldBeFalse();
	}

	#endregion

	#region Complete Object Tests

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var transition = new ServiceBoundaryTransition
		{
			ServiceName = "CompleteService",
			Timestamp = timestamp,
			TraceParent = "00-12345678901234567890123456789012-1234567890123456-01",
			ContextPreserved = true,
		};

		// Assert
		transition.ServiceName.ShouldBe("CompleteService");
		transition.Timestamp.ShouldBe(timestamp);
		transition.TraceParent.ShouldBe("00-12345678901234567890123456789012-1234567890123456-01");
		transition.ContextPreserved.ShouldBeTrue();
	}

	[Theory]
	[InlineData("ServiceA")]
	[InlineData("my-microservice")]
	[InlineData("Order.API")]
	[InlineData("com.example.service")]
	public void SupportVariousServiceNameFormats(string serviceName)
	{
		// Arrange & Act
		var transition = new ServiceBoundaryTransition
		{
			ServiceName = serviceName,
		};

		// Assert
		transition.ServiceName.ShouldBe(serviceName);
	}

	[Theory]
	[InlineData("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01")]
	[InlineData("00-00000000000000000000000000000000-0000000000000000-00")]
	[InlineData("00-ffffffffffffffffffffffffffffffff-ffffffffffffffff-ff")]
	public void SupportW3CTraceParentFormat(string traceParent)
	{
		// Arrange & Act
		var transition = new ServiceBoundaryTransition
		{
			ServiceName = "TraceService",
			TraceParent = traceParent,
		};

		// Assert
		transition.TraceParent.ShouldBe(traceParent);
	}

	[Fact]
	public void SupportScenarioWhereContextIsPreserved()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var transition = new ServiceBoundaryTransition
		{
			ServiceName = "DownstreamService",
			Timestamp = timestamp,
			TraceParent = "00-abcdef1234567890abcdef1234567890-fedcba0987654321-01",
			ContextPreserved = true,
		};

		// Assert - This represents a successful context propagation
		transition.ContextPreserved.ShouldBeTrue();
		transition.TraceParent.ShouldNotBeNull();
	}

	[Fact]
	public void SupportScenarioWhereContextIsLost()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var transition = new ServiceBoundaryTransition
		{
			ServiceName = "ExternalThirdPartyApi",
			Timestamp = timestamp,
			TraceParent = null,
			ContextPreserved = false,
		};

		// Assert - This represents context loss at service boundary
		transition.ContextPreserved.ShouldBeFalse();
		transition.TraceParent.ShouldBeNull();
	}

	#endregion
}
