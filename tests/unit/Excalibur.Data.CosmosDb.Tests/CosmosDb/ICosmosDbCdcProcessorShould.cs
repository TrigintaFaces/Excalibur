// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CosmosDb.Cdc;

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
[Trait("Feature", "CDC")]
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
		var parameters = method.GetParameters();

		// Assert
		parameters.Length.ShouldBe(2);
		parameters[0].ParameterType.ShouldBe(typeof(Func<CosmosDbDataChangeEvent, CancellationToken, Task>));
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
		var parameters = method.GetParameters();

		// Assert
		parameters.Length.ShouldBe(2);
		parameters[0].ParameterType.ShouldBe(typeof(Func<CosmosDbDataChangeEvent, CancellationToken, Task>));
		parameters[1].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	[Fact]
	public void HaveGetCurrentPositionAsyncMethod()
	{
		// Arrange
		var method = _interfaceType.GetMethod("GetCurrentPositionAsync");

		// Assert
		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(Task<CosmosDbCdcPosition>));
	}

	[Fact]
	public void GetCurrentPositionAsyncMethod_HasCorrectParameters()
	{
		// Arrange
		var method = _interfaceType.GetMethod("GetCurrentPositionAsync");
		var parameters = method.GetParameters();

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
		var parameters = method.GetParameters();

		// Assert
		parameters.Length.ShouldBe(2);
		parameters[0].ParameterType.ShouldBe(typeof(CosmosDbCdcPosition));
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
