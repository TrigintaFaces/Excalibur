// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;

using FakeItEasy;

namespace Excalibur.Data.Tests.Abstractions;

/// <summary>
/// Unit tests for <see cref="DataRequestContext"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.Abstractions")]
[Trait("Feature", "DataRequestPipeline")]
public sealed class DataRequestContextShould : UnitTestBase
{
	private readonly object _testRequest;
#pragma warning disable CA2213 // _mockProvider is a FakeItEasy fake, not a real IDisposable
	private readonly IPersistenceProvider _mockProvider;
#pragma warning restore CA2213
	private readonly Type _requestType;
	private readonly Type _resultType;

	public DataRequestContextShould()
	{
		_testRequest = new { Id = 1, Name = "Test" };
		_mockProvider = A.Fake<IPersistenceProvider>();
		_requestType = typeof(TestRequest);
		_resultType = typeof(TestResult);
	}

	private DataRequestContext CreateContext() =>
		new(_testRequest, _mockProvider, _requestType, _resultType);

	#region Constructor Tests

	[Fact]
	public void Create_StoresRequest()
	{
		// Act
		var context = CreateContext();

		// Assert
		context.Request.ShouldBe(_testRequest);
	}

	[Fact]
	public void Create_StoresProvider()
	{
		// Act
		var context = CreateContext();

		// Assert
		context.Provider.ShouldBe(_mockProvider);
	}

	[Fact]
	public void Create_StoresRequestType()
	{
		// Act
		var context = CreateContext();

		// Assert
		context.RequestType.ShouldBe(_requestType);
	}

	[Fact]
	public void Create_StoresResultType()
	{
		// Act
		var context = CreateContext();

		// Assert
		context.ResultType.ShouldBe(_resultType);
	}

	[Fact]
	public void Create_InitializesItemsDictionary()
	{
		// Act
		var context = CreateContext();

		// Assert
		context.Items.ShouldNotBeNull();
		context.Items.ShouldBeEmpty();
	}

	#endregion

	#region Result Property Tests

	[Fact]
	public void Result_InitiallyNull()
	{
		// Act
		var context = CreateContext();

		// Assert
		context.Result.ShouldBeNull();
	}

	[Fact]
	public void Result_CanBeSet()
	{
		// Arrange
		var context = CreateContext();
		var result = new TestResult { Value = 42 };

		// Act
		context.Result = result;

		// Assert
		context.Result.ShouldBe(result);
	}

	#endregion

	#region Exception Property Tests

	[Fact]
	public void Exception_InitiallyNull()
	{
		// Act
		var context = CreateContext();

		// Assert
		context.Exception.ShouldBeNull();
	}

	[Fact]
	public void Exception_CanBeSet()
	{
		// Arrange
		var context = CreateContext();
		var exception = new InvalidOperationException("Test error");

		// Act
		context.Exception = exception;

		// Assert
		context.Exception.ShouldBe(exception);
	}

	#endregion

	#region IsSuccess Property Tests

	[Fact]
	public void IsSuccess_ReturnsTrue_WhenExceptionIsNull()
	{
		// Arrange
		var context = CreateContext();

		// Assert
		context.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public void IsSuccess_ReturnsFalse_WhenExceptionIsSet()
	{
		// Arrange
		var context = CreateContext();
		context.Exception = new InvalidOperationException("Error");

		// Assert
		context.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public void IsSuccess_ReturnsTrue_AfterExceptionCleared()
	{
		// Arrange
		var context = CreateContext();
		context.Exception = new InvalidOperationException("Error");

		// Act
		context.Exception = null;

		// Assert
		context.IsSuccess.ShouldBeTrue();
	}

	#endregion

	#region Items Dictionary Tests

	[Fact]
	public void Items_CanAddItems()
	{
		// Arrange
		var context = CreateContext();

		// Act
		context.Items["key1"] = "value1";
		context.Items["key2"] = 123;

		// Assert
		context.Items.Count.ShouldBe(2);
		context.Items["key1"].ShouldBe("value1");
		context.Items["key2"].ShouldBe(123);
	}

	[Fact]
	public void Items_UsesOrdinalComparison()
	{
		// Arrange
		var context = CreateContext();

		// Act
		context.Items["Key"] = "value1";
		context.Items["key"] = "value2";

		// Assert - ordinal comparison means these are different keys
		context.Items.Count.ShouldBe(2);
	}

	[Fact]
	public void Items_CanStoreNullValues()
	{
		// Arrange
		var context = CreateContext();

		// Act
		context.Items["nullKey"] = null;

		// Assert
		context.Items.ContainsKey("nullKey").ShouldBeTrue();
		context.Items["nullKey"].ShouldBeNull();
	}

	#endregion

	#region InterceptionContext Property Tests

	[Fact]
	public void InterceptionContext_InitiallyNull()
	{
		// Act
		var context = CreateContext();

		// Assert
		context.InterceptionContext.ShouldBeNull();
	}

	[Fact]
	public void InterceptionContext_CanBeSet()
	{
		// Arrange
		var context = CreateContext();
		var interceptionContext = new InterceptionContext
		{
			ProviderName = "SqlServer",
			OperationType = "Query"
		};

		// Act
		context.InterceptionContext = interceptionContext;

		// Assert
		context.InterceptionContext.ShouldBe(interceptionContext);
		context.InterceptionContext.ProviderName.ShouldBe("SqlServer");
	}

	#endregion

	#region Test Types

	private sealed class TestRequest
	{
		public int Id { get; init; }
	}

	private sealed class TestResult
	{
		public int Value { get; init; }
	}

	#endregion
}
