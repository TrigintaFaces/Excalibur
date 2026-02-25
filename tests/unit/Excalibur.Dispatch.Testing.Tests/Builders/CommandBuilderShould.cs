// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Testing;
using Excalibur.Dispatch.Testing.Builders;

namespace Excalibur.Dispatch.Testing.Tests.Builders;

[Trait("Category", "Unit")]
[Trait("Component", "Testing")]
public sealed class CommandBuilderShould
{
	[Fact]
	public void BuildWithDefaultCommand()
	{
		var (command, context) = new CommandBuilder<TestCommand>().Build();
		command.ShouldNotBeNull();
		context.ShouldNotBeNull();
	}

	[Fact]
	public void BuildWithExplicitCommand()
	{
		var cmd = new TestCommand { Name = "explicit" };
		var (command, _) = new CommandBuilder<TestCommand>()
			.WithCommand(cmd)
			.Build();

		command.Name.ShouldBe("explicit");
	}

	[Fact]
	public void SetCorrelationId()
	{
		var (_, context) = new CommandBuilder<TestCommand>()
			.WithCorrelationId("corr-123")
			.Build();

		context.CorrelationId.ShouldBe("corr-123");
	}

	[Fact]
	public void SetCausationId()
	{
		var (_, context) = new CommandBuilder<TestCommand>()
			.WithCausationId("cause-456")
			.Build();

		context.CausationId.ShouldBe("cause-456");
	}

	[Fact]
	public void SetTenantId()
	{
		var (_, context) = new CommandBuilder<TestCommand>()
			.WithTenantId("tenant-abc")
			.Build();

		context.TenantId.ShouldBe("tenant-abc");
	}

	[Fact]
	public void SetUserId()
	{
		var (_, context) = new CommandBuilder<TestCommand>()
			.WithUserId("user-1")
			.Build();

		context.UserId.ShouldBe("user-1");
	}

	[Fact]
	public void SetRequestServices()
	{
		var sp = new ServiceCollection().BuildServiceProvider();
		var (_, context) = new CommandBuilder<TestCommand>()
			.WithRequestServices(sp)
			.Build();

		context.RequestServices.ShouldBe(sp);
	}

	[Fact]
	public void SetContextItem()
	{
		var (_, context) = new CommandBuilder<TestCommand>()
			.WithContextItem("key1", "value1")
			.Build();

		context.Items["key1"].ShouldBe("value1");
	}

	[Fact]
	public void SupportFluentChaining()
	{
		var (command, context) = new CommandBuilder<TestCommand>()
			.WithCorrelationId("corr")
			.WithCausationId("cause")
			.WithTenantId("tenant")
			.WithUserId("user")
			.WithContextItem("k", "v")
			.Build();

		command.ShouldNotBeNull();
		context.CorrelationId.ShouldBe("corr");
		context.CausationId.ShouldBe("cause");
	}

	[Fact]
	public void BuildCommandOnly()
	{
		var command = new CommandBuilder<TestCommand>().BuildCommand();
		command.ShouldNotBeNull();
	}

	[Fact]
	public void BuildCommandOnlyWithExplicitInstance()
	{
		var cmd = new TestCommand { Name = "explicit" };
		var command = new CommandBuilder<TestCommand>()
			.WithCommand(cmd)
			.BuildCommand();

		command.Name.ShouldBe("explicit");
	}

	// TestCommand must NOT be nested public (CA1034), but CommandBuilder<T> requires
	// IDispatchAction + new(). Since it's in the same assembly, private nested works.
	private sealed class TestCommand : IDispatchAction
	{
		public string? Name { get; set; }
	}
}
