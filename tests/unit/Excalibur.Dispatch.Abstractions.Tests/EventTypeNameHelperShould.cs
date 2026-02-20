// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests;

/// <summary>
/// Unit tests for <see cref="EventTypeNameHelper"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventType")]
[Trait("Priority", "0")]
public sealed class EventTypeNameHelperShould
{
	#region GetEventTypeName Tests

	[Fact]
	public void GetEventTypeName_WithNull_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => EventTypeNameHelper.GetEventTypeName(null!));
	}

	[Fact]
	public void GetEventTypeName_WithSimpleType_ReturnsAssemblyQualifiedName()
	{
		// Act
		var result = EventTypeNameHelper.GetEventTypeName(typeof(string));

		// Assert
		result.ShouldNotBeNullOrEmpty();
		result.ShouldContain("System.String");
		result.ShouldContain("System.Private.CoreLib");
	}

	[Fact]
	public void GetEventTypeName_WithValueType_ReturnsAssemblyQualifiedName()
	{
		// Act
		var result = EventTypeNameHelper.GetEventTypeName(typeof(int));

		// Assert
		result.ShouldNotBeNullOrEmpty();
		result.ShouldContain("System.Int32");
	}

	[Fact]
	public void GetEventTypeName_WithCustomType_ReturnsAssemblyQualifiedName()
	{
		// Act
		var result = EventTypeNameHelper.GetEventTypeName(typeof(TestEvent));

		// Assert
		result.ShouldNotBeNullOrEmpty();
		result.ShouldContain("TestEvent");
		result.ShouldContain("Excalibur.Dispatch.Abstractions.Tests");
	}

	[Fact]
	public void GetEventTypeName_WithGenericType_ReturnsAssemblyQualifiedName()
	{
		// Act
		var result = EventTypeNameHelper.GetEventTypeName(typeof(List<string>));

		// Assert
		result.ShouldNotBeNullOrEmpty();
		result.ShouldContain("System.Collections.Generic.List");
	}

	[Fact]
	public void GetEventTypeName_WithNestedType_ReturnsAssemblyQualifiedName()
	{
		// Act
		var result = EventTypeNameHelper.GetEventTypeName(typeof(NestedTestClass));

		// Assert
		result.ShouldNotBeNullOrEmpty();
		result.ShouldContain("NestedTestClass");
	}

	[Fact]
	public void GetEventTypeName_WithArrayType_ReturnsAssemblyQualifiedName()
	{
		// Act
		var result = EventTypeNameHelper.GetEventTypeName(typeof(int[]));

		// Assert
		result.ShouldNotBeNullOrEmpty();
		result.ShouldContain("System.Int32[]");
	}

	[Fact]
	public void GetEventTypeName_WithInterface_ReturnsAssemblyQualifiedName()
	{
		// Act
		var result = EventTypeNameHelper.GetEventTypeName(typeof(IDisposable));

		// Assert
		result.ShouldNotBeNullOrEmpty();
		result.ShouldContain("System.IDisposable");
	}

	[Fact]
	public void GetEventTypeName_WithOpenGenericType_ReturnsFullName()
	{
		// Act
		var result = EventTypeNameHelper.GetEventTypeName(typeof(List<>));

		// Assert
		result.ShouldNotBeNullOrEmpty();
		// Open generic types don't have assembly qualified names
	}

	#endregion

	#region Test Helpers

	private sealed class TestEvent;

	private sealed class NestedTestClass;

	#endregion
}
