// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Tests.Infrastructure;

/// <summary>
/// Covers <see cref="ContainerFixtureBase.DisposeAsync"/>'s disposal guard.
/// </summary>
/// <remarks>
/// The guard used to be <c>DockerAvailable</c>, which <see cref="ContainerFixtureBase.MarkUnavailable"/>
/// can flip to <c>false</c> AFTER a container has already started (its documented purpose is exactly
/// that: a dependent service, e.g. queue creation, failed post-start). On that path the container was
/// leaked -- disposal was skipped even though something was running. The fix gates disposal on whether
/// a container actually started, tracked independently of <c>DockerAvailable</c>.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class ContainerFixtureBaseShould
{
	[Fact]
	public async Task DisposeTheContainer_WhenMarkedUnavailableAfterStarting()
	{
		var fixture = new FakeContainerFixture();

		await fixture.InitializeAsync();
		fixture.MarkUnavailable("dependent service failed after container start");
		await fixture.DisposeAsync();

		fixture.DisposeContainerAsyncCallCount.ShouldBe(1);
	}

	[Fact]
	public async Task NotAttemptDisposal_WhenTheContainerNeverStarted()
	{
		var fixture = new FakeContainerFixture(failInitialization: true) { AllowDegradation = true };

		await fixture.InitializeAsync();
		await fixture.DisposeAsync();

		fixture.DisposeContainerAsyncCallCount.ShouldBe(0);
	}

	private sealed class FakeContainerFixture(bool failInitialization = false) : ContainerFixtureBase
	{
		public int DisposeContainerAsyncCallCount { get; private set; }

		public bool AllowDegradation { get; init; }

		protected override bool AllowGracefulDegradation => AllowDegradation;

		protected override Task InitializeContainerAsync(CancellationToken cancellationToken) =>
			failInitialization
				? throw new InvalidOperationException("simulated container start failure")
				: Task.CompletedTask;

		protected override Task DisposeContainerAsync(CancellationToken cancellationToken)
		{
			DisposeContainerAsyncCallCount++;
			return Task.CompletedTask;
		}
	}
}
