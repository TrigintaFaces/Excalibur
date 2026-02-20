// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Tests.Messaging.Resilience;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class NoOpRetryPolicyShould
{
	[Fact]
	public void Instance_ReturnSingleton()
	{
		// Act
		var instance1 = NoOpRetryPolicy.Instance;
		var instance2 = NoOpRetryPolicy.Instance;

		// Assert
		instance1.ShouldNotBeNull();
		instance1.ShouldBeSameAs(instance2);
	}

	[Fact]
	public async Task ExecuteAsync_WithResult_ReturnResult()
	{
		// Arrange
		var policy = NoOpRetryPolicy.Instance;

		// Act
		var result = await policy.ExecuteAsync(
			ct => Task.FromResult(42),
			CancellationToken.None);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task ExecuteAsync_WithResult_PropagateException()
	{
		// Arrange
		var policy = NoOpRetryPolicy.Instance;

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await policy.ExecuteAsync<int>(
				ct => throw new InvalidOperationException("test failure"),
				CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_WithoutResult_CompleteSuccessfully()
	{
		// Arrange
		var policy = NoOpRetryPolicy.Instance;
		var executed = false;

		// Act
		await policy.ExecuteAsync(
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
	public async Task ExecuteAsync_WithoutResult_PropagateException()
	{
		// Arrange
		var policy = NoOpRetryPolicy.Instance;

		// Act & Assert
		await Should.ThrowAsync<TimeoutException>(async () =>
			await policy.ExecuteAsync(
				ct => throw new TimeoutException("timed out"),
				CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_WithResult_ThrowOnNullAction()
	{
		// Arrange
		var policy = NoOpRetryPolicy.Instance;

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await policy.ExecuteAsync<int>(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_WithoutResult_ThrowOnNullAction()
	{
		// Arrange
		var policy = NoOpRetryPolicy.Instance;

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await policy.ExecuteAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_PassCancellationTokenToAction()
	{
		// Arrange
		var policy = NoOpRetryPolicy.Instance;
		using var cts = new CancellationTokenSource();
		CancellationToken receivedToken = default;

		// Act
		await policy.ExecuteAsync(
			ct =>
			{
				receivedToken = ct;
				return Task.FromResult(true);
			},
			cts.Token);

		// Assert
		receivedToken.ShouldBe(cts.Token);
	}

	[Fact]
	public void ImplementIRetryPolicy()
	{
		// Assert
		NoOpRetryPolicy.Instance.ShouldBeAssignableTo<IRetryPolicy>();
	}
}
