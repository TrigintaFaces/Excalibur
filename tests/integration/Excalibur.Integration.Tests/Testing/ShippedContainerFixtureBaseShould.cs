// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Integration.Tests.Testing;

/// <summary>
/// Covers the SHIPPED <see cref="Excalibur.Testing.Containers.ContainerFixtureBase"/> in Excalibur.Testing.Containers, which is the
/// copy a consumer gets from the package.
/// </summary>
/// <remarks>
/// A near-identical type lives in the test tree and had these defects fixed there first; the shipped
/// one kept them, and a grep for the fix returned hits from the wrong file. Two files sharing a name
/// is why this suite names the assembly it is about.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Testing")]
public sealed class ShippedContainerFixtureBaseShould
{
	[Fact]
	public void ReportDockerUnavailable_BeforeAnythingHasInitialized()
	{
		// Fail closed. A fixture that has not run its initialization has established nothing about
		// Docker, and a default of true means a suite whose fixture never ran still reads as though
		// its infrastructure were present.
		var fixture = new FakeContainerFixture();

		fixture.DockerAvailable.ShouldBeFalse(
			"an uninitialized fixture must not claim Docker is available; that reads as infrastructure "
			+ "being present to every arm that gates on it.");
	}

	[Fact]
	public async Task DisposeTheContainer_WhenMarkedUnavailableAfterStarting()
	{
		// The defect this arm exists for: disposal used to be gated on DockerAvailable, which
		// MarkUnavailable can clear AFTER a container is running. The container then leaked.
		var fixture = new FakeContainerFixture();

		await fixture.InitializeAsync();
		fixture.MarkUnavailable("a dependent service failed after the container had started");
		await fixture.DisposeAsync();

		fixture.DisposeContainerAsyncCallCount.ShouldBe(
			1,
			"a container that started must be disposed even once the fixture is marked unavailable, "
			+ "otherwise marking it unavailable leaks the container it had already started.");
	}

	[Fact]
	public async Task NotAttemptDisposal_WhenTheContainerNeverStarted()
	{
		var fixture = new FakeContainerFixture(failInitialization: true) { AllowDegradation = true };

		await fixture.InitializeAsync();
		await fixture.DisposeAsync();

		fixture.DisposeContainerAsyncCallCount.ShouldBe(
			0,
			"nothing started, so there is nothing to dispose; disposing here would call into a "
			+ "container the fixture never created.");
	}

	private sealed class FakeContainerFixture(bool failInitialization = false) : Excalibur.Testing.Containers.ContainerFixtureBase
	{
		// Fully qualified deliberately: a Tests.Shared type of the SAME NAME is in scope through the
		// global usings, and the two are different classes. That ambiguity is the defect this suite
		// exists for -- the shipped copy kept bugs the test-tree copy had already had fixed.
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
