// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.DynamoDb;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for the <see cref="IDynamoDbCdcProcessor"/> interface.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Tests verify interface definition and member signatures.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class IDynamoDbCdcProcessorShould
{
	private readonly Type _interfaceType = typeof(IDynamoDbCdcProcessor);

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
		// Assert — IDynamoDbCdcProcessor : ICdcStreamProcessor<DynamoDbDataChangeEvent, DynamoDbCdcPosition>
		typeof(Excalibur.Cdc.ICdcStreamProcessor<DynamoDbDataChangeEvent, DynamoDbCdcPosition>)
			.IsAssignableFrom(_interfaceType).ShouldBeTrue();
	}

	[Fact]
	public void InheritFromICdcProcessor()
	{
		// Assert — transitive: ICdcStreamProcessor<T, TPos> : ICdcProcessor<T>
		typeof(Excalibur.Cdc.ICdcProcessor<DynamoDbDataChangeEvent>)
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
		// Assert — StartAsync is accessible through the interface hierarchy
		var streamType = typeof(Excalibur.Cdc.ICdcStreamProcessor<DynamoDbDataChangeEvent, DynamoDbCdcPosition>);
		var method = streamType.GetMethod("StartAsync");
		method.ShouldNotBeNull();
		method!.ReturnType.ShouldBe(typeof(Task));
	}

	[Fact]
	public void ExposeProcessBatchAsync_ViaBaseInterface()
	{
		// Assert — ProcessBatchAsync is accessible through the interface hierarchy
		var baseType = typeof(Excalibur.Cdc.ICdcProcessor<DynamoDbDataChangeEvent>);
		var method = baseType.GetMethod("ProcessBatchAsync");
		method.ShouldNotBeNull();
		method!.ReturnType.ShouldBe(typeof(Task<int>));
	}

	[Fact]
	public void ExposeGetCurrentPositionAsync_ViaBaseInterface()
	{
		// Assert
		var streamType = typeof(Excalibur.Cdc.ICdcStreamProcessor<DynamoDbDataChangeEvent, DynamoDbCdcPosition>);
		var method = streamType.GetMethod("GetCurrentPositionAsync");
		method.ShouldNotBeNull();
		method!.ReturnType.ShouldBe(typeof(Task<DynamoDbCdcPosition>));
	}

	[Fact]
	public void ExposeConfirmPositionAsync_ViaBaseInterface()
	{
		// Assert
		var streamType = typeof(Excalibur.Cdc.ICdcStreamProcessor<DynamoDbDataChangeEvent, DynamoDbCdcPosition>);
		var method = streamType.GetMethod("ConfirmPositionAsync");
		method.ShouldNotBeNull();
		method!.ReturnType.ShouldBe(typeof(Task));
	}

	#endregion
}
