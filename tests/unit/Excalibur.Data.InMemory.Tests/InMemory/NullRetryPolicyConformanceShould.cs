// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Resilience;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Tests.Shared.Conformance;

using Excalibur.Data.InMemory;

namespace Excalibur.Data.Tests.InMemory;

/// <summary>
/// Conformance tests for NullRetryPolicy.
/// Verifies that the null/no-op retry policy correctly implements IDataRequestRetryPolicy.
/// </summary>
/// <remarks>
/// <para>
/// This test class demonstrates how to use the RetryPolicyConformanceTestBase
/// to verify that a retry policy implementation correctly follows the interface contract.
/// </para>
/// <para>
/// To create conformance tests for your own retry policy:
/// <list type="number">
///   <item>Inherit from RetryPolicyConformanceTestBase</item>
///   <item>Override CreatePolicy() to create an instance of your policy</item>
///   <item>Override CreateRetryableException() to return an exception your policy should retry</item>
///   <item>Override CreateNonRetryableException() to return an exception your policy should NOT retry</item>
///   <item>Override IsNullPolicy if testing a no-op policy (returns false for all ShouldRetry calls)</item>
/// </list>
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class NullRetryPolicyConformanceShould : RetryPolicyConformanceTestBase
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
}
