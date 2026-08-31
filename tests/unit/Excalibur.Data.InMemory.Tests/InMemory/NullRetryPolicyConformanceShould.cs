// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Resilience;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;


using Excalibur.Data.InMemory;

using Excalibur.Testing.Conformance;

namespace Excalibur.Data.Tests.InMemory;

/// <summary>
/// Conformance tests for NullRetryPolicy.
/// Verifies that the null/no-op retry policy correctly implements IDataRequestRetryPolicy.
/// </summary>
/// <remarks>
/// <para>
/// This test class demonstrates how to use the published RetryPolicyConformanceTestKit
/// to verify that a retry policy implementation correctly follows the interface contract.
/// </para>
/// <para>
/// To create conformance tests for your own retry policy:
/// <list type="number">
///   <item>Inherit from RetryPolicyConformanceTestKit</item>
///   <item>Override CreatePolicy() to create an instance of your policy</item>
///   <item>Override CreateRetryableException() to return an exception your policy should retry</item>
///   <item>Override CreateNonRetryableException() to return an exception your policy should NOT retry</item>
///   <item>Override IsNullPolicy if testing a no-op policy (returns false for all ShouldRetry calls)</item>
/// </list>
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class NullRetryPolicyConformanceShould : RetryPolicyConformanceTestKit
{
	/// <inheritdoc/>
	protected override bool IsNullPolicy => true;

	/// <inheritdoc/>
	protected override IDataRequestRetryPolicy CreatePolicy(int maxRetryAttempts)
	{
		// NullRetryPolicy is internal, so we access it via InMemoryPersistenceProvider.RetryPolicy
		var logger = A.Fake<ILogger<InMemoryPersistenceProvider>>();
		var options = Options.Create(new InMemoryProviderOptions { Name = "ConformanceTest" });
		using var provider = new InMemoryPersistenceProvider(options, logger);
		return provider.RetryPolicy;
	}

	/// <inheritdoc/>
	protected override Exception CreateRetryableException()
	{
		// NullRetryPolicy doesn't retry anything, but we need to provide an exception
		return new TimeoutException("Simulated timeout");
	}

	/// <inheritdoc/>
	protected override Exception CreateNonRetryableException()
	{
		return new ArgumentException("Invalid argument");
	}

	// ---------------------------------------------------------------------------------------------
	// Conformance arm wiring.
	//
	// The published kit ships without test-framework attributes so a consumer is not forced onto our
	// runner. Discovery is this suite's job: one attributed member per arm. An arm nobody wires never
	// executes, and an arm that never executes cannot fail -- in the results it is indistinguishable
	// from one that passed.
	// ---------------------------------------------------------------------------------------------

	[Fact] public void Policy_ShouldImplementIDataRequestRetryPolicy_Test() => Policy_ShouldImplementIDataRequestRetryPolicy();
	[Fact] public void MaxRetryAttempts_ShouldMatchConfiguredValue_Test() => MaxRetryAttempts_ShouldMatchConfiguredValue();
	[Fact] public void MaxRetryAttempts_ShouldBeNonNegative_Test() => MaxRetryAttempts_ShouldBeNonNegative();
	[Fact] public void BaseRetryDelay_ShouldBeNonNegative_Test() => BaseRetryDelay_ShouldBeNonNegative();
	[Fact] public void BaseRetryDelay_ForNullPolicy_ShouldBeZero_Test() => BaseRetryDelay_ForNullPolicy_ShouldBeZero();
	[Fact] public void ShouldRetry_WithRetryableException_ReturnsExpectedResult_Test() => ShouldRetry_WithRetryableException_ReturnsExpectedResult();
	[Fact] public void ShouldRetry_WithNonRetryableException_ReturnsFalse_Test() => ShouldRetry_WithNonRetryableException_ReturnsFalse();
	[Fact] public void BaseRetryDelay_ForNonNullPolicy_ShouldBePositive_Test() => BaseRetryDelay_ForNonNullPolicy_ShouldBePositive();
	[Fact] public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();

}
