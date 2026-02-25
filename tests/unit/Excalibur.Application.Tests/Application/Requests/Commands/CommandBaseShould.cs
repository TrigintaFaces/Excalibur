// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Transactions;

using Excalibur.Application.Requests;
using Excalibur.Application.Requests.Commands;
using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Tests.Application.Requests.Commands;

/// <summary>
/// Unit tests for <see cref="CommandBase"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Application")]
[Trait("Feature", "Commands")]
public sealed class CommandBaseShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Create_WithCorrelationId_SetsCorrelationId()
	{
		// Arrange
		var correlationId = Guid.NewGuid();

		// Act
		var command = new TestCommand(correlationId);

		// Assert
		((IAmCorrelatable)command).CorrelationId.ShouldBe(correlationId);
	}

	[Fact]
	public void Create_WithTenantId_SetsTenantId()
	{
		// Arrange
		var correlationId = Guid.NewGuid();
		var tenantId = "tenant-123";

		// Act
		var command = new TestCommand(correlationId, tenantId);

		// Assert
		command.TenantId.ShouldBe(tenantId);
	}

	[Fact]
	public void Create_WithoutTenantId_SetsDefaultTenantId()
	{
		// Arrange
		var correlationId = Guid.NewGuid();

		// Act
		var command = new TestCommand(correlationId);

		// Assert
		command.TenantId.ShouldBe(TenantDefaults.DefaultTenantId);
	}

	[Fact]
	public void Create_WithDefaultConstructor_SetsEmptyCorrelationId()
	{
		// Act
		var command = new TestCommandWithDefaultConstructor();

		// Assert
		((IAmCorrelatable)command).CorrelationId.ShouldBe(Guid.Empty);
	}

	#endregion

	#region Id Property Tests

	[Fact]
	public void Id_GeneratesUniqueGuid()
	{
		// Arrange & Act
		var command1 = new TestCommand(Guid.NewGuid());
		var command2 = new TestCommand(Guid.NewGuid());

		// Assert
		command1.Id.ShouldNotBe(Guid.Empty);
		command2.Id.ShouldNotBe(Guid.Empty);
		command1.Id.ShouldNotBe(command2.Id);
	}

	#endregion

	#region MessageId Property Tests

	[Fact]
	public void MessageId_ReturnsIdAsString()
	{
		// Arrange
		var command = new TestCommand(Guid.NewGuid());

		// Act & Assert
		command.MessageId.ShouldBe(command.Id.ToString());
	}

	#endregion

	#region MessageType Property Tests

	[Fact]
	public void MessageType_ReturnsFullTypeName()
	{
		// Arrange
		var command = new TestCommand(Guid.NewGuid());

		// Act
		var messageType = command.MessageType;

		// Assert
		messageType.ShouldContain("TestCommand");
	}

	#endregion

	#region Kind Property Tests

	[Fact]
	public void Kind_ReturnsAction()
	{
		// Arrange
		var command = new TestCommand(Guid.NewGuid());

		// Act & Assert
		command.Kind.ShouldBe(MessageKinds.Action);
	}

	#endregion

	#region Headers Property Tests

	[Fact]
	public void Headers_ReturnsReadOnlyDictionary()
	{
		// Arrange
		var command = new TestCommand(Guid.NewGuid());

		// Act
		var headers = command.Headers;

		// Assert
		headers.ShouldNotBeNull();
		headers.Count.ShouldBe(0);
	}

	#endregion

	#region ActivityType Property Tests

	[Fact]
	public void ActivityType_ReturnsCommand()
	{
		// Arrange
		var command = new TestCommand(Guid.NewGuid());

		// Act
		var activityType = ((IActivity)command).ActivityType;

		// Assert
		activityType.ShouldBe(ActivityType.Command);
	}

	#endregion

	#region ActivityName Property Tests

	[Fact]
	public void ActivityName_ReturnsNamespaceAndTypeName()
	{
		// Arrange
		var command = new TestCommand(Guid.NewGuid());

		// Act
		var activityName = command.ActivityName;

		// Assert
		activityName.ShouldContain(":");
		activityName.ShouldContain("TestCommand");
	}

	#endregion

	#region Transaction Properties Tests

	[Fact]
	public void TransactionBehavior_DefaultsToRequired()
	{
		// Arrange
		var command = new TestCommand(Guid.NewGuid());

		// Act & Assert
		command.TransactionBehavior.ShouldBe(TransactionScopeOption.Required);
	}

	[Fact]
	public void TransactionIsolation_DefaultsToReadCommitted()
	{
		// Arrange
		var command = new TestCommand(Guid.NewGuid());

		// Act & Assert
		command.TransactionIsolation.ShouldBe(IsolationLevel.ReadCommitted);
	}

	[Fact]
	public void TransactionTimeout_DefaultsToOneMinute()
	{
		// Arrange
		var command = new TestCommand(Guid.NewGuid());

		// Act & Assert
		command.TransactionTimeout.ShouldBe(TimeSpan.FromMinutes(1));
	}

	#endregion

	#region ICommand Interface Tests

	[Fact]
	public void ImplementsICommand()
	{
		// Arrange & Act
		var command = new TestCommand(Guid.NewGuid());

		// Assert
		command.ShouldBeAssignableTo<ICommand>();
	}

	[Fact]
	public void ImplementsIActivity()
	{
		// Arrange & Act
		var command = new TestCommand(Guid.NewGuid());

		// Assert
		command.ShouldBeAssignableTo<IActivity>();
	}

	[Fact]
	public void ImplementsIAmCorrelatable()
	{
		// Arrange & Act
		var command = new TestCommand(Guid.NewGuid());

		// Assert
		command.ShouldBeAssignableTo<IAmCorrelatable>();
	}

	[Fact]
	public void ImplementsIAmMultiTenant()
	{
		// Arrange & Act
		var command = new TestCommand(Guid.NewGuid());

		// Assert
		command.ShouldBeAssignableTo<IAmMultiTenant>();
	}

	#endregion

	#region Test Implementations

	private sealed class TestCommand : CommandBase
	{
		public TestCommand(Guid correlationId, string? tenantId = null)
			: base(correlationId, tenantId)
		{
		}

		public override string ActivityDisplayName => "Test Command";
		public override string ActivityDescription => "A test command for unit testing";
	}

	private sealed class TestCommandWithDefaultConstructor : CommandBase
	{
		public override string ActivityDisplayName => "Test Command";
		public override string ActivityDescription => "A test command for unit testing";
	}

	#endregion
}
