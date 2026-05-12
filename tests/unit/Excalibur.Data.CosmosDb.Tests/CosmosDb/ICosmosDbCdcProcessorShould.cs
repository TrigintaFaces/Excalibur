// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.CosmosDb;

namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Unit tests for the <see cref="ICosmosDbCdcProcessor"/> interface.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.3): CosmosDB unit tests.
/// Tests verify interface definition and member signatures.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "CosmosDb")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class ICosmosDbCdcProcessorShould
{
	private readonly Type _interfaceType = typeof(ICosmosDbCdcProcessor);

	#region Interface Definition Tests

	[Fact]
	public void BeAnInterface()
	{
		// Assert
		_interfaceType.IsInterface.ShouldBeTrue();
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

	#region ISP Hierarchy Tests (Sprint 820)

	[Fact]
	public void InheritFromICdcStreamProcessor()
	{
		// Assert — ICosmosDbCdcProcessor : ICdcStreamProcessor<CosmosDbDataChangeEvent, CosmosDbCdcPosition>
		typeof(Excalibur.Cdc.ICdcStreamProcessor<CosmosDbDataChangeEvent, CosmosDbCdcPosition>)
			.IsAssignableFrom(_interfaceType).ShouldBeTrue();
	}

	[Fact]
	public void InheritFromICdcProcessor()
	{
		// Assert — transitive: ICdcStreamProcessor<T, TPos> : ICdcProcessor<T>
		typeof(Excalibur.Cdc.ICdcProcessor<CosmosDbDataChangeEvent>)
			.IsAssignableFrom(_interfaceType).ShouldBeTrue();
	}

	[Fact]
	public void HaveZeroDeclaredMethods()
	{
		// Assert — marker interface, all methods inherited from base interfaces
		var methods = _interfaceType.GetMethods(
				System.Reflection.BindingFlags.Public |
				System.Reflection.BindingFlags.Instance |
				System.Reflection.BindingFlags.DeclaredOnly)
			.ToList();

		methods.Count.ShouldBe(0);
	}

	#endregion

	#region Inherited Method Accessibility Tests

	[Fact]
	public void ExposeStartAsync_ViaBaseInterface()
	{
		var streamType = typeof(Excalibur.Cdc.ICdcStreamProcessor<CosmosDbDataChangeEvent, CosmosDbCdcPosition>);
		var method = streamType.GetMethod("StartAsync");
		method.ShouldNotBeNull();
		method!.ReturnType.ShouldBe(typeof(Task));
	}

	[Fact]
	public void ExposeProcessBatchAsync_ViaBaseInterface()
	{
		var baseType = typeof(Excalibur.Cdc.ICdcProcessor<CosmosDbDataChangeEvent>);
		var method = baseType.GetMethod("ProcessBatchAsync");
		method.ShouldNotBeNull();
		method!.ReturnType.ShouldBe(typeof(Task<int>));
	}

	[Fact]
	public void ExposeGetCurrentPositionAsync_ViaBaseInterface()
	{
		var streamType = typeof(Excalibur.Cdc.ICdcStreamProcessor<CosmosDbDataChangeEvent, CosmosDbCdcPosition>);
		var method = streamType.GetMethod("GetCurrentPositionAsync");
		method.ShouldNotBeNull();
		method!.ReturnType.ShouldBe(typeof(Task<CosmosDbCdcPosition>));
	}

	[Fact]
	public void ExposeConfirmPositionAsync_ViaBaseInterface()
	{
		var streamType = typeof(Excalibur.Cdc.ICdcStreamProcessor<CosmosDbDataChangeEvent, CosmosDbCdcPosition>);
		var method = streamType.GetMethod("ConfirmPositionAsync");
		method.ShouldNotBeNull();
		method!.ReturnType.ShouldBe(typeof(Task));
	}

	#endregion
}
