// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.EventSourcing.Tests.MaterializedViews;

/// <summary>
/// Unit tests for <see cref="IMaterializedViewBuilder{TView}"/> interface contract.
/// </summary>
/// <remarks>
/// Sprint 516: Materialized Views foundation tests.
/// Tests verify interface structure, method signatures, and type constraints.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "MaterializedViews")]
[Trait("Feature", "Builder")]
public sealed class IMaterializedViewBuilderShould
{
	#region Interface Structure Tests

	[Fact]
	public void BeAnInterface()
	{
		// Assert
		typeof(IMaterializedViewBuilder<>).IsInterface.ShouldBeTrue();
	}

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(IMaterializedViewBuilder<>).IsPublic.ShouldBeTrue();
	}

	[Fact]
	public void BeGeneric()
	{
		// Assert
		typeof(IMaterializedViewBuilder<>).IsGenericTypeDefinition.ShouldBeTrue();
		typeof(IMaterializedViewBuilder<>).GetGenericArguments().Length.ShouldBe(1);
	}

	[Fact]
	public void HaveViewNameProperty()
	{
		// Arrange
		var property = typeof(IMaterializedViewBuilder<TestView>).GetProperty("ViewName");

		// Assert
		property.ShouldNotBeNull();
		property.PropertyType.ShouldBe(typeof(string));
		property.CanRead.ShouldBeTrue();
	}

	[Fact]
	public void HaveHandledEventTypesProperty()
	{
		// Arrange
		var property = typeof(IMaterializedViewBuilder<TestView>).GetProperty("HandledEventTypes");

		// Assert
		property.ShouldNotBeNull();
		property.PropertyType.ShouldBe(typeof(IReadOnlyList<Type>));
		property.CanRead.ShouldBeTrue();
	}

	[Fact]
	public void HaveGetViewIdMethod()
	{
		// Assert
		var method = typeof(IMaterializedViewBuilder<TestView>).GetMethod("GetViewId");
		method.ShouldNotBeNull();
	}

	[Fact]
	public void HaveApplyMethod()
	{
		// Assert
		var method = typeof(IMaterializedViewBuilder<TestView>).GetMethod("Apply");
		method.ShouldNotBeNull();
	}

	[Fact]
	public void HaveCreateNewMethod()
	{
		// Assert
		var method = typeof(IMaterializedViewBuilder<TestView>).GetMethod("CreateNew");
		method.ShouldNotBeNull();
	}

	#endregion

	#region Method Parameter Tests

	[Fact]
	public void GetViewId_TakeIDomainEvent()
	{
		// Arrange
		var method = typeof(IMaterializedViewBuilder<TestView>).GetMethod("GetViewId");
		var parameters = method.GetParameters();

		// Assert
		parameters.Length.ShouldBe(1);
		parameters[0].Name.ShouldBe("event");
		parameters[0].ParameterType.ShouldBe(typeof(IDomainEvent));
	}

	[Fact]
	public void Apply_TakeViewAndEvent()
	{
		// Arrange
		var method = typeof(IMaterializedViewBuilder<TestView>).GetMethod("Apply");
		var parameters = method.GetParameters();

		// Assert
		parameters.Length.ShouldBe(2);
		parameters[0].Name.ShouldBe("view");
		parameters[0].ParameterType.ShouldBe(typeof(TestView));
		parameters[1].Name.ShouldBe("event");
		parameters[1].ParameterType.ShouldBe(typeof(IDomainEvent));
	}

	[Fact]
	public void CreateNew_TakeNoParameters()
	{
		// Arrange
		var method = typeof(IMaterializedViewBuilder<TestView>).GetMethod("CreateNew");
		var parameters = method.GetParameters();

		// Assert
		parameters.Length.ShouldBe(0);
	}

	#endregion

	#region Return Type Tests

	[Fact]
	public void GetViewId_ReturnNullableString()
	{
		// Arrange
		var method = typeof(IMaterializedViewBuilder<TestView>).GetMethod("GetViewId");

		// Assert
		method.ReturnType.ShouldBe(typeof(string));
	}

	[Fact]
	public void Apply_ReturnView()
	{
		// Arrange
		var method = typeof(IMaterializedViewBuilder<TestView>).GetMethod("Apply");

		// Assert
		method.ReturnType.ShouldBe(typeof(TestView));
	}

	[Fact]
	public void CreateNew_ReturnView()
	{
		// Arrange
		var method = typeof(IMaterializedViewBuilder<TestView>).GetMethod("CreateNew");

		// Assert
		method.ReturnType.ShouldBe(typeof(TestView));
	}

	#endregion

	#region Generic Constraint Tests

	[Fact]
	public void HaveClassConstraintOnTView()
	{
		// Arrange
		var genericArgs = typeof(IMaterializedViewBuilder<>).GetGenericArguments();

		// Assert
		genericArgs.Length.ShouldBe(1);
		genericArgs[0].GenericParameterAttributes
			.HasFlag(System.Reflection.GenericParameterAttributes.ReferenceTypeConstraint)
			.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultConstructorConstraintOnTView()
	{
		// Arrange
		var genericArgs = typeof(IMaterializedViewBuilder<>).GetGenericArguments();

		// Assert
		genericArgs[0].GenericParameterAttributes
			.HasFlag(System.Reflection.GenericParameterAttributes.DefaultConstructorConstraint)
			.ShouldBeTrue();
	}

	#endregion

	#region Test Types

	/// <summary>
	/// Test view type for interface testing.
	/// </summary>
	internal sealed class TestView
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
	}

	#endregion
}
