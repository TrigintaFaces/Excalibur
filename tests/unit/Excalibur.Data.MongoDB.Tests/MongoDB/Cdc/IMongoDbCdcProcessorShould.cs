// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using Excalibur.Cdc;
using Excalibur.Cdc.MongoDB;

namespace Excalibur.Data.Tests.MongoDB.Cdc;

/// <summary>
/// Unit tests for the <see cref="IMongoDbCdcProcessor"/> interface.
/// Tests verify interface definition, hierarchy, and inherited member signatures.
/// After CDC ISP unification (S820), IMongoDbCdcProcessor is a marker interface
/// inheriting all methods from ICdcStreamProcessor&lt;TEvent, TPosition&gt;.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "MongoDB")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class IMongoDbCdcProcessorShould
{
	private readonly Type _interfaceType = typeof(IMongoDbCdcProcessor);

	/// <summary>
	/// Gets a method from the interface or any of its inherited interfaces.
	/// Required because .NET reflection does not traverse interface hierarchies
	/// with a simple GetMethod call on the derived marker interface.
	/// </summary>
	private static MethodInfo? GetInterfaceMethod(Type interfaceType, string name)
	{
		var method = interfaceType.GetMethod(name);
		if (method is not null)
			return method;

		foreach (var parent in interfaceType.GetInterfaces())
		{
			method = parent.GetMethod(name);
			if (method is not null)
				return method;
		}

		return null;
	}

	#region Interface Definition Tests

	[Fact]
	public void BeAnInterface()
	{
		// Assert
		_interfaceType.IsInterface.ShouldBeTrue();
	}

	[Fact]
	public void BeAMarkerInterfaceWithNoDeclaredMethods()
	{
		// After CDC ISP unification, this is a marker interface —
		// all methods are inherited from ICdcStreamProcessor<T, TPos>
		var declaredMethods = _interfaceType.GetMethods()
			.Where(m => m.DeclaringType == _interfaceType)
			.ToList();

		declaredMethods.Count.ShouldBe(0);
	}

	[Fact]
	public void InheritFromICdcStreamProcessor()
	{
		// Assert
		typeof(ICdcStreamProcessor<MongoDbDataChangeEvent, MongoDbCdcPosition>)
			.IsAssignableFrom(_interfaceType).ShouldBeTrue();
	}

	[Fact]
	public void InheritFromICdcProcessor()
	{
		// Assert
		typeof(ICdcProcessor<MongoDbDataChangeEvent>)
			.IsAssignableFrom(_interfaceType).ShouldBeTrue();
	}

	[Fact]
	public void InheritFromIAsyncDisposable()
	{
		// Assert
		typeof(IAsyncDisposable).IsAssignableFrom(_interfaceType).ShouldBeTrue();
	}

	[Fact]
	public void InheritFromIDisposable()
	{
		// Assert
		typeof(IDisposable).IsAssignableFrom(_interfaceType).ShouldBeTrue();
	}

	#endregion

	#region Inherited Method Tests

	[Fact]
	public void HaveStartAsyncMethod()
	{
		// Arrange — method inherited from ICdcStreamProcessor
		var method = GetInterfaceMethod(_interfaceType, "StartAsync");

		// Assert
		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(Task));
	}

	[Fact]
	public void StartAsyncMethod_HasCorrectParameters()
	{
		// Arrange
		var method = GetInterfaceMethod(_interfaceType, "StartAsync");
		var parameters = method!.GetParameters();

		// Assert
		parameters.Length.ShouldBe(2);
		parameters[0].ParameterType.ShouldBe(typeof(Func<MongoDbDataChangeEvent, CancellationToken, Task>));
		parameters[1].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	[Fact]
	public void HaveProcessBatchAsyncMethod()
	{
		// Arrange — method inherited from ICdcProcessor
		var method = GetInterfaceMethod(_interfaceType, "ProcessBatchAsync");

		// Assert
		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(Task<int>));
	}

	[Fact]
	public void ProcessBatchAsyncMethod_HasCorrectParameters()
	{
		// Arrange
		var method = GetInterfaceMethod(_interfaceType, "ProcessBatchAsync");
		var parameters = method!.GetParameters();

		// Assert
		parameters.Length.ShouldBe(2);
		parameters[0].ParameterType.ShouldBe(typeof(Func<MongoDbDataChangeEvent, CancellationToken, Task>));
		parameters[1].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	[Fact]
	public void HaveGetCurrentPositionAsyncMethod()
	{
		// Arrange — method inherited from ICdcStreamProcessor
		var method = GetInterfaceMethod(_interfaceType, "GetCurrentPositionAsync");

		// Assert
		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(Task<MongoDbCdcPosition>));
	}

	[Fact]
	public void GetCurrentPositionAsyncMethod_HasCorrectParameters()
	{
		// Arrange
		var method = GetInterfaceMethod(_interfaceType, "GetCurrentPositionAsync");
		var parameters = method!.GetParameters();

		// Assert
		parameters.Length.ShouldBe(1);
		parameters[0].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	[Fact]
	public void HaveConfirmPositionAsyncMethod()
	{
		// Arrange — method inherited from ICdcStreamProcessor
		var method = GetInterfaceMethod(_interfaceType, "ConfirmPositionAsync");

		// Assert
		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(Task));
	}

	[Fact]
	public void ConfirmPositionAsyncMethod_HasCorrectParameters()
	{
		// Arrange
		var method = GetInterfaceMethod(_interfaceType, "ConfirmPositionAsync");
		var parameters = method!.GetParameters();

		// Assert
		parameters.Length.ShouldBe(2);
		parameters[0].ParameterType.ShouldBe(typeof(MongoDbCdcPosition));
		parameters[1].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	#endregion

	#region Total Method Count Tests

	[Fact]
	public void ExposeFourMethodsThroughInheritance()
	{
		// All 4 methods are inherited: StartAsync, ProcessBatchAsync,
		// GetCurrentPositionAsync, ConfirmPositionAsync
		var allMethods = _interfaceType.GetInterfaces()
			.Where(i => i != typeof(IAsyncDisposable) && i != typeof(IDisposable))
			.SelectMany(i => i.GetMethods().Where(m => m.DeclaringType == i))
			.ToList();

		allMethods.Count.ShouldBe(4);
	}

	#endregion
}
