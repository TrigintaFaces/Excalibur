// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Tests.Shared.Fixtures;

namespace Excalibur.Testing.Conformance.Tests.Testing;

/// <summary>
/// Locks the availability policy that container fixtures own on behalf of their consumers.
/// </summary>
/// <remarks>
/// <para>
/// The policy exists because it used to be DELEGATED. Two suites against the same emulator answered
/// "what happens when it is not there" in opposite ways — one asserted availability and failed, the
/// other skipped — so whether a given guarantee was actually proven depended on which convention its
/// author had copied. Centralising the decision on the fixture is only worth anything if the decision
/// itself is locked, which is what this suite does.
/// </para>
/// <para>
/// Both arms are required and neither is redundant. The SAFETY arm proves an unavailable container
/// FAILS rather than skips: a real-infrastructure lock that never ran must not contribute a pass it
/// did not earn. The LIVENESS arm proves the guard is not simply throwing at everything — a policy
/// that always threw would satisfy the safety arm perfectly while making every container suite
/// permanently red, and nothing in the safety arm alone can tell those two apart.
/// </para>
/// <para>
/// No container is started here. <see cref="ContainerFixtureBase"/> sets its availability flag from
/// whether <c>InitializeContainerAsync</c> returned, so a stub that returns — or throws — drives both
/// states deterministically, with no Docker dependency and no timing.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ContainerFixtureAvailabilityPolicyShould
{
	[Fact]
	public async Task PermitTheTestToRunWhenTheContainerStarted()
	{
		// LIVENESS. Without this, a policy hard-coded to throw would pass every other arm in this file
		// and take every container suite in the repository down with it.
		var fixture = new StubContainerFixture(failWith: null);
		await fixture.InitializeAsync();

		fixture.DockerAvailable.ShouldBeTrue("the stub container 'started', so the fixture is available");
		Should.NotThrow(fixture.EnsureAvailable);
	}

	[Fact]
	public async Task FailTheTestWhenTheContainerCouldNotStart()
	{
		// SAFETY, and the whole reason the policy moved onto the fixture: this must FAIL, not skip.
		var fixture = new StubContainerFixture(failWith: new InvalidOperationException("emulator refused to boot"));
		await fixture.InitializeAsync();

		fixture.DockerAvailable.ShouldBeFalse("the stub container failed to start");

		var thrown = Should.Throw<InvalidOperationException>(fixture.EnsureAvailable);

		// The message must carry the ORIGINAL cause. A guard that fails with only "unavailable" sends the
		// reader hunting for a flaky container when the real failure is recorded and sitting right here.
		thrown.Message.ShouldContain("emulator refused to boot");
	}

	[Fact]
	public async Task FailTheTestWhenADependentServiceFailedAfterTheContainerStarted()
	{
		// The half-started state: the container came up, then something it depends on did not. Reading
		// the availability flag directly is what let consumers miss this case; routing through the
		// fixture means they cannot.
		var fixture = new StubContainerFixture(failWith: null);
		await fixture.InitializeAsync();
		fixture.MarkUnavailable("queue creation failed");

		var thrown = Should.Throw<InvalidOperationException>(fixture.EnsureAvailable);
		thrown.Message.ShouldContain("queue creation failed");
	}

	/// <summary>A container fixture that starts, or fails to, on command — and never touches Docker.</summary>
	private sealed class StubContainerFixture(Exception? failWith) : ContainerFixtureBase
	{
		// Required: without graceful degradation the base RETHROWS, so the unavailable state this suite
		// exists to exercise would never be reachable through InitializeAsync.
		protected override bool AllowGracefulDegradation => true;

		protected override Task InitializeContainerAsync(CancellationToken cancellationToken) =>
			failWith is null ? Task.CompletedTask : Task.FromException(failWith);

		protected override Task DisposeContainerAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	}
}
