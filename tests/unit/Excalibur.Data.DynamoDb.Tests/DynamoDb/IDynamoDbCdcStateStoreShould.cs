// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb.Cdc;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for the <see cref="IDynamoDbCdcStateStore"/> interface.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Tests verify interface definition and member signatures.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "CDC")]
public sealed class IDynamoDbCdcStateStoreShould
{
	private readonly Type _interfaceType = typeof(IDynamoDbCdcStateStore);

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
	public void HaveGetPositionAsyncMethod()
	{
		// Arrange
		var method = _interfaceType.GetMethod("GetPositionAsync");

		// Assert
		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(Task<DynamoDbCdcPosition?>));
	}

	[Fact]
	public void GetPositionAsyncMethod_HasCorrectParameters()
	{
		// Arrange
		var method = _interfaceType.GetMethod("GetPositionAsync");
		var parameters = method.GetParameters();

		// Assert
		parameters.Length.ShouldBe(2);
		parameters[0].ParameterType.ShouldBe(typeof(string));
		parameters[0].Name.ShouldBe("processorName");
		parameters[1].ParameterType.ShouldBe(typeof(CancellationToken));
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
		var parameters = method.GetParameters();

		// Assert
		parameters.Length.ShouldBe(3);
		parameters[0].ParameterType.ShouldBe(typeof(string));
		parameters[0].Name.ShouldBe("processorName");
		parameters[1].ParameterType.ShouldBe(typeof(DynamoDbCdcPosition));
		parameters[1].Name.ShouldBe("position");
		parameters[2].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	[Fact]
	public void HaveDeletePositionAsyncMethod()
	{
		// Arrange
		var method = _interfaceType.GetMethod("DeletePositionAsync");

		// Assert
		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(Task));
	}

	[Fact]
	public void DeletePositionAsyncMethod_HasCorrectParameters()
	{
		// Arrange
		var method = _interfaceType.GetMethod("DeletePositionAsync");
		var parameters = method.GetParameters();

		// Assert
		parameters.Length.ShouldBe(2);
		parameters[0].ParameterType.ShouldBe(typeof(string));
		parameters[0].Name.ShouldBe("processorName");
		parameters[1].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	#endregion

	#region Method Count Tests

	[Fact]
	public void HaveThreeDeclaredMethods()
	{
		// Arrange
		var methods = _interfaceType.GetMethods()
			.Where(m => m.DeclaringType == _interfaceType)
			.ToList();

		// Assert
		methods.Count.ShouldBe(3);
	}

	#endregion
}
