// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

using Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="DispatchResilienceAdapter"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DispatchResilienceAdapterShould : UnitTestBase
{
	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenPipelineIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new DispatchResilienceAdapter(null!));
	}

	[Fact]
	public async Task ExecuteAsync_Generic_ExecutesOperation()
	{
		// Arrange
		var pipeline = new ResiliencePipelineBuilder().Build();
		var adapter = new DispatchResilienceAdapter(pipeline);

		// Act
		var result = await adapter.ExecuteAsync(
			ct => Task.FromResult(42),
			CancellationToken.None);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task ExecuteAsync_Generic_ThrowsArgumentNullException_WhenOperationIsNull()
	{
		// Arrange
		var pipeline = new ResiliencePipelineBuilder().Build();
		var adapter = new DispatchResilienceAdapter(pipeline);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await adapter.ExecuteAsync<string>(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_Void_ExecutesOperation()
	{
		// Arrange
		var pipeline = new ResiliencePipelineBuilder().Build();
		var adapter = new DispatchResilienceAdapter(pipeline);
		var executed = false;

		// Act
		await adapter.ExecuteAsync(
			ct =>
			{
				executed = true;
				return Task.CompletedTask;
			},
			CancellationToken.None);

		// Assert
		executed.ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteAsync_Void_ThrowsArgumentNullException_WhenOperationIsNull()
	{
		// Arrange
		var pipeline = new ResiliencePipelineBuilder().Build();
		var adapter = new DispatchResilienceAdapter(pipeline);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await adapter.ExecuteAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_Generic_PropagatesException()
	{
		// Arrange
		var pipeline = new ResiliencePipelineBuilder().Build();
		var adapter = new DispatchResilienceAdapter(pipeline);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await adapter.ExecuteAsync<string>(
				_ => throw new InvalidOperationException("test error"),
				CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_Void_PropagatesException()
	{
		// Arrange
		var pipeline = new ResiliencePipelineBuilder().Build();
		var adapter = new DispatchResilienceAdapter(pipeline);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await adapter.ExecuteAsync(
				_ => throw new InvalidOperationException("test error"),
				CancellationToken.None));
	}
}
