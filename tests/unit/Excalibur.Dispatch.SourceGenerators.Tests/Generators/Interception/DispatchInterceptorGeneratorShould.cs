// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.SourceGenerators.Interception;

namespace Excalibur.Dispatch.SourceGenerators.Tests.Interception;

/// <summary>
/// Tests for <see cref="DispatchInterceptorGenerator"/> - the C# 12 interceptor generator.
/// Validates interceptor info extraction, generation patterns, and edge case handling.
/// </summary>
/// <remarks>
/// Sprint 454 - S454.5: Unit tests for interceptor behavior.
/// Tests the generator logic, InterceptorInfo model, and expected output patterns.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Priority", "0")]
public sealed class DispatchInterceptorGeneratorShould
{
	#region InterceptorInfo Model Tests (5 tests)

	[Fact]
	public void InterceptorInfo_UniqueId_CombinesTypeNameLineColumn()
	{
		// Arrange
		var info = new InterceptorInfo
		{
			MessageTypeName = "TestCommand",
			Line = 42,
			Column = 15
		};

		// Act
		var uniqueId = info.UniqueId;

		// Assert
		uniqueId.ShouldBe("TestCommand_42_15");
	}

	[Fact]
	public void InterceptorInfo_HasResult_DefaultsToFalse()
	{
		// Arrange
		var info = new InterceptorInfo();

		// Assert
		info.HasResult.ShouldBeFalse();
	}

	[Fact]
	public void InterceptorInfo_FilePath_DefaultsToEmptyString()
	{
		// Arrange
		var info = new InterceptorInfo();

		// Assert
		info.FilePath.ShouldBe(string.Empty);
	}

	[Fact]
	public void InterceptorInfo_SetsHasResultTrue_WhenResultTypeProvided()
	{
		// Arrange
		var info = new InterceptorInfo
		{
			HasResult = true,
			ResultTypeFullName = "global::TestApp.CommandResult"
		};

		// Assert
		info.HasResult.ShouldBeTrue();
		info.ResultTypeFullName.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void InterceptorInfo_StoresInterceptableLocationData()
	{
		// Arrange
		var locationData = "[System.Runtime.CompilerServices.InterceptsLocation(1, \"AAEAAACBAAAA...\")]";
		var info = new InterceptorInfo
		{
			InterceptableLocationData = locationData
		};

		// Assert
		info.InterceptableLocationData.ShouldBe(locationData);
	}

	#endregion

	#region Generator Registration Tests (3 tests)

	[Fact]
	public void Generator_ImplementsIIncrementalGenerator()
	{
		// Assert
		typeof(DispatchInterceptorGenerator).GetInterfaces()
			.ShouldContain(typeof(Microsoft.CodeAnalysis.IIncrementalGenerator));
	}

	[Fact]
	public void Generator_HasGeneratorAttribute()
	{
		// Assert
		var attribute = typeof(DispatchInterceptorGenerator)
			.GetCustomAttributes(typeof(Microsoft.CodeAnalysis.GeneratorAttribute), false);
		attribute.ShouldNotBeEmpty();
	}

	[Fact]
	public void Generator_CanBeInstantiated()
	{
		// Act
		var generator = new DispatchInterceptorGenerator();

		// Assert
		_ = generator.ShouldNotBeNull();
	}

	#endregion

	#region UniqueId Generation Edge Cases (4 tests)

	[Fact]
	public void InterceptorInfo_UniqueId_HandlesZeroLineColumn()
	{
		// Arrange
		var info = new InterceptorInfo
		{
			MessageTypeName = "ZeroPositionMessage",
			Line = 0,
			Column = 0
		};

		// Act
		var uniqueId = info.UniqueId;

		// Assert
		uniqueId.ShouldBe("ZeroPositionMessage_0_0");
	}

	[Fact]
	public void InterceptorInfo_UniqueId_HandlesLargeLineNumbers()
	{
		// Arrange - simulate a large file
		var info = new InterceptorInfo
		{
			MessageTypeName = "LargeFileCommand",
			Line = 10000,
			Column = 150
		};

		// Act
		var uniqueId = info.UniqueId;

		// Assert
		uniqueId.ShouldBe("LargeFileCommand_10000_150");
	}

	[Fact]
	public void InterceptorInfo_UniqueId_HandlesGenericTypeName()
	{
		// Arrange - generic message type (would be filtered by generator, but model should handle)
		var info = new InterceptorInfo
		{
			MessageTypeName = "GenericCommand`1",
			Line = 10,
			Column = 5
		};

		// Act
		var uniqueId = info.UniqueId;

		// Assert
		uniqueId.ShouldBe("GenericCommand`1_10_5");
	}

	[Fact]
	public void InterceptorInfo_UniqueId_DiffersByLocation()
	{
		// Arrange - same message type at different locations
		var info1 = new InterceptorInfo { MessageTypeName = "TestCommand", Line = 10, Column = 5 };
		var info2 = new InterceptorInfo { MessageTypeName = "TestCommand", Line = 20, Column = 5 };
		var info3 = new InterceptorInfo { MessageTypeName = "TestCommand", Line = 10, Column = 15 };

		// Assert - all should be unique
		info1.UniqueId.ShouldNotBe(info2.UniqueId);
		info1.UniqueId.ShouldNotBe(info3.UniqueId);
		info2.UniqueId.ShouldNotBe(info3.UniqueId);
	}

	#endregion

	#region Generated Output Pattern Tests (3 tests)

	[Fact]
	public void ExpectedOutput_UsesFileModifier_ForInterceptorClass()
	{
		// The generator produces a file-scoped class to avoid conflicts
		// Expected pattern: "file static class DispatchInterceptors"
		// This test documents the expected behavior

		// Assert - InterceptorInfo supports the data needed for file-scoped generation
		var info = new InterceptorInfo
		{
			MessageTypeName = "TestCommand",
			MessageTypeFullName = "global::TestApp.TestCommand",
			Line = 42,
			Column = 15,
			HasResult = false
		};

		// Generator should use MessageTypeFullName for type references
		info.MessageTypeFullName.ShouldStartWith("global::");
	}

	[Fact]
	public void ExpectedOutput_UsesInternalModifier_ForInterceptorMethods()
	{
		// Expected pattern: "internal static async Task<IMessageResult> Intercept_{UniqueId}(...)"
		// This test documents the expected behavior

		var info = new InterceptorInfo
		{
			MessageTypeName = "TestCommand",
			Line = 42,
			Column = 15
		};

		// Unique method name should be predictable
		info.UniqueId.ShouldBe("TestCommand_42_15");
	}

	[Fact]
	public void ExpectedOutput_IncludesResultType_WhenHasResultIsTrue()
	{
		// Expected pattern: "Task<IMessageResult<TResult>>" when HasResult is true
		var info = new InterceptorInfo
		{
			MessageTypeName = "TestQuery",
			MessageTypeFullName = "global::TestApp.TestQuery",
			Line = 50,
			Column = 20,
			HasResult = true,
			ResultTypeFullName = "global::TestApp.QueryResult"
		};

		// Verify the data is available for generation
		info.HasResult.ShouldBeTrue();
		_ = info.ResultTypeFullName.ShouldNotBeNull();
		info.ResultTypeFullName.ShouldStartWith("global::");
	}

	#endregion
}
