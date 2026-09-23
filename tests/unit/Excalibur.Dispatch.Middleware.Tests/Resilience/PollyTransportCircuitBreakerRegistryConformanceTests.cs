// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Resilience.Polly;
using Excalibur.Dispatch.Testing.Resilience;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Runs the shared registry conformance suite against the resilience-package registry.
/// </summary>
/// <remarks>
/// This is the implementation a consumer receives merely by adding the package reference — the container
/// removes the in-box registry and registers this one. Binding both to one suite is what makes that
/// silent substitution safe.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PollyTransportCircuitBreakerRegistryConformanceTests
	: TransportCircuitBreakerRegistryConformanceTests
{
	/// <inheritdoc />
	protected override ITransportCircuitBreakerRegistry CreateRegistry() =>
		new PollyTransportCircuitBreakerRegistry(new CircuitBreakerOptions(), null);

	[Fact] public void GetOrCreate_WithABlankTransportName_ShouldThrowArgumentException_Test() => VerifyABlankTransportNameIsRefused();
	[Fact] public void GetOrCreate_WithNullOptions_ShouldThrowArgumentNullException_Test() => VerifyNullOptionsAreRefused();
	[Fact] public void GetOrCreate_ShouldAcceptEveryValueTheOptionsTypeAdmits_Test() => VerifyEveryValueTheOptionsTypeAdmitsIsAccepted();
	[Fact] public Task GetOrCreate_ShouldReturnAUsablePolicyThatStartsClosed_Test() => VerifyANewPolicyIsUsableAndStartsClosed();
	[Fact] public void GetOrCreate_WithTheSameName_ShouldReturnTheSameInstance_Test() => VerifyTheSameNameReturnsTheSamePolicy();
	[Fact] public void GetOrCreate_WithDifferentlyCasedNames_ShouldReturnTheSameInstance_Test() => VerifyTransportNamesAreMatchedWithoutRegardToCase();
	[Fact] public void TryGet_ForAnUnknownName_ShouldReturnNull_Test() => VerifyAnUnknownNameIsAbsentRatherThanFabricated();
	[Fact] public void BoundedRegistryNeverSharesOneCircuitBetweenTransports_Test() => VerifyABoundedRegistryNeverSharesOneCircuitBetweenTransports();
	[Fact] public Task Policy_ShouldOpenOnTheConfiguredThreshold_ThenAdmitAProbe_Test() => VerifyTheCircuitOpensOnTheConfiguredThresholdThenAdmitsAProbe();
	[Fact] public Task Reset_ShouldReturnAnOpenedCircuitToClosed_Test() => VerifyResetReturnsAnOpenedCircuitToService();

	/// <summary>
	/// Records that the failed-close postcondition is UNVERIFIED for the resilience-package adapter: it has no
	/// reachable close-failure path, so the suite cannot bind the clause here.
	/// </summary>
	/// <remarks>
	/// This is deliberately not a silent pass. The arm answers PASSED, FAILED or NOT DETERMINED, and
	/// this asserts the third — so if someone later gives this registry a failing close without also
	/// overriding the hook that binds the clause, this goes RED and says so.
	/// </remarks>
	[Fact]
	public async Task FailedClose_IsNotDeterminedForThisRegistry_Test() =>
		(await VerifyAFailedCloseSurfacesAsAFaultedTask()).ShouldBeFalse(
			"this registry has no reachable close-failure path, so the clause is unverified for it — "
			+ "if that has changed, override CreatePolicyWhoseCloseFails() so the clause is actually bound");
}
