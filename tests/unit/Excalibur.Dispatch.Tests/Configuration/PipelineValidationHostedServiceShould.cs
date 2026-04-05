// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly - FakeItEasy .Returns() stores ValueTask

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Configuration;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class PipelineValidationHostedServiceShould
{
	[Fact]
	public async Task ThrowWhenNoMiddlewareRegistered()
	{
		// Arrange -- empty service collection (no middleware)
		var services = new ServiceCollection();
		var sp = services.BuildServiceProvider();
		var sut = new PipelineValidationHostedService(sp, NullLogger<PipelineValidationHostedService>.Instance);

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => sut.StartAsync(CancellationToken.None));
		ex.Message.ShouldContain("No dispatch middleware registered");
	}

	[Fact]
	public async Task SucceedWhenSingleMiddlewareRegistered()
	{
		// Arrange
		var middleware = A.Fake<IDispatchMiddleware>();
		A.CallTo(() => middleware.Stage).Returns(DispatchMiddlewareStage.PreProcessing);

		var services = new ServiceCollection();
		services.AddSingleton(middleware);
		var sp = services.BuildServiceProvider();
		var sut = new PipelineValidationHostedService(sp, NullLogger<PipelineValidationHostedService>.Instance);

		// Act & Assert -- should not throw
		await Should.NotThrowAsync(() => sut.StartAsync(CancellationToken.None));
	}

	[Fact]
	public async Task SucceedWithMultipleMiddlewareInDifferentStages()
	{
		// Arrange
		var pre = A.Fake<IDispatchMiddleware>();
		A.CallTo(() => pre.Stage).Returns(DispatchMiddlewareStage.PreProcessing);
		var val = A.Fake<IDispatchMiddleware>();
		A.CallTo(() => val.Stage).Returns(DispatchMiddlewareStage.Validation);

		var services = new ServiceCollection();
		services.AddSingleton(pre);
		services.AddSingleton(val);
		var sp = services.BuildServiceProvider();
		var sut = new PipelineValidationHostedService(sp, NullLogger<PipelineValidationHostedService>.Instance);

		// Act & Assert
		await Should.NotThrowAsync(() => sut.StartAsync(CancellationToken.None));
	}

	[Fact]
	public async Task NotThrowForDuplicateMiddlewareButLogWarning()
	{
		// Arrange -- two identical middleware types in same stage (should warn, not fail)
		var m1 = A.Fake<IDispatchMiddleware>();
		A.CallTo(() => m1.Stage).Returns(DispatchMiddlewareStage.PreProcessing);
		var m2 = A.Fake<IDispatchMiddleware>();
		A.CallTo(() => m2.Stage).Returns(DispatchMiddlewareStage.PreProcessing);

		var services = new ServiceCollection();
		services.AddSingleton(m1);
		services.AddSingleton(m2);
		var sp = services.BuildServiceProvider();
		var sut = new PipelineValidationHostedService(sp, NullLogger<PipelineValidationHostedService>.Instance);

		// Act & Assert -- duplicate is a warning, not an error
		await Should.NotThrowAsync(() => sut.StartAsync(CancellationToken.None));
	}

	[Fact]
	public async Task CompleteStopAsyncImmediately()
	{
		// Arrange
		var middleware = A.Fake<IDispatchMiddleware>();
		A.CallTo(() => middleware.Stage).Returns(DispatchMiddlewareStage.PreProcessing);

		var services = new ServiceCollection();
		services.AddSingleton(middleware);
		var sp = services.BuildServiceProvider();
		var sut = new PipelineValidationHostedService(sp, NullLogger<PipelineValidationHostedService>.Instance);

		// Act & Assert -- StopAsync is a no-op
		await Should.NotThrowAsync(() => sut.StopAsync(CancellationToken.None));
	}
}
