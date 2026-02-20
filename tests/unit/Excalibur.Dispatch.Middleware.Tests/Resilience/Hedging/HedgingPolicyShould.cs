// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience.Hedging;

/// <summary>
/// Unit tests for <see cref="HedgingPolicy"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class HedgingPolicyShould : UnitTestBase
{
	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new HedgingPolicy(null!));
	}

	[Fact]
	public void Constructor_AcceptsNullLogger()
	{
		// Arrange & Act & Assert - should not throw
		var policy = new HedgingPolicy(new HedgingOptions());
		policy.ShouldNotBeNull();
	}

	[Fact]
	public async Task ExecuteAsync_Generic_ReturnsResult_WhenOperationSucceeds()
	{
		// Arrange
		var policy = new HedgingPolicy(new HedgingOptions());

		// Act
		var result = await policy.ExecuteAsync(
			ct => Task.FromResult("success"),
			CancellationToken.None);

		// Assert
		result.ShouldBe("success");
	}

	[Fact]
	public async Task ExecuteAsync_Generic_ThrowsArgumentNullException_WhenOperationIsNull()
	{
		// Arrange
		var policy = new HedgingPolicy(new HedgingOptions());

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await policy.ExecuteAsync<string>(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_Void_CompletesSuccessfully()
	{
		// Arrange
		var policy = new HedgingPolicy(new HedgingOptions());
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
	public async Task ExecuteAsync_Void_ThrowsArgumentNullException_WhenOperationIsNull()
	{
		// Arrange
		var policy = new HedgingPolicy(new HedgingOptions());

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await policy.ExecuteAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_Generic_PropagatesNonHedgeableException()
	{
		// Arrange - ArgumentException is not hedgeable by default
		var policy = new HedgingPolicy(new HedgingOptions { Delay = TimeSpan.FromMilliseconds(50) });

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await policy.ExecuteAsync<string>(
				_ => throw new ArgumentException("not hedgeable"),
				CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_UsesCustomShouldHedgePredicate()
	{
		// Arrange - custom predicate that does NOT hedge TimeoutException
		var options = new HedgingOptions
		{
			ShouldHedge = _ => false,
			Delay = TimeSpan.FromMilliseconds(50),
		};
		var policy = new HedgingPolicy(options);

		// Act & Assert - should propagate since ShouldHedge returns false
		await Should.ThrowAsync<TimeoutException>(async () =>
			await policy.ExecuteAsync<string>(
				_ => throw new TimeoutException("no hedging"),
				CancellationToken.None));
	}
}
