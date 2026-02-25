// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Hosting.AspNetCore;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Excalibur.Dispatch.Hosting.AspNetCore.Tests;

/// <summary>
/// Tests for <see cref="ControllerBaseExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ControllerBaseExtensionsShould : UnitTestBase
{
	#region DispatchMessageAsync null guards

	[Fact]
	public async Task DispatchMessageAsync_ThrowWhenControllerIsNull()
	{
		// Arrange
		var message = new TestAction();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => ((ControllerBase)null!).DispatchMessageAsync(message, CancellationToken.None));
	}

	[Fact]
	public async Task DispatchMessageAsync_ThrowWhenMessageIsNull()
	{
		// Arrange
		var controller = CreateTestController();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => controller.DispatchMessageAsync((TestAction)null!, CancellationToken.None));
	}

	#endregion

	#region DispatchMessageAsync<TMessage, TResponse> null guards

	[Fact]
	public async Task DispatchMessageAsyncGeneric_ThrowWhenControllerIsNull()
	{
		// Arrange
		var message = new TestActionWithResponse();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => ((ControllerBase)null!).DispatchMessageAsync<TestActionWithResponse, TestResponse>(
				message, CancellationToken.None));
	}

	[Fact]
	public async Task DispatchMessageAsyncGeneric_ThrowWhenMessageIsNull()
	{
		// Arrange
		var controller = CreateTestController();

		// Act & Assert — cast to resolve overload ambiguity
		await Should.ThrowAsync<ArgumentNullException>(
			() => controller.DispatchMessageAsync<TestActionWithResponse, TestResponse>(
				(TestActionWithResponse)null!, CancellationToken.None));
	}

	#endregion

	#region DispatchMessageAsync with factory null guards

	[Fact]
	public async Task DispatchMessageAsyncWithFactory_ThrowWhenControllerIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => ((ControllerBase)null!).DispatchMessageAsync<TestAction>(
				_ => new TestAction(), CancellationToken.None));
	}

	[Fact]
	public async Task DispatchMessageAsyncWithFactory_ThrowWhenFactoryIsNull()
	{
		// Arrange
		var controller = CreateTestController();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => controller.DispatchMessageAsync<TestAction>(
				(Func<HttpContext, TestAction>)null!, CancellationToken.None));
	}

	#endregion

	#region DispatchEventAsync null guards

	[Fact]
	public async Task DispatchEventAsync_ThrowWhenControllerIsNull()
	{
		// Arrange
		var evt = new TestEvent();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => ((ControllerBase)null!).DispatchEventAsync(evt, CancellationToken.None));
	}

	[Fact]
	public async Task DispatchEventAsync_ThrowWhenEventIsNull()
	{
		// Arrange
		var controller = CreateTestController();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => controller.DispatchEventAsync((TestEvent)null!, CancellationToken.None));
	}

	#endregion

	#region DispatchEventAsync with factory null guards

	[Fact]
	public async Task DispatchEventAsyncWithFactory_ThrowWhenControllerIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => ((ControllerBase)null!).DispatchEventAsync<TestEvent>(
				_ => new TestEvent(), CancellationToken.None));
	}

	[Fact]
	public async Task DispatchEventAsyncWithFactory_ThrowWhenFactoryIsNull()
	{
		// Arrange
		var controller = CreateTestController();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => controller.DispatchEventAsync<TestEvent>(
				(Func<HttpContext, TestEvent>)null!, CancellationToken.None));
	}

	#endregion

	#region Helpers

	private static TestController CreateTestController()
	{
		var httpContext = new DefaultHttpContext();
		var controller = new TestController
		{
			ControllerContext = new ControllerContext
			{
				HttpContext = httpContext
			}
		};
		return controller;
	}

	private sealed class TestController : ControllerBase;

	private sealed class TestAction : IDispatchAction;

	private sealed class TestActionWithResponse : IDispatchAction<TestResponse>;

	private sealed class TestResponse;

	private sealed class TestEvent : IDispatchEvent;

	#endregion
}
