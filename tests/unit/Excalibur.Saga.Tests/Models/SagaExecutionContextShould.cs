// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Models;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Saga.Tests.Models;

/// <summary>
/// Unit tests for <see cref="SagaExecutionContext{TData}"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaExecutionContextShould
{
	#region Constructor Tests

	[Fact]
	public void CreateWithRequiredParameters()
	{
		// Arrange
		var services = new ServiceCollection().BuildServiceProvider();
		var data = new TestSagaData { OrderId = "ORD-123" };

		// Act
		var context = new SagaExecutionContext<TestSagaData>(
			"saga-001",
			"corr-001",
			data,
			services,
			currentStepIndex: 0);

		// Assert
		context.SagaId.ShouldBe("saga-001");
		context.CorrelationId.ShouldBe("corr-001");
		context.Data.ShouldBe(data);
		context.Services.ShouldBe(services);
		context.CurrentStepIndex.ShouldBe(0);
		context.IsCompensating.ShouldBeFalse();
	}

	[Fact]
	public void CreateWithIsCompensatingTrue()
	{
		// Arrange
		var services = new ServiceCollection().BuildServiceProvider();
		var data = new TestSagaData();

		// Act
		var context = new SagaExecutionContext<TestSagaData>(
			"saga-002",
			"corr-002",
			data,
			services,
			currentStepIndex: 2,
			isCompensating: true);

		// Assert
		context.IsCompensating.ShouldBeTrue();
	}

	[Fact]
	public void CreateWithIsCompensatingFalseByDefault()
	{
		// Arrange
		var services = new ServiceCollection().BuildServiceProvider();
		var data = new TestSagaData();

		// Act
		var context = new SagaExecutionContext<TestSagaData>(
			"saga-003",
			"corr-003",
			data,
			services,
			currentStepIndex: 0);

		// Assert
		context.IsCompensating.ShouldBeFalse();
	}

	#endregion Constructor Tests

	#region Property Tests

	[Fact]
	public void ExposeEmptySharedContextByDefault()
	{
		// Arrange
		var services = new ServiceCollection().BuildServiceProvider();
		var context = new SagaExecutionContext<TestSagaData>(
			"saga-id",
			"corr-id",
			new TestSagaData(),
			services,
			0);

		// Assert
		context.SharedContext.ShouldNotBeNull();
		context.SharedContext.ShouldBeEmpty();
	}

	[Fact]
	public void ExposeMetadataAsSameReferenceAsSharedContext()
	{
		// Arrange
		var services = new ServiceCollection().BuildServiceProvider();
		var context = new SagaExecutionContext<TestSagaData>(
			"saga-id",
			"corr-id",
			new TestSagaData(),
			services,
			0);

		// Act
		context.SharedContext["key"] = "value";

		// Assert
		context.Metadata.ShouldContainKey("key");
		context.Metadata["key"].ShouldBe("value");
		ReferenceEquals(context.SharedContext, context.Metadata).ShouldBeTrue();
	}

	[Fact]
	public void ExposeEmptyActivitiesListByDefault()
	{
		// Arrange
		var services = new ServiceCollection().BuildServiceProvider();
		var context = new SagaExecutionContext<TestSagaData>(
			"saga-id",
			"corr-id",
			new TestSagaData(),
			services,
			0);

		// Assert
		context.Activities.ShouldNotBeNull();
		context.Activities.ShouldBeEmpty();
	}

	[Fact]
	public void AllowDataToBeModified()
	{
		// Arrange
		var services = new ServiceCollection().BuildServiceProvider();
		var initialData = new TestSagaData { OrderId = "ORD-001" };
		var context = new SagaExecutionContext<TestSagaData>(
			"saga-id",
			"corr-id",
			initialData,
			services,
			0);

		// Act
		context.Data = new TestSagaData { OrderId = "ORD-002" };

		// Assert
		context.Data.OrderId.ShouldBe("ORD-002");
	}

	#endregion Property Tests

	#region SharedContext Tests

	[Fact]
	public void AllowSharedContextToBePopulated()
	{
		// Arrange
		var services = new ServiceCollection().BuildServiceProvider();
		var context = new SagaExecutionContext<TestSagaData>(
			"saga-id",
			"corr-id",
			new TestSagaData(),
			services,
			0);

		// Act
		context.SharedContext["transactionId"] = "TXN-12345";
		context.SharedContext["amount"] = 99.99m;
		context.SharedContext["timestamp"] = DateTimeOffset.UtcNow;

		// Assert
		context.SharedContext.Count.ShouldBe(3);
		context.SharedContext["transactionId"].ShouldBe("TXN-12345");
	}

	[Fact]
	public void PreserveSharedContextAcrossSteps()
	{
		// Arrange
		var services = new ServiceCollection().BuildServiceProvider();
		var context = new SagaExecutionContext<TestSagaData>(
			"saga-id",
			"corr-id",
			new TestSagaData(),
			services,
			0);

		// Act - Simulate step 1 adding data
		context.SharedContext["step1_result"] = "success";

		// Assert - Data is available for step 2
		context.SharedContext.ShouldContainKey("step1_result");
	}

	#endregion SharedContext Tests

	#region AddActivity Tests

	[Fact]
	public void AddActivityWithMessage()
	{
		// Arrange
		var services = new ServiceCollection().BuildServiceProvider();
		var context = new SagaExecutionContext<TestSagaData>(
			"saga-id",
			"corr-id",
			new TestSagaData(),
			services,
			0);

		// Act
		context.AddActivity("Step started");

		// Assert
		context.Activities.Count.ShouldBe(1);
		context.Activities[0].Message.ShouldBe("Step started");
		context.Activities[0].Details.ShouldBeNull();
	}

	[Fact]
	public void AddActivityWithMessageAndDetails()
	{
		// Arrange
		var services = new ServiceCollection().BuildServiceProvider();
		var context = new SagaExecutionContext<TestSagaData>(
			"saga-id",
			"corr-id",
			new TestSagaData(),
			services,
			0);

		var details = new { orderId = "ORD-123", status = "processing" };

		// Act
		context.AddActivity("Processing order", details);

		// Assert
		context.Activities.Count.ShouldBe(1);
		context.Activities[0].Message.ShouldBe("Processing order");
		context.Activities[0].Details.ShouldNotBeNull();
	}

	[Fact]
	public void AddMultipleActivities()
	{
		// Arrange
		var services = new ServiceCollection().BuildServiceProvider();
		var context = new SagaExecutionContext<TestSagaData>(
			"saga-id",
			"corr-id",
			new TestSagaData(),
			services,
			0);

		// Act
		context.AddActivity("Step 1 started");
		context.AddActivity("Step 1 completed");
		context.AddActivity("Step 2 started");

		// Assert
		context.Activities.Count.ShouldBe(3);
	}

	[Fact]
	public void SetActivityTimestamp()
	{
		// Arrange
		var services = new ServiceCollection().BuildServiceProvider();
		var context = new SagaExecutionContext<TestSagaData>(
			"saga-id",
			"corr-id",
			new TestSagaData(),
			services,
			0);

		var beforeAdd = DateTimeOffset.UtcNow;

		// Act
		context.AddActivity("Test activity");

		var afterAdd = DateTimeOffset.UtcNow;

		// Assert
		context.Activities[0].Timestamp.ShouldBeGreaterThanOrEqualTo(beforeAdd);
		context.Activities[0].Timestamp.ShouldBeLessThanOrEqualTo(afterAdd);
	}

	[Fact]
	public void MaintainActivityOrderChronologically()
	{
		// Arrange
		var services = new ServiceCollection().BuildServiceProvider();
		var context = new SagaExecutionContext<TestSagaData>(
			"saga-id",
			"corr-id",
			new TestSagaData(),
			services,
			0);

		// Act
		context.AddActivity("First");
		context.AddActivity("Second");
		context.AddActivity("Third");

		// Assert
		context.Activities[0].Message.ShouldBe("First");
		context.Activities[1].Message.ShouldBe("Second");
		context.Activities[2].Message.ShouldBe("Third");
	}

	#endregion AddActivity Tests

	#region GetRequiredService Tests

	[Fact]
	public void GetRequiredServiceFromServiceProvider()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton<ITestService, TestService>();
		var serviceProvider = services.BuildServiceProvider();

		var context = new SagaExecutionContext<TestSagaData>(
			"saga-id",
			"corr-id",
			new TestSagaData(),
			serviceProvider,
			0);

		// Act
		var service = context.GetRequiredService<ITestService>();

		// Assert
		service.ShouldNotBeNull();
		service.ShouldBeOfType<TestService>();
	}

	[Fact]
	public void ThrowWhenRequiredServiceNotRegistered()
	{
		// Arrange
		var services = new ServiceCollection().BuildServiceProvider();
		var context = new SagaExecutionContext<TestSagaData>(
			"saga-id",
			"corr-id",
			new TestSagaData(),
			services,
			0);

		// Act & Assert
		Should.Throw<InvalidOperationException>(
			() => context.GetRequiredService<ITestService>());
	}

	#endregion GetRequiredService Tests

	#region ISagaContext Implementation Tests

	[Fact]
	public void ImplementISagaContext()
	{
		// Arrange
		var services = new ServiceCollection().BuildServiceProvider();
		var context = new SagaExecutionContext<TestSagaData>(
			"saga-id",
			"corr-id",
			new TestSagaData(),
			services,
			0);

		// Assert
		context.ShouldBeAssignableTo<ISagaContext<TestSagaData>>();
	}

	#endregion ISagaContext Implementation Tests

	#region Scenario Tests

	[Fact]
	public void RepresentOrderProcessingContext()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton<ITestService, TestService>();
		var serviceProvider = services.BuildServiceProvider();

		var data = new TestSagaData
		{
			OrderId = "ORD-789",
			CustomerId = "CUST-456",
		};

		// Act
		var context = new SagaExecutionContext<TestSagaData>(
			"order-saga-001",
			"request-12345",
			data,
			serviceProvider,
			currentStepIndex: 1);

		context.SharedContext["paymentMethod"] = "CreditCard";
		context.AddActivity("Validating order");
		context.AddActivity("Processing payment", new { amount = 150.00m });

		// Assert
		context.SagaId.ShouldBe("order-saga-001");
		context.Data.OrderId.ShouldBe("ORD-789");
		context.SharedContext["paymentMethod"].ShouldBe("CreditCard");
		context.Activities.Count.ShouldBe(2);
	}

	[Fact]
	public void RepresentCompensationContext()
	{
		// Arrange
		var services = new ServiceCollection().BuildServiceProvider();
		var data = new TestSagaData { OrderId = "ORD-FAILED" };

		// Act
		var context = new SagaExecutionContext<TestSagaData>(
			"failed-saga-001",
			"request-failed",
			data,
			services,
			currentStepIndex: 2,
			isCompensating: true);

		context.AddActivity("Starting compensation");
		context.AddActivity("Refunding payment");

		// Assert
		context.IsCompensating.ShouldBeTrue();
		context.CurrentStepIndex.ShouldBe(2);
		context.Activities.Count.ShouldBe(2);
	}

	#endregion Scenario Tests

	#region Test Helper Types

	private sealed class TestSagaData
	{
		public string OrderId { get; init; } = string.Empty;
		public string CustomerId { get; init; } = string.Empty;
	}

	private interface ITestService
	{
		string GetValue();
	}

	private sealed class TestService : ITestService
	{
		public string GetValue() => "test-value";
	}

	#endregion Test Helper Types
}
