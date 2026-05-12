// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.Firestore;

namespace Excalibur.Data.Tests.Firestore.Cdc;

/// <summary>
/// Sprint 623 B.1: Unit tests for the <see cref="IFirestoreCdcProcessor"/> interface.
/// Tests verify interface definition and member signatures.
/// Follows the CosmosDb/DynamoDb processor interface test pattern.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Firestore")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class IFirestoreCdcProcessorShould
{
	private readonly Type _interfaceType = typeof(IFirestoreCdcProcessor);

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
		// Assert — IFirestoreCdcProcessor : ICdcStreamProcessor<FirestoreDataChangeEvent, FirestoreCdcPosition>
		typeof(Excalibur.Cdc.ICdcStreamProcessor<FirestoreDataChangeEvent, FirestoreCdcPosition>)
			.IsAssignableFrom(_interfaceType).ShouldBeTrue();
	}

	[Fact]
	public void InheritFromICdcProcessor()
	{
		// Assert — transitive: ICdcStreamProcessor<T, TPos> : ICdcProcessor<T>
		typeof(Excalibur.Cdc.ICdcProcessor<FirestoreDataChangeEvent>)
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
		var streamType = typeof(Excalibur.Cdc.ICdcStreamProcessor<FirestoreDataChangeEvent, FirestoreCdcPosition>);
		var method = streamType.GetMethod("StartAsync");
		method.ShouldNotBeNull();
		method!.ReturnType.ShouldBe(typeof(Task));
	}

	[Fact]
	public void ExposeProcessBatchAsync_ViaBaseInterface()
	{
		var baseType = typeof(Excalibur.Cdc.ICdcProcessor<FirestoreDataChangeEvent>);
		var method = baseType.GetMethod("ProcessBatchAsync");
		method.ShouldNotBeNull();
		method!.ReturnType.ShouldBe(typeof(Task<int>));
	}

	[Fact]
	public void ExposeGetCurrentPositionAsync_ViaBaseInterface()
	{
		var streamType = typeof(Excalibur.Cdc.ICdcStreamProcessor<FirestoreDataChangeEvent, FirestoreCdcPosition>);
		var method = streamType.GetMethod("GetCurrentPositionAsync");
		method.ShouldNotBeNull();
		method!.ReturnType.ShouldBe(typeof(Task<FirestoreCdcPosition>));
	}

	[Fact]
	public void ExposeConfirmPositionAsync_ViaBaseInterface()
	{
		var streamType = typeof(Excalibur.Cdc.ICdcStreamProcessor<FirestoreDataChangeEvent, FirestoreCdcPosition>);
		var method = streamType.GetMethod("ConfirmPositionAsync");
		method.ShouldNotBeNull();
		method!.ReturnType.ShouldBe(typeof(Task));
	}

	#endregion
}
