// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class TransportBindingRegistryShould : UnitTestBase
{
	[Fact]
	public void ReturnHighestPriorityMatch_WhenMultipleBindingsMatchEndpoint()
	{
		var registry = new TransportBindingRegistry();
		var low = new TestBinding("low", "orders", priority: 10);
		var high = new TestBinding("high", "orders", priority: 100);

		registry.RegisterBinding(low);
		registry.RegisterBinding(high);

		var selected = registry.FindBinding("orders");

		selected.ShouldBe(high);
	}

	[Fact]
	public void ReturnSnapshotFromGetBindings_OrderedByPriority()
	{
		var registry = new TransportBindingRegistry();
		registry.RegisterBinding(new TestBinding("p10", "a", priority: 10));
		registry.RegisterBinding(new TestBinding("p50", "b", priority: 50));
		registry.RegisterBinding(new TestBinding("p30", "c", priority: 30));

		var bindings = registry.GetBindings();

		bindings.Count.ShouldBe(3);
		bindings[0].Name.ShouldBe("p50");
		bindings[1].Name.ShouldBe("p30");
		bindings[2].Name.ShouldBe("p10");
	}

	[Fact]
	public void ReturnStableSnapshot_WhenBindingsChangeAfterRead()
	{
		var registry = new TransportBindingRegistry();
		registry.RegisterBinding(new TestBinding("first", "orders", priority: 10));
		var snapshot = registry.GetBindings();

		registry.RegisterBinding(new TestBinding("second", "orders", priority: 20));

		snapshot.Count.ShouldBe(1);
		snapshot[0].Name.ShouldBe("first");

		var latest = registry.GetBindings();
		latest.Count.ShouldBe(2);
	}

	[Fact]
	public void UpdateSnapshotAfterRemoveBinding()
	{
		var registry = new TransportBindingRegistry();
		var first = new TestBinding("first", "orders", priority: 10);
		var second = new TestBinding("second", "orders", priority: 20);

		registry.RegisterBinding(first);
		registry.RegisterBinding(second);

		registry.RemoveBinding("second").ShouldBeTrue();

		var bindings = registry.GetBindings();
		bindings.Count.ShouldBe(1);
		bindings[0].Name.ShouldBe("first");
	}

	[Fact]
	public void Throw_WhenDuplicateBindingNameRegistered()
	{
		var registry = new TransportBindingRegistry();
		registry.RegisterBinding(new TestBinding("duplicate", "orders", priority: 10));

		_ = Should.Throw<InvalidOperationException>(
			() => registry.RegisterBinding(new TestBinding("duplicate", "orders", priority: 20)));
	}

	private sealed class TestBinding(string name, string endpointPattern, int priority) : ITransportBinding, ITransportBindingRouting
	{
		public string Name { get; } = name;
		public ITransportAdapter TransportAdapter { get; } = null!;
		public string EndpointPattern { get; } = endpointPattern;
		public IPipelineProfile? PipelineProfile { get; }
		public MessageKinds AcceptedMessageKinds { get; } = MessageKinds.All;
		public int Priority { get; } = priority;

		public bool Matches(string endpoint) => string.Equals(EndpointPattern, endpoint, StringComparison.Ordinal);
	}
}
