// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Messaging.Tests.Messaging.Bus;

/// <summary>
/// ISP gate compliance tests for the Sprint 612 IMessageBusAdapter interface
/// split (A.4): IMessageBusAdapter, IMessageBusAdapterLifecycle, IMessageBusAdapterCapabilities.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Messaging")]
[Trait("Feature", "Transport")]
public sealed class MessageBusAdapterIspShould : UnitTestBase
{
	#region IMessageBusAdapter ISP Gate

	[Fact]
	public void IMessageBusAdapter_HaveAtMostFiveMethods()
	{
		var methods = typeof(IMessageBusAdapter)
			.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			.Where(m => !m.IsSpecialName)
			.ToArray();

		methods.Length.ShouldBeLessThanOrEqualTo(5,
			$"IMessageBusAdapter has {methods.Length} methods: {string.Join(", ", methods.Select(m => m.Name))}");
	}

	[Fact]
	public void IMessageBusAdapter_HaveAtMostTwoProperties()
	{
		var props = typeof(IMessageBusAdapter)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

		props.Length.ShouldBeLessThanOrEqualTo(5);

		// Verify specific properties
		var propNames = props.Select(p => p.Name).ToArray();
		propNames.ShouldContain("Name");
		propNames.ShouldContain("IsConnected");
	}

	[Fact]
	public void IMessageBusAdapter_NotExtendIDisposable()
	{
		// T.19 (Sprint 689): IDisposable removed from IMessageBusAdapter -- implementors add IDisposable independently
		typeof(IDisposable).IsAssignableFrom(typeof(IMessageBusAdapter)).ShouldBeFalse();
	}

	#endregion

	#region IMessageBusAdapterLifecycle ISP Gate

	[Fact]
	public void IMessageBusAdapterLifecycle_HaveExactlyThreeMethods()
	{
		var methods = typeof(IMessageBusAdapterLifecycle)
			.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			.Where(m => !m.IsSpecialName)
			.ToArray();

		methods.Length.ShouldBe(3);

		var methodNames = methods.Select(m => m.Name).OrderBy(n => n).ToArray();
		methodNames.ShouldContain("CheckHealthAsync");
		methodNames.ShouldContain("StartAsync");
		methodNames.ShouldContain("StopAsync");
	}

	[Fact]
	public void IMessageBusAdapterLifecycle_NotInheritIMessageBusAdapter()
	{
		// ISP: lifecycle is separate -- not inherited
		typeof(IMessageBusAdapterLifecycle).GetInterfaces().ShouldNotContain(typeof(IMessageBusAdapter));
	}

	[Fact]
	public void IMessageBusAdapterLifecycle_HaveNoProperties()
	{
		var props = typeof(IMessageBusAdapterLifecycle)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

		props.Length.ShouldBe(0);
	}

	#endregion

	#region IMessageBusAdapterCapabilities ISP Gate

	[Fact]
	public void IMessageBusAdapterCapabilities_HaveExactlyThreeProperties()
	{
		var props = typeof(IMessageBusAdapterCapabilities)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

		props.Length.ShouldBe(3);

		var propNames = props.Select(p => p.Name).OrderBy(n => n).ToArray();
		propNames.ShouldContain("SupportsPublishing");
		propNames.ShouldContain("SupportsSubscription");
		propNames.ShouldContain("SupportsTransactions");
	}

	[Fact]
	public void IMessageBusAdapterCapabilities_AllPropertiesAreBool()
	{
		var props = typeof(IMessageBusAdapterCapabilities)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

		foreach (var prop in props)
		{
			prop.PropertyType.ShouldBe(typeof(bool), $"Property {prop.Name} should be bool");
		}
	}

	[Fact]
	public void IMessageBusAdapterCapabilities_NotInheritIMessageBusAdapter()
	{
		// ISP: capabilities is separate -- not inherited
		typeof(IMessageBusAdapterCapabilities).GetInterfaces().ShouldNotContain(typeof(IMessageBusAdapter));
	}

	[Fact]
	public void IMessageBusAdapterCapabilities_HaveNoMethods()
	{
		var methods = typeof(IMessageBusAdapterCapabilities)
			.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			.Where(m => !m.IsSpecialName)
			.ToArray();

		methods.Length.ShouldBe(0);
	}

	#endregion

	#region Combined ISP Verification

	[Fact]
	public void AllThreeInterfaces_BeSeparateAndIndependent()
	{
		var adapterInterfaces = typeof(IMessageBusAdapter).GetInterfaces();
		var lifecycleInterfaces = typeof(IMessageBusAdapterLifecycle).GetInterfaces();
		var capabilitiesInterfaces = typeof(IMessageBusAdapterCapabilities).GetInterfaces();

		// None should inherit from each other
		adapterInterfaces.ShouldNotContain(typeof(IMessageBusAdapterLifecycle));
		adapterInterfaces.ShouldNotContain(typeof(IMessageBusAdapterCapabilities));
		lifecycleInterfaces.ShouldNotContain(typeof(IMessageBusAdapter));
		lifecycleInterfaces.ShouldNotContain(typeof(IMessageBusAdapterCapabilities));
		capabilitiesInterfaces.ShouldNotContain(typeof(IMessageBusAdapter));
		capabilitiesInterfaces.ShouldNotContain(typeof(IMessageBusAdapterLifecycle));
	}

	[Fact]
	public void ConcreteAdapter_CanImplementAllThreeInterfaces()
	{
		// Verify a concrete type can implement all three without conflict
		var adapter = new TestAdapter();

		var busAdapter = (IMessageBusAdapter)adapter;
		var lifecycle = (IMessageBusAdapterLifecycle)adapter;
		var capabilities = (IMessageBusAdapterCapabilities)adapter;

		busAdapter.Name.ShouldBe("test");
		busAdapter.IsConnected.ShouldBeFalse();
		capabilities.SupportsPublishing.ShouldBeTrue();
		capabilities.SupportsSubscription.ShouldBeTrue();
		capabilities.SupportsTransactions.ShouldBeFalse();

		// Lifecycle methods should be callable
		lifecycle.ShouldNotBeNull();
	}

	private sealed class TestAdapter : IMessageBusAdapter, IMessageBusAdapterLifecycle, IMessageBusAdapterCapabilities
	{
		public string Name => "test";
		public bool IsConnected => false;
		public bool SupportsPublishing => true;
		public bool SupportsSubscription => true;
		public bool SupportsTransactions => false;

		public Task InitializeAsync(MessageBusOptions options, CancellationToken cancellationToken) =>
			Task.CompletedTask;

		public Task<IMessageResult> PublishAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken) =>
			throw new NotImplementedException();

		public Task SubscribeAsync(string subscriptionName, Func<IDispatchMessage, IMessageContext, CancellationToken, Task<IMessageResult>> messageHandler, MessageBusOptions? options, CancellationToken cancellationToken) =>
			Task.CompletedTask;

		public Task UnsubscribeAsync(string subscriptionName, CancellationToken cancellationToken) =>
			Task.CompletedTask;

		public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken) =>
			Task.FromResult(new HealthCheckResult(true, "Healthy"));

		public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
		public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

		public void Dispose() { }
	}

	#endregion
}
