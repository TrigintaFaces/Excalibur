// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.MongoDB;

namespace Excalibur.Data.Tests.MongoDB.Cdc;

/// <summary>
/// Sprint 623 B.1: Unit tests for the <see cref="IMongoDbCdcProcessor"/> interface.
/// Tests verify interface definition and member signatures.
/// Follows the CosmosDb/DynamoDb processor interface test pattern.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "MongoDB")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class IMongoDbCdcProcessorShould
{
	private readonly Type _interfaceType = typeof(IMongoDbCdcProcessor);

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

	#region Method Definition Tests

	[Fact]
	public void HaveStartAsyncMethod()
	{
		// Arrange
		var method = _interfaceType.GetMethod("StartAsync");

		// Assert
		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(Task));
	}

	[Fact]
	public void StartAsyncMethod_HasCorrectParameters()
	{
		// Arrange
		var method = _interfaceType.GetMethod("StartAsync");
		var parameters = method!.GetParameters();

		// Assert
		parameters.Length.ShouldBe(2);
		parameters[0].ParameterType.ShouldBe(typeof(Func<MongoDbDataChangeEvent, CancellationToken, Task>));
		parameters[1].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	[Fact]
	public void HaveProcessBatchAsyncMethod()
	{
		// Arrange
		var method = _interfaceType.GetMethod("ProcessBatchAsync");

		// Assert
		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(Task<int>));
	}

	[Fact]
	public void ProcessBatchAsyncMethod_HasCorrectParameters()
	{
		// Arrange
		var method = _interfaceType.GetMethod("ProcessBatchAsync");
		var parameters = method!.GetParameters();

		// Assert
		parameters.Length.ShouldBe(2);
		parameters[0].ParameterType.ShouldBe(typeof(Func<MongoDbDataChangeEvent, CancellationToken, Task>));
		parameters[1].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	[Fact]
	public void HaveGetCurrentPositionAsyncMethod()
	{
		// Arrange
		var method = _interfaceType.GetMethod("GetCurrentPositionAsync");

		// Assert
		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(Task<MongoDbCdcPosition>));
	}

	[Fact]
	public void GetCurrentPositionAsyncMethod_HasCorrectParameters()
	{
		// Arrange
		var method = _interfaceType.GetMethod("GetCurrentPositionAsync");
		var parameters = method!.GetParameters();

		// Assert
		parameters.Length.ShouldBe(1);
		parameters[0].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	[Fact]
	public void HaveConfirmPositionAsyncMethod()
	{
		// Arrange
		var method = _interfaceType.GetMethod("ConfirmPositionAsync");

		// Assert
		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(Task));
	}

	[Fact]
	public void ConfirmPositionAsyncMethod_HasCorrectParameters()
	{
		// Arrange
		var method = _interfaceType.GetMethod("ConfirmPositionAsync");
		var parameters = method!.GetParameters();

		// Assert
		parameters.Length.ShouldBe(2);
		parameters[0].ParameterType.ShouldBe(typeof(MongoDbCdcPosition));
		parameters[1].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	#endregion

	#region Method Count Tests

	[Fact]
	public void HaveFourDeclaredMethods()
	{
		// Arrange
		var methods = _interfaceType.GetMethods()
			.Where(m => m.DeclaringType == _interfaceType)
			.ToList();

		// Assert
		methods.Count.ShouldBe(4);
	}

	#endregion
}
