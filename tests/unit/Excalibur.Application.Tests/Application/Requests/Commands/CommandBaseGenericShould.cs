// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Application.Requests.Commands;

namespace Excalibur.Tests.Application.Requests.Commands;

/// <summary>
/// Unit tests for <see cref="CommandBase{TResponse}"/> generic variant.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Application")]
public sealed class CommandBaseGenericShould
{
	[Fact]
	public void ImplementICommandGeneric()
	{
		var command = new TestTypedCommand(Guid.NewGuid());
		command.ShouldBeAssignableTo<ICommand<string>>();
	}

	[Fact]
	public void InheritFromCommandBase()
	{
		var command = new TestTypedCommand(Guid.NewGuid());
		command.ShouldBeAssignableTo<CommandBase>();
	}

	[Fact]
	public void PropagateCorrelationId()
	{
		var correlationId = Guid.NewGuid();
		var command = new TestTypedCommand(correlationId);
		((IAmCorrelatable)command).CorrelationId.ShouldBe(correlationId);
	}

	[Fact]
	public void PropagateTenantId()
	{
		var command = new TestTypedCommand(Guid.NewGuid(), "tenant-abc");
		command.TenantId.ShouldBe("tenant-abc");
	}

	[Fact]
	public void DefaultConstructor_SetsEmptyCorrelationId()
	{
		var command = new TestTypedCommandDefault();
		((IAmCorrelatable)command).CorrelationId.ShouldBe(Guid.Empty);
	}

	[Fact]
	public void HaveUniqueId()
	{
		var command = new TestTypedCommand(Guid.NewGuid());
		command.Id.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void ReturnActionKind()
	{
		var command = new TestTypedCommand(Guid.NewGuid());
		command.Kind.ShouldBe(MessageKinds.Action);
	}

	[Fact]
	public void ImplementIActivity()
	{
		var command = new TestTypedCommand(Guid.NewGuid());
		command.ShouldBeAssignableTo<IActivity>();
	}

	[Fact]
	public void ReturnCommandActivityType()
	{
		var command = new TestTypedCommand(Guid.NewGuid());
		((IActivity)command).ActivityType.ShouldBe(ActivityType.Command);
	}

	#region Test Types

	private sealed class TestTypedCommand : CommandBase<string>
	{
		public TestTypedCommand(Guid correlationId, string? tenantId = null)
			: base(correlationId, tenantId)
		{
		}
	}

	private sealed class TestTypedCommandDefault : CommandBase<int>
	{
	}

	#endregion
}
