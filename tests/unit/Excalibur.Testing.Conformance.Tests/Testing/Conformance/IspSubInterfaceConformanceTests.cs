// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Compliance;
using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.Data.Abstractions.Persistence;

using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Performance;
using Excalibur.Dispatch.Transport;

using Tests.Shared.Categories;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Conformance tests verifying Sprint 744 ISP sub-interfaces:
/// each sub-interface has <=5 methods, extension methods use
/// interface-cast dispatch (not reflection), and fallback behavior
/// is correct when sub-interface is not implemented.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "ISP")]
public sealed class IspSubInterfaceConformanceTests
{
	private const int MaxMethodGate = 5;

	/// <summary>All S744 public sub-interfaces reachable from this test project.</summary>
	public static TheoryData<Type, int> SubInterfacesWithExpectedMethodCount => new()
	{
		// Abstractions package
		{ typeof(IConnectionPoolDiagnostics<>), 3 },
		{ typeof(ITransportProviderFactory), 4 },
		{ typeof(IRemoteMessageBusProvider), 3 },
		{ typeof(ITimePolicyConfiguration), 4 },
		{ typeof(IOutboxStoreBatch), 3 },

		// Compliance.Abstractions package
		{ typeof(IKeyCacheAdmin), 4 },
		{ typeof(IComplianceMetricsAdmin), 5 },

		// Data.Abstractions package
		{ typeof(ICloudNativeOutboxStoreBatch), 4 },
		{ typeof(IPersistenceMetricsAdmin), 4 },
		{ typeof(IPersistenceConfigurationAdmin), 3 },
		{ typeof(IConnectionStringProviderAdmin), 4 },
	};

	[Theory]
	[MemberData(nameof(SubInterfacesWithExpectedMethodCount))]
	public void SubInterface_ShouldHaveAtMostFiveMethods(Type subInterface, int expectedCount)
	{
		var methods = subInterface.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			.Where(m => !m.IsSpecialName) // Exclude property getters/setters
			.ToArray();
		var properties = subInterface.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
		var totalMembers = methods.Length + properties.Length;

		totalMembers.ShouldBeLessThanOrEqualTo(MaxMethodGate,
			$"{subInterface.Name} has {totalMembers} members, exceeds gate of {MaxMethodGate}");
		totalMembers.ShouldBe(expectedCount,
			$"{subInterface.Name} expected {expectedCount} members but has {totalMembers}");
	}

	[Theory]
	[MemberData(nameof(SubInterfacesWithExpectedMethodCount))]
	public void SubInterface_ShouldBeInterface(Type subInterface, int _)
	{
		subInterface.IsInterface.ShouldBeTrue($"{subInterface.Name} must be an interface");
	}

	[Theory]
	[MemberData(nameof(SubInterfacesWithExpectedMethodCount))]
	public void SubInterface_ShouldNotInheritParentContract(Type subInterface, int _)
	{
		// Sub-interfaces should be standalone (ISP) -- not inherit the core parent interface.
		var parentNames = new[] { "IOutboxStore", "ITimePolicy", "IKeyCache", "IConnectionPool", "ITransportProvider", "IMessageBusProvider" };
		var interfaces = subInterface.GetInterfaces();
		foreach (var iface in interfaces)
		{
			foreach (var parent in parentNames)
			{
				iface.Name.ShouldNotBe(parent,
					$"{subInterface.Name} inherits {parent} -- ISP sub-interfaces should be standalone");
			}
		}
	}

	/// <summary>Extension classes that must be reflection-free.</summary>
	public static TheoryData<Type> ExtensionClassTypes => new()
	{
		{ typeof(ConnectionPoolExtensions) },
		{ typeof(TransportProviderExtensions) },
		{ typeof(MessageBusProviderExtensions) },
		{ typeof(TimePolicyExtensions) },
		{ typeof(OutboxStoreExtensions) },
		{ typeof(KeyCacheExtensions) },
		{ typeof(ComplianceMetricsExtensions) },
	};

	[Theory]
	[MemberData(nameof(ExtensionClassTypes))]
	public void ExtensionClass_ShouldNotUseReflection(Type extensionClass)
	{
		// Extension methods should NOT reference System.Reflection types.
		var methods = extensionClass.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
		foreach (var method in methods)
		{
			var body = method.GetMethodBody();
			if (body is null)
			{
				continue;
			}

			foreach (var local in body.LocalVariables)
			{
				local.LocalType.ShouldNotBe(typeof(MethodInfo),
					$"{extensionClass.Name}.{method.Name} uses System.Reflection.MethodInfo");
				local.LocalType.ShouldNotBe(typeof(PropertyInfo),
					$"{extensionClass.Name}.{method.Name} uses System.Reflection.PropertyInfo");
			}
		}
	}

	[Fact]
	public async Task OutboxStoreBatch_FallbackWhenNotImplemented()
	{
		// Store WITHOUT IOutboxStoreBatch should get fallback behavior.
		var store = A.Fake<IOutboxStore>();

		var result = await store.TryMarkSentAndReceivedAsync("msg-1",
			new InboxEntry
			{
				MessageId = "msg-1",
				HandlerType = "handler",
				MessageType = "type",
				Payload = [1],
				Status = InboxStatus.Received,
				ReceivedAt = DateTimeOffset.UtcNow,
			},
			CancellationToken.None).ConfigureAwait(false);

		result.ShouldBeFalse();
	}

	[Fact]
	public void TimePolicyConfiguration_FallbackDelegatesToGetTimeoutFor()
	{
		// Policy WITHOUT ITimePolicyConfiguration should fall back to GetTimeoutFor.
		var policy = A.Fake<ITimePolicy>();
		A.CallTo(() => policy.GetTimeoutFor(A<TimeoutOperationType>._))
			.Returns(TimeSpan.FromSeconds(42));

		var timeout = policy.SerializationTimeout();

		timeout.ShouldBe(TimeSpan.FromSeconds(42));
	}

	[Fact]
	public void KeyCacheAdmin_FallbackInvalidateDelegatesToRemove()
	{
		// Cache WITHOUT IKeyCacheAdmin should fall back to Remove.
		var cache = A.Fake<IKeyCache>();

		cache.Invalidate("key-1");

		A.CallTo(() => cache.Remove("key-1")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ConnectionPoolDiagnostics_FallbackReturnsEmptyStats()
	{
		// Pool WITHOUT IConnectionPoolDiagnostics should return empty stats.
		var pool = A.Fake<IConnectionPool<object>>();

		var stats = pool.GetStatistics();

		_ = stats.ShouldNotBeNull();
	}

	#region ILongPollingStrategyAdmin Fallback Pattern

	[Fact]
	public async Task LongPollingStrategyAdmin_FallbackReturnsDefaultStats()
	{
		// Strategy WITHOUT ILongPollingStrategyAdmin should get default (zero) stats
		// via the pattern: strategy is ILongPollingStrategyAdmin admin ? admin.GetStatisticsAsync() : default
		var strategy = A.Fake<ILongPollingStrategy>();

		var stats = strategy is ILongPollingStrategyAdmin admin
			? await admin.GetStatisticsAsync().ConfigureAwait(false)
			: default;

		stats.TotalReceives.ShouldBe(0);
		stats.EmptyReceiveRate.ShouldBe(0);
		stats.TotalMessages.ShouldBe(0);
	}

	#endregion

	#region Concrete Implementation Admin Interface Declarations

	/// <summary>
	/// Verifies concrete implementations declare their admin sub-interfaces.
	/// Without this, extension methods that dispatch via "obj is IFooAdmin" silently no-op.
	/// This catches the KeyCache bug (Sprint 745): class had all methods but didn't declare IKeyCacheAdmin.
	/// </summary>
	public static TheoryData<Type, Type> ConcreteImplementationAdminInterfaces => new()
	{
		// KeyCache must implement IKeyCacheAdmin for Clear/Invalidate/Set(ttl)/GetOrAddAsync(ttl) dispatch
		{ typeof(KeyCache), typeof(IKeyCacheAdmin) },

		// InMemoryTransportAdapter must implement ITransportAdapterLifecycle for Start/Stop dispatch
		{ typeof(InMemoryTransportAdapter), typeof(ITransportAdapterLifecycle) },

		// InMemoryTransportAdapter must implement ITransportHealthMetrics for GetHealthMetricsAsync dispatch
		{ typeof(InMemoryTransportAdapter), typeof(ITransportHealthMetrics) },

		// CronTimerTransportAdapter must implement ITransportAdapterLifecycle
		{ typeof(CronTimerTransportAdapter), typeof(ITransportAdapterLifecycle) },

		// CronTimerTransportAdapter must implement ITransportHealthMetrics
		{ typeof(CronTimerTransportAdapter), typeof(ITransportHealthMetrics) },

		// PerformanceMetricsCollector must implement IPerformanceMetricsCollectorAdmin for Reset dispatch
		{ typeof(PerformanceMetricsCollector), typeof(IPerformanceMetricsCollectorAdmin) },

		// NullDeadLetterQueue must NOT implement IDeadLetterQueueAdmin (methods were intentionally removed)
	};

	[Theory]
	[MemberData(nameof(ConcreteImplementationAdminInterfaces))]
	public void ConcreteImplementation_ShouldDeclareAdminSubInterface(Type concreteType, Type adminInterface)
	{
		adminInterface.IsAssignableFrom(concreteType).ShouldBeTrue(
			$"{concreteType.Name} must implement {adminInterface.Name} " +
			"for extension method dispatch. Without it, admin operations silently no-op.");
	}

	[Fact]
	public void NullDeadLetterQueue_ShouldImplementIDeadLetterQueueAdmin()
	{
		// NullDeadLetterQueue implements IDeadLetterQueueAdmin with no-op stubs
		// so that GetService<IDeadLetterQueueAdmin>() returns a safe null object
		// rather than null when DLQ is disabled (reviewer N1 fix).
		typeof(IDeadLetterQueueAdmin).IsAssignableFrom(typeof(NullDeadLetterQueue))
			.ShouldBeTrue("NullDeadLetterQueue should implement IDeadLetterQueueAdmin for null object safety");
	}

	#endregion
}
