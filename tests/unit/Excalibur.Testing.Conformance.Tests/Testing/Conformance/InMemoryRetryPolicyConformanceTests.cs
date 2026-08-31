// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Resilience;
using Excalibur.Testing.Conformance;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Self-test proving <see cref="RetryPolicyConformanceTestKit"/> runs end-to-end against a sample
/// custom <see cref="IDataRequestRetryPolicy"/> implementation and reports pass/fail (wired-and-tested).
/// </summary>
/// <remarks>
/// Uses a minimal in-memory reference policy (retries on <see cref="InvalidOperationException"/>, does not
/// retry on <see cref="ArgumentException"/>) so the retryable, non-retryable, and value branches of the
/// kit are all exercised — not merely that the kit type is public.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "PROVIDER")]
public sealed class InMemoryRetryPolicyConformanceTests : RetryPolicyConformanceTestKit
{
	/// <inheritdoc />
	protected override IDataRequestRetryPolicy CreatePolicy(int maxRetryAttempts) =>
		new ReferenceRetryPolicy(maxRetryAttempts);

	/// <inheritdoc />
	protected override Exception CreateRetryableException() => new InvalidOperationException("transient");

	/// <inheritdoc />
	protected override Exception CreateNonRetryableException() => new ArgumentException("permanent");

	[Fact]
	public void Policy_ShouldImplementIDataRequestRetryPolicy_Test() =>
		Policy_ShouldImplementIDataRequestRetryPolicy();

	[Fact]
	public void MaxRetryAttempts_ShouldMatchConfiguredValue_Test() =>
		MaxRetryAttempts_ShouldMatchConfiguredValue();

	[Fact]
	public void MaxRetryAttempts_ShouldBeNonNegative_Test() =>
		MaxRetryAttempts_ShouldBeNonNegative();

	[Fact]
	public void BaseRetryDelay_ShouldBeNonNegative_Test() =>
		BaseRetryDelay_ShouldBeNonNegative();

	[Fact]
	public void BaseRetryDelay_ForNullPolicy_ShouldBeZero_Test() =>
		BaseRetryDelay_ForNullPolicy_ShouldBeZero();

	[Fact]
	public void ShouldRetry_WithRetryableException_ReturnsExpectedResult_Test() =>
		ShouldRetry_WithRetryableException_ReturnsExpectedResult();

	[Fact]
	public void ShouldRetry_WithNonRetryableException_ReturnsFalse_Test() =>
		ShouldRetry_WithNonRetryableException_ReturnsFalse();

	[Fact]
	public void BaseRetryDelay_ForNonNullPolicy_ShouldBePositive_Test() =>
		BaseRetryDelay_ForNonNullPolicy_ShouldBePositive();

	/// <summary>
	/// Minimal in-memory reference retry policy used only to exercise the conformance kit.
	/// </summary>
	private sealed class ReferenceRetryPolicy(int maxRetryAttempts) : IDataRequestRetryPolicy
	{
		public int MaxRetryAttempts { get; } = maxRetryAttempts;

		public TimeSpan BaseRetryDelay { get; } = TimeSpan.FromMilliseconds(50);

		public bool ShouldRetry(Exception exception) => exception is InvalidOperationException;
	}

	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();
}
