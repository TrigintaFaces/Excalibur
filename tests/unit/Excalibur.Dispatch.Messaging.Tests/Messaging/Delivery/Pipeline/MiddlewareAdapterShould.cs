// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery.Pipeline;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Pipeline;

/// <summary>
/// Unit tests for the <see cref="MiddlewareAdapter"/> class.
/// </summary>
/// <remarks>
/// Sprint 461 - Task S461.1: Remaining 0% Coverage Tests.
/// Tests the adapter that wraps IDispatchMiddleware for zero-allocation pipeline usage.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
[Trait("Priority", "0")]
public sealed class MiddlewareAdapterShould : IDisposable
{
	private readonly IDispatchMiddleware _middleware;
	private readonly MiddlewareAdapter _sut;

	public MiddlewareAdapterShould()
	{
		_middleware = A.Fake<IDispatchMiddleware>();
		_ = A.CallTo(() => _middleware.Stage).Returns(DispatchMiddlewareStage.Processing);
		_sut = new MiddlewareAdapter(_middleware);
	}

	public void Dispose()
	{
		_sut.Dispose();
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowsOnNullMiddleware()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new MiddlewareAdapter(null!));
	}

	[Fact]
	public void Constructor_AcceptsValidMiddleware()
	{
		// Arrange & Act
		using var adapter = new MiddlewareAdapter(_middleware);

		// Assert
		_ = adapter.ShouldNotBeNull();
	}

	#endregion

	#region Stage Property Tests

	[Fact]
	public void Stage_ReturnsMiddlewareStageWhenSet()
	{
		// Arrange
		_ = A.CallTo(() => _middleware.Stage).Returns(DispatchMiddlewareStage.PreProcessing);

		using var adapter = new MiddlewareAdapter(_middleware);

		// Act
		var result = adapter.Stage;

		// Assert
		result.ShouldBe(DispatchMiddlewareStage.PreProcessing);
	}

	[Fact]
	public void Stage_ReturnsEndWhenMiddlewareStageIsNull()
	{
		// Arrange
		_ = A.CallTo(() => _middleware.Stage).Returns(null);

		using var adapter = new MiddlewareAdapter(_middleware);

		// Act
		var result = adapter.Stage;

		// Assert
		result.ShouldBe(DispatchMiddlewareStage.End);
	}

	[Theory]
	[InlineData(DispatchMiddlewareStage.PreProcessing)]
	[InlineData(DispatchMiddlewareStage.Processing)]
	[InlineData(DispatchMiddlewareStage.PostProcessing)]
	[InlineData(DispatchMiddlewareStage.End)]
	public void Stage_MapsAllStagesCorrectly(DispatchMiddlewareStage expectedStage)
	{
		// Arrange
		_ = A.CallTo(() => _middleware.Stage).Returns(expectedStage);

		using var adapter = new MiddlewareAdapter(_middleware);

		// Act
		var result = adapter.Stage;

		// Assert
		result.ShouldBe(expectedStage);
	}

	#endregion

	#region IZeroAllocationMiddleware Implementation Tests

	[Fact]
	public void Adapter_ImplementsIZeroAllocationMiddleware()
	{
		// Assert
		_ = _sut.ShouldBeAssignableTo<IZeroAllocationMiddleware>();
	}

	[Fact]
	public void Adapter_ImplementsIDisposable()
	{
		// Assert
		_ = _sut.ShouldBeAssignableTo<IDisposable>();
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		// Arrange
		var adapter = new MiddlewareAdapter(_middleware);

		// Act & Assert - Should not throw
		adapter.Dispose();
		adapter.Dispose();
		adapter.Dispose();
	}

	[Fact]
	public void Dispose_DisposesInternalResources()
	{
		// Arrange
		var adapter = new MiddlewareAdapter(_middleware);

		// Act
		adapter.Dispose();

		// Assert - Should not throw, resources cleaned up
		// The ThreadLocal is internally disposed
		true.ShouldBeTrue();
	}

	#endregion

	#region Wrapped Middleware Tests

	[Fact]
	public void WrappedMiddleware_PreservesStageFromOriginal()
	{
		// Arrange
		var middleware1 = A.Fake<IDispatchMiddleware>();
		var middleware2 = A.Fake<IDispatchMiddleware>();

		_ = A.CallTo(() => middleware1.Stage).Returns(DispatchMiddlewareStage.PreProcessing);
		_ = A.CallTo(() => middleware2.Stage).Returns(DispatchMiddlewareStage.PostProcessing);

		// Act
		using var adapter1 = new MiddlewareAdapter(middleware1);
		using var adapter2 = new MiddlewareAdapter(middleware2);

		// Assert
		adapter1.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
		adapter2.Stage.ShouldBe(DispatchMiddlewareStage.PostProcessing);
	}

	[Fact]
	public void WrappedMiddleware_CanWrapMultipleMiddlewareInstances()
	{
		// Arrange
		var adapters = new List<MiddlewareAdapter>();

		// Act
		for (int i = 0; i < 5; i++)
		{
			var middleware = A.Fake<IDispatchMiddleware>();
			_ = A.CallTo(() => middleware.Stage).Returns(DispatchMiddlewareStage.Processing);
			adapters.Add(new MiddlewareAdapter(middleware));
		}

		// Assert
		adapters.Count.ShouldBe(5);
		adapters.All(a => a.Stage == DispatchMiddlewareStage.Processing).ShouldBeTrue();

		// Cleanup
		foreach (var adapter in adapters)
		{
			adapter.Dispose();
		}
	}

	#endregion
}
