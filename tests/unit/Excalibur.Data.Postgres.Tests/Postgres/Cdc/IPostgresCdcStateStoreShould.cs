// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.Postgres;

namespace Excalibur.Data.Tests.Postgres.Cdc;

/// <summary>
/// Sprint 623 B.1: Unit tests for the <see cref="IPostgresCdcStateStore"/> interface.
/// Tests verify interface definition and member signatures.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Postgres")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class IPostgresCdcStateStoreShould
{
	private readonly Type _interfaceType = typeof(IPostgresCdcStateStore);

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
	public void HaveGetLastPositionAsyncMethod()
	{
		// Arrange
		var method = _interfaceType.GetMethod("GetLastPositionAsync");

		// Assert
		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(Task<PostgresCdcPosition>));
	}

	[Fact]
	public void GetLastPositionAsyncMethod_HasCorrectParameters()
	{
		// Arrange
		var method = _interfaceType.GetMethod("GetLastPositionAsync");
		var parameters = method!.GetParameters();

		// Assert -- Postgres includes slotName parameter
		parameters.Length.ShouldBe(3);
		parameters[0].ParameterType.ShouldBe(typeof(string));
		parameters[0].Name.ShouldBe("processorId");
		parameters[1].ParameterType.ShouldBe(typeof(string));
		parameters[1].Name.ShouldBe("slotName");
		parameters[2].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	[Fact]
	public void HaveSavePositionAsyncMethod()
	{
		// Arrange
		var method = _interfaceType.GetMethod("SavePositionAsync");

		// Assert
		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(Task));
	}

	[Fact]
	public void SavePositionAsyncMethod_HasCorrectParameters()
	{
		// Arrange
		var method = _interfaceType.GetMethod("SavePositionAsync");
		var parameters = method!.GetParameters();

		// Assert -- Postgres includes slotName parameter
		parameters.Length.ShouldBe(4);
		parameters[0].ParameterType.ShouldBe(typeof(string));
		parameters[0].Name.ShouldBe("processorId");
		parameters[1].ParameterType.ShouldBe(typeof(string));
		parameters[1].Name.ShouldBe("slotName");
		parameters[2].ParameterType.ShouldBe(typeof(PostgresCdcPosition));
		parameters[3].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	[Fact]
	public void HaveGetAllStatesAsyncMethod()
	{
		// Arrange
		var method = _interfaceType.GetMethod("GetAllStatesAsync");

		// Assert
		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(Task<IReadOnlyList<PostgresCdcStateEntry>>));
	}

	[Fact]
	public void HaveSaveStateAsyncMethod()
	{
		// Arrange
		var method = _interfaceType.GetMethod("SaveStateAsync");

		// Assert
		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(Task));
	}

	[Fact]
	public void HaveClearStateAsyncMethod()
	{
		// Arrange
		var method = _interfaceType.GetMethod("ClearStateAsync");

		// Assert
		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(Task));
	}

	#endregion

	#region Method Count Tests

	[Fact]
	public void HaveFiveDeclaredMethods()
	{
		// Arrange
		var methods = _interfaceType.GetMethods()
			.Where(m => m.DeclaringType == _interfaceType)
			.ToList();

		// Assert
		methods.Count.ShouldBe(5);
	}

	#endregion
}
