// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Tests.MaterializedViews;

/// <summary>
/// Unit tests for <see cref="IMaterializedViewStore"/> interface contract.
/// </summary>
/// <remarks>
/// Sprint 516: Materialized Views foundation tests.
/// Tests verify interface structure, method signatures, and type constraints.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "MaterializedViews")]
[Trait("Feature", "Store")]
public sealed class IMaterializedViewStoreShould
{
	#region Interface Structure Tests

	[Fact]
	public void BeAnInterface()
	{
		// Assert
		typeof(IMaterializedViewStore).IsInterface.ShouldBeTrue();
	}

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(IMaterializedViewStore).IsPublic.ShouldBeTrue();
	}

	[Fact]
	public void HaveGetAsyncMethod()
	{
		// Assert
		var method = typeof(IMaterializedViewStore).GetMethod("GetAsync");
		method.ShouldNotBeNull();
		method.IsGenericMethod.ShouldBeTrue();
	}

	[Fact]
	public void HaveSaveAsyncMethod()
	{
		// Assert
		var method = typeof(IMaterializedViewStore).GetMethod("SaveAsync");
		method.ShouldNotBeNull();
		method.IsGenericMethod.ShouldBeTrue();
	}

	[Fact]
	public void HaveDeleteAsyncMethod()
	{
		// Assert
		var method = typeof(IMaterializedViewStore).GetMethod("DeleteAsync");
		method.ShouldNotBeNull();
	}

	[Fact]
	public void HaveGetPositionAsyncMethod()
	{
		// Assert
		var method = typeof(IMaterializedViewStore).GetMethod("GetPositionAsync");
		method.ShouldNotBeNull();
	}

	[Fact]
	public void HaveSavePositionAsyncMethod()
	{
		// Assert
		var method = typeof(IMaterializedViewStore).GetMethod("SavePositionAsync");
		method.ShouldNotBeNull();
	}

	#endregion

	#region Method Parameter Tests

	[Fact]
	public void GetAsync_RequireCancellationToken()
	{
		// Arrange
		var method = typeof(IMaterializedViewStore).GetMethod("GetAsync");
		var parameters = method.GetParameters();

		// Assert - viewName, viewId, cancellationToken
		parameters.Length.ShouldBe(3);
		parameters[0].Name.ShouldBe("viewName");
		parameters[0].ParameterType.ShouldBe(typeof(string));
		parameters[1].Name.ShouldBe("viewId");
		parameters[1].ParameterType.ShouldBe(typeof(string));
		parameters[2].Name.ShouldBe("cancellationToken");
		parameters[2].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	[Fact]
	public void SaveAsync_RequireCancellationToken()
	{
		// Arrange
		var method = typeof(IMaterializedViewStore).GetMethod("SaveAsync");
		var parameters = method.GetParameters();

		// Assert - viewName, viewId, view, cancellationToken
		parameters.Length.ShouldBe(4);
		parameters[0].Name.ShouldBe("viewName");
		parameters[1].Name.ShouldBe("viewId");
		parameters[2].Name.ShouldBe("view");
		parameters[3].Name.ShouldBe("cancellationToken");
		parameters[3].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	[Fact]
	public void DeleteAsync_RequireCancellationToken()
	{
		// Arrange
		var method = typeof(IMaterializedViewStore).GetMethod("DeleteAsync");
		var parameters = method.GetParameters();

		// Assert - viewName, viewId, cancellationToken
		parameters.Length.ShouldBe(3);
		parameters[0].Name.ShouldBe("viewName");
		parameters[1].Name.ShouldBe("viewId");
		parameters[2].Name.ShouldBe("cancellationToken");
		parameters[2].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	[Fact]
	public void GetPositionAsync_RequireCancellationToken()
	{
		// Arrange
		var method = typeof(IMaterializedViewStore).GetMethod("GetPositionAsync");
		var parameters = method.GetParameters();

		// Assert
		parameters.Length.ShouldBe(2);
		parameters[0].Name.ShouldBe("viewName");
		parameters[1].Name.ShouldBe("cancellationToken");
		parameters[1].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	[Fact]
	public void SavePositionAsync_RequireCancellationToken()
	{
		// Arrange
		var method = typeof(IMaterializedViewStore).GetMethod("SavePositionAsync");
		var parameters = method.GetParameters();

		// Assert
		parameters.Length.ShouldBe(3);
		parameters[0].Name.ShouldBe("viewName");
		parameters[1].Name.ShouldBe("position");
		parameters[1].ParameterType.ShouldBe(typeof(long));
		parameters[2].Name.ShouldBe("cancellationToken");
		parameters[2].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	#endregion

	#region Return Type Tests

	[Fact]
	public void GetAsync_ReturnValueTask()
	{
		// Arrange
		var method = typeof(IMaterializedViewStore).GetMethod("GetAsync");

		// Assert - ValueTask<TView?>
		method.ReturnType.IsGenericType.ShouldBeTrue();
		method.ReturnType.GetGenericTypeDefinition().ShouldBe(typeof(ValueTask<>));
	}

	[Fact]
	public void SaveAsync_ReturnValueTask()
	{
		// Arrange
		var method = typeof(IMaterializedViewStore).GetMethod("SaveAsync");

		// Assert
		method.ReturnType.ShouldBe(typeof(ValueTask));
	}

	[Fact]
	public void DeleteAsync_ReturnValueTask()
	{
		// Arrange
		var method = typeof(IMaterializedViewStore).GetMethod("DeleteAsync");

		// Assert
		method.ReturnType.ShouldBe(typeof(ValueTask));
	}

	[Fact]
	public void GetPositionAsync_ReturnValueTaskOfNullableLong()
	{
		// Arrange
		var method = typeof(IMaterializedViewStore).GetMethod("GetPositionAsync");

		// Assert
		method.ReturnType.ShouldBe(typeof(ValueTask<long?>));
	}

	[Fact]
	public void SavePositionAsync_ReturnValueTask()
	{
		// Arrange
		var method = typeof(IMaterializedViewStore).GetMethod("SavePositionAsync");

		// Assert
		method.ReturnType.ShouldBe(typeof(ValueTask));
	}

	#endregion

	#region Generic Constraint Tests

	[Fact]
	public void GetAsync_HaveClassConstraint()
	{
		// Arrange
		var method = typeof(IMaterializedViewStore).GetMethod("GetAsync");
		var genericArgs = method.GetGenericArguments();

		// Assert
		genericArgs.Length.ShouldBe(1);
		var constraint = genericArgs[0].GetGenericParameterConstraints();
		genericArgs[0].GenericParameterAttributes
			.HasFlag(System.Reflection.GenericParameterAttributes.ReferenceTypeConstraint)
			.ShouldBeTrue();
	}

	[Fact]
	public void SaveAsync_HaveClassConstraint()
	{
		// Arrange
		var method = typeof(IMaterializedViewStore).GetMethod("SaveAsync");
		var genericArgs = method.GetGenericArguments();

		// Assert
		genericArgs.Length.ShouldBe(1);
		genericArgs[0].GenericParameterAttributes
			.HasFlag(System.Reflection.GenericParameterAttributes.ReferenceTypeConstraint)
			.ShouldBeTrue();
	}

	#endregion
}
