// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Saga.StateMachine;
using SagaStateBase = Excalibur.Dispatch.Abstractions.Messaging.SagaState;

namespace Excalibur.Saga.Tests.StateMachine;

/// <summary>
/// Test data class for SagaContext tests.
/// </summary>
public sealed class SagaContextTestData : SagaStateBase
{
	public int OrderId { get; set; }
	public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Test message class for SagaContext tests.
/// </summary>
public sealed class SagaContextTestMessage
{
	public string MessageContent { get; set; } = string.Empty;
}

/// <summary>
/// Unit tests for <see cref="SagaContext{TData, TMessage}"/>.
/// Verifies saga context record behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaContextShould
{
	#region Constructor Tests

	[Fact]
	public void CreateInstance_WithValidParameters()
	{
		// Arrange
		var data = new SagaContextTestData { OrderId = 123 };
		var message = new SagaContextTestMessage { MessageContent = "Test" };
		var processManager = A.Fake<ProcessManager<SagaContextTestData>>();

		// Act
		var context = new SagaContext<SagaContextTestData, SagaContextTestMessage>(data, message, processManager);

		// Assert
		context.ShouldNotBeNull();
	}

	[Fact]
	public void StoreData_WhenProvided()
	{
		// Arrange
		var data = new SagaContextTestData { OrderId = 456, Status = "Active" };
		var message = new SagaContextTestMessage();
		var processManager = A.Fake<ProcessManager<SagaContextTestData>>();

		// Act
		var context = new SagaContext<SagaContextTestData, SagaContextTestMessage>(data, message, processManager);

		// Assert
		context.Data.ShouldBe(data);
		context.Data.OrderId.ShouldBe(456);
		context.Data.Status.ShouldBe("Active");
	}

	[Fact]
	public void StoreMessage_WhenProvided()
	{
		// Arrange
		var data = new SagaContextTestData();
		var message = new SagaContextTestMessage { MessageContent = "Important message" };
		var processManager = A.Fake<ProcessManager<SagaContextTestData>>();

		// Act
		var context = new SagaContext<SagaContextTestData, SagaContextTestMessage>(data, message, processManager);

		// Assert
		context.Message.ShouldBe(message);
		context.Message.MessageContent.ShouldBe("Important message");
	}

	[Fact]
	public void StoreProcessManager_WhenProvided()
	{
		// Arrange
		var data = new SagaContextTestData();
		var message = new SagaContextTestMessage();
		var processManager = A.Fake<ProcessManager<SagaContextTestData>>();

		// Act
		var context = new SagaContext<SagaContextTestData, SagaContextTestMessage>(data, message, processManager);

		// Assert
		context.ProcessManager.ShouldBe(processManager);
	}

	#endregion

	#region Record Equality Tests

	[Fact]
	public void BeEqual_WhenPropertiesMatch()
	{
		// Arrange
		var data = new SagaContextTestData { OrderId = 123 };
		var message = new SagaContextTestMessage { MessageContent = "Test" };
		var processManager = A.Fake<ProcessManager<SagaContextTestData>>();

		// Act
		var context1 = new SagaContext<SagaContextTestData, SagaContextTestMessage>(data, message, processManager);
		var context2 = new SagaContext<SagaContextTestData, SagaContextTestMessage>(data, message, processManager);

		// Assert
		context1.ShouldBe(context2);
	}

	[Fact]
	public void NotBeEqual_WhenDataDiffers()
	{
		// Arrange
		var data1 = new SagaContextTestData { OrderId = 123 };
		var data2 = new SagaContextTestData { OrderId = 456 };
		var message = new SagaContextTestMessage();
		var processManager = A.Fake<ProcessManager<SagaContextTestData>>();

		// Act
		var context1 = new SagaContext<SagaContextTestData, SagaContextTestMessage>(data1, message, processManager);
		var context2 = new SagaContext<SagaContextTestData, SagaContextTestMessage>(data2, message, processManager);

		// Assert
		context1.ShouldNotBe(context2);
	}

	[Fact]
	public void NotBeEqual_WhenMessageDiffers()
	{
		// Arrange
		var data = new SagaContextTestData();
		var message1 = new SagaContextTestMessage { MessageContent = "First" };
		var message2 = new SagaContextTestMessage { MessageContent = "Second" };
		var processManager = A.Fake<ProcessManager<SagaContextTestData>>();

		// Act
		var context1 = new SagaContext<SagaContextTestData, SagaContextTestMessage>(data, message1, processManager);
		var context2 = new SagaContext<SagaContextTestData, SagaContextTestMessage>(data, message2, processManager);

		// Assert
		context1.ShouldNotBe(context2);
	}

	[Fact]
	public void NotBeEqual_WhenProcessManagerDiffers()
	{
		// Arrange
		var data = new SagaContextTestData();
		var message = new SagaContextTestMessage();
		var processManager1 = A.Fake<ProcessManager<SagaContextTestData>>();
		var processManager2 = A.Fake<ProcessManager<SagaContextTestData>>();

		// Act
		var context1 = new SagaContext<SagaContextTestData, SagaContextTestMessage>(data, message, processManager1);
		var context2 = new SagaContext<SagaContextTestData, SagaContextTestMessage>(data, message, processManager2);

		// Assert
		context1.ShouldNotBe(context2);
	}

	#endregion

	#region Record With Expression Tests

	[Fact]
	public void SupportWithExpression_ForData()
	{
		// Arrange
		var data = new SagaContextTestData { OrderId = 123 };
		var newData = new SagaContextTestData { OrderId = 456 };
		var message = new SagaContextTestMessage();
		var processManager = A.Fake<ProcessManager<SagaContextTestData>>();
		var context = new SagaContext<SagaContextTestData, SagaContextTestMessage>(data, message, processManager);

		// Act
		var newContext = context with { Data = newData };

		// Assert
		newContext.Data.OrderId.ShouldBe(456);
		newContext.Message.ShouldBe(message);
		newContext.ProcessManager.ShouldBe(processManager);
	}

	[Fact]
	public void SupportWithExpression_ForMessage()
	{
		// Arrange
		var data = new SagaContextTestData();
		var message = new SagaContextTestMessage { MessageContent = "Original" };
		var newMessage = new SagaContextTestMessage { MessageContent = "Updated" };
		var processManager = A.Fake<ProcessManager<SagaContextTestData>>();
		var context = new SagaContext<SagaContextTestData, SagaContextTestMessage>(data, message, processManager);

		// Act
		var newContext = context with { Message = newMessage };

		// Assert
		newContext.Data.ShouldBe(data);
		newContext.Message.MessageContent.ShouldBe("Updated");
		newContext.ProcessManager.ShouldBe(processManager);
	}

	[Fact]
	public void SupportWithExpression_ForProcessManager()
	{
		// Arrange
		var data = new SagaContextTestData();
		var message = new SagaContextTestMessage();
		var processManager = A.Fake<ProcessManager<SagaContextTestData>>();
		var newProcessManager = A.Fake<ProcessManager<SagaContextTestData>>();
		var context = new SagaContext<SagaContextTestData, SagaContextTestMessage>(data, message, processManager);

		// Act
		var newContext = context with { ProcessManager = newProcessManager };

		// Assert
		newContext.Data.ShouldBe(data);
		newContext.Message.ShouldBe(message);
		newContext.ProcessManager.ShouldBe(newProcessManager);
	}

	#endregion

	#region ToString Tests

	[Fact]
	public void ReturnMeaningfulToString()
	{
		// Arrange
		var data = new SagaContextTestData { OrderId = 123 };
		var message = new SagaContextTestMessage { MessageContent = "Test" };
		var processManager = A.Fake<ProcessManager<SagaContextTestData>>();
		var context = new SagaContext<SagaContextTestData, SagaContextTestMessage>(data, message, processManager);

		// Act
		var result = context.ToString();

		// Assert
		result.ShouldNotBeNullOrEmpty();
		result.ShouldContain("SagaContext");
	}

	#endregion

	#region Deconstruction Tests

	[Fact]
	public void SupportDeconstruction()
	{
		// Arrange
		var data = new SagaContextTestData { OrderId = 789 };
		var message = new SagaContextTestMessage { MessageContent = "Deconstruct" };
		var processManager = A.Fake<ProcessManager<SagaContextTestData>>();
		var context = new SagaContext<SagaContextTestData, SagaContextTestMessage>(data, message, processManager);

		// Act
		var (deconstructedData, deconstructedMessage, deconstructedProcessManager) = context;

		// Assert
		deconstructedData.ShouldBe(data);
		deconstructedMessage.ShouldBe(message);
		deconstructedProcessManager.ShouldBe(processManager);
	}

	#endregion
}
