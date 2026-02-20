// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Testing.Builders;

namespace Excalibur.Dispatch.Testing.Tests.Builders;

[Trait("Category", "Unit")]
[Trait("Component", "Testing")]
public sealed class SagaStateBuilderShould
{
	[Fact]
	public void BuildWithDefaults()
	{
		var state = new SagaStateBuilder<TestSagaState>().Build();
		state.ShouldNotBeNull();
		state.SagaId.ShouldNotBe(Guid.Empty);
		state.Completed.ShouldBeFalse();
	}

	[Fact]
	public void SetSagaId()
	{
		var id = Guid.NewGuid();
		var state = new SagaStateBuilder<TestSagaState>()
			.WithSagaId(id)
			.Build();

		state.SagaId.ShouldBe(id);
	}

	[Fact]
	public void SetCompleted()
	{
		var state = new SagaStateBuilder<TestSagaState>()
			.WithCompleted(true)
			.Build();

		state.Completed.ShouldBeTrue();
	}

	[Fact]
	public void ApplyConfiguration()
	{
		var state = new SagaStateBuilder<TestSagaState>()
			.Configure(s => s.OrderId = "order-123")
			.Build();

		state.OrderId.ShouldBe("order-123");
	}

	[Fact]
	public void AccumulateMultipleConfigurations()
	{
		var state = new SagaStateBuilder<TestSagaState>()
			.Configure(s => s.OrderId = "order-1")
			.Configure(s => s.Step = "prepared")
			.Build();

		state.OrderId.ShouldBe("order-1");
		state.Step.ShouldBe("prepared");
	}

	[Fact]
	public void ThrowOnNullConfigure()
	{
		var builder = new SagaStateBuilder<TestSagaState>();
		Should.Throw<ArgumentNullException>(() => builder.Configure(null!));
	}

	[Fact]
	public void BuildManyWithUniqueIds()
	{
		var states = new SagaStateBuilder<TestSagaState>()
			.WithCompleted(false)
			.BuildMany(5);

		states.Count.ShouldBe(5);
		states.Select(s => s.SagaId).Distinct().Count().ShouldBe(5);
	}

	[Fact]
	public void BuildManyAppliesConfiguration()
	{
		var states = new SagaStateBuilder<TestSagaState>()
			.Configure(s => s.OrderId = "shared-order")
			.BuildMany(3);

		states.ShouldAllBe(s => s.OrderId == "shared-order");
	}

	[Fact]
	public void BuildManyAppliesCompletedFlag()
	{
		var states = new SagaStateBuilder<TestSagaState>()
			.WithCompleted(true)
			.BuildMany(2);

		states.ShouldAllBe(s => s.Completed);
	}

	[Fact]
	public void SupportFluentChaining()
	{
		var id = Guid.NewGuid();
		var state = new SagaStateBuilder<TestSagaState>()
			.WithSagaId(id)
			.WithCompleted(true)
			.Configure(s => s.OrderId = "chained")
			.Build();

		state.SagaId.ShouldBe(id);
		state.Completed.ShouldBeTrue();
		state.OrderId.ShouldBe("chained");
	}

	private sealed class TestSagaState : SagaState
	{
		public string? OrderId { get; set; }
		public string? Step { get; set; }
	}
}
