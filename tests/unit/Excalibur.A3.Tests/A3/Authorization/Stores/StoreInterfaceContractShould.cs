// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.A3.Abstractions.Authorization;

namespace Excalibur.Tests.A3.Authorization.Stores;

/// <summary>
/// Contract tests verifying the A3 store interfaces adhere to
/// the Microsoft-First Design Standard (max 5 methods per interface).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class StoreInterfaceContractShould
{
	private const int MaxMethodCount = 6; // 5 domain methods + GetService

	[Theory]
	[InlineData(typeof(IGrantStore), 6)]          // 5 CRUD + GetService
	[InlineData(typeof(IGrantQueryStore), 2)]      // 2 query methods
	[InlineData(typeof(IActivityGroupStore), 5)]   // 4 CRUD + GetService
	[InlineData(typeof(IActivityGroupGrantStore), 4)] // 4 bridging methods
	public void HaveExpectedMethodCount(Type interfaceType, int expectedCount)
	{
		// Act
		var methods = interfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

		// Assert
		methods.Length.ShouldBe(expectedCount,
			$"{interfaceType.Name} should have exactly {expectedCount} methods but has {methods.Length}");
	}

	[Theory]
	[InlineData(typeof(IGrantStore))]
	[InlineData(typeof(IGrantQueryStore))]
	[InlineData(typeof(IActivityGroupStore))]
	[InlineData(typeof(IActivityGroupGrantStore))]
	public void NotExceedMicrosoftMethodGate(Type interfaceType)
	{
		// Act
		var methods = interfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

		// Assert -- Microsoft-First Design Standard: <=5 methods + optional GetService
		methods.Length.ShouldBeLessThanOrEqualTo(MaxMethodCount,
			$"{interfaceType.Name} exceeds the <=5+GetService method gate ({methods.Length} methods)");
	}

	[Fact]
	public void HaveGetServiceEscapeHatch_OnIGrantStore()
	{
		// Verify IGrantStore has the GetService pattern
		var method = typeof(IGrantStore).GetMethod("GetService",
			BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

		method.ShouldNotBeNull("IGrantStore must have a GetService(Type) method");
		method.ReturnType.ShouldBe(typeof(object));
		method.GetParameters().Length.ShouldBe(1);
		method.GetParameters()[0].ParameterType.ShouldBe(typeof(Type));
	}

	[Fact]
	public void HaveGetServiceEscapeHatch_OnIActivityGroupStore()
	{
		// Verify IActivityGroupStore has the GetService pattern
		var method = typeof(IActivityGroupStore).GetMethod("GetService",
			BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

		method.ShouldNotBeNull("IActivityGroupStore must have a GetService(Type) method");
		method.ReturnType.ShouldBe(typeof(object));
		method.GetParameters().Length.ShouldBe(1);
		method.GetParameters()[0].ParameterType.ShouldBe(typeof(Type));
	}

	[Theory]
	[InlineData(typeof(IGrantStore))]
	[InlineData(typeof(IGrantQueryStore))]
	[InlineData(typeof(IActivityGroupStore))]
	[InlineData(typeof(IActivityGroupGrantStore))]
	public void BeInterface(Type type)
	{
		type.IsInterface.ShouldBeTrue($"{type.Name} should be an interface");
	}

	[Theory]
	[InlineData(typeof(IGrantStore))]
	[InlineData(typeof(IGrantQueryStore))]
	[InlineData(typeof(IActivityGroupStore))]
	[InlineData(typeof(IActivityGroupGrantStore))]
	public void BeInAuthorizationNamespace(Type type)
	{
		type.Namespace.ShouldBe("Excalibur.A3.Abstractions.Authorization",
			$"{type.Name} should be in Excalibur.A3.Abstractions.Authorization namespace");
	}

	[Theory]
	[InlineData(typeof(IGrantQueryStore))]
	[InlineData(typeof(IActivityGroupGrantStore))]
	public void NotInheritFromPrimaryStoreInterface(Type subInterfaceType)
	{
		// ISP sub-interfaces should NOT inherit from the primary store interfaces
		// They are accessed via GetService, not through inheritance
		subInterfaceType.GetInterfaces().ShouldNotContain(typeof(IGrantStore),
			$"{subInterfaceType.Name} should not inherit IGrantStore (accessed via GetService instead)");
		subInterfaceType.GetInterfaces().ShouldNotContain(typeof(IActivityGroupStore),
			$"{subInterfaceType.Name} should not inherit IActivityGroupStore (accessed via GetService instead)");
	}

	[Fact]
	public void HaveAllAsyncMethods_ReturnTask()
	{
		// All methods ending in Async should return Task or Task<T>
		var allStoreTypes = new[]
		{
			typeof(IGrantStore),
			typeof(IGrantQueryStore),
			typeof(IActivityGroupStore),
			typeof(IActivityGroupGrantStore),
		};

		foreach (var type in allStoreTypes)
		{
			var asyncMethods = type
				.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
				.Where(m => m.Name.EndsWith("Async", StringComparison.Ordinal));

			foreach (var method in asyncMethods)
			{
				var returnType = method.ReturnType;
				(returnType == typeof(Task) || returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
					.ShouldBeTrue($"{type.Name}.{method.Name} should return Task or Task<T>");
			}
		}
	}

	[Fact]
	public void HaveAllAsyncMethods_AcceptCancellationToken()
	{
		// All async methods should have CancellationToken as last parameter
		var allStoreTypes = new[]
		{
			typeof(IGrantStore),
			typeof(IGrantQueryStore),
			typeof(IActivityGroupStore),
			typeof(IActivityGroupGrantStore),
		};

		foreach (var type in allStoreTypes)
		{
			var asyncMethods = type
				.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
				.Where(m => m.Name.EndsWith("Async", StringComparison.Ordinal));

			foreach (var method in asyncMethods)
			{
				var parameters = method.GetParameters();
				parameters.Length.ShouldBeGreaterThan(0,
					$"{type.Name}.{method.Name} should have at least one parameter");

				var lastParam = parameters[^1];
				lastParam.ParameterType.ShouldBe(typeof(CancellationToken),
					$"{type.Name}.{method.Name} last parameter should be CancellationToken, not {lastParam.ParameterType.Name}");

				lastParam.HasDefaultValue.ShouldBeFalse(
					$"{type.Name}.{method.Name} CancellationToken should NOT have a default value (library convention)");
			}
		}
	}
}
