// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Implementation;

namespace Excalibur.Saga.Tests.Abstractions;

/// <summary>
/// Sprint 616 F.1 / Sprint 617 A.1: Tests for Saga API cleanup -- IRetryPolicy deleted (greenfield),
/// ISagaRetryPolicy canonical, and ParallelFailurePolicy enum correctness.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaApiCleanupShould
{
	#region A.1: IRetryPolicy Deleted (Sprint 617)

	[Fact]
	public void NotHaveIRetryPolicyInSagaAbstractions()
	{
		// Sprint 617 A.1: IRetryPolicy deleted outright (greenfield, no consumers)
		var sagaAssembly = typeof(ISagaRetryPolicy).Assembly;
		var retryPolicyType = sagaAssembly.GetType("Excalibur.Saga.Abstractions.IRetryPolicy");

		retryPolicyType.ShouldBeNull("IRetryPolicy must be deleted -- use ISagaRetryPolicy instead");
	}

	[Fact]
	public void NotMarkISagaRetryPolicyAsObsolete()
	{
		// Arrange
		var type = typeof(ISagaRetryPolicy);

		// Act
		var obsoleteAttr = type.GetCustomAttribute<ObsoleteAttribute>();

		// Assert
		obsoleteAttr.ShouldBeNull("ISagaRetryPolicy is the canonical interface and must NOT be obsolete");
	}

	[Fact]
	public void ReturnISagaRetryPolicyFromISagaDefinition()
	{
		// Arrange
		var retryPolicyProp = typeof(ISagaDefinition<>).GetProperty("RetryPolicy");

		// Assert
		retryPolicyProp.ShouldNotBeNull();
		retryPolicyProp.PropertyType.ShouldBe(typeof(ISagaRetryPolicy),
			"ISagaDefinition<T>.RetryPolicy must return ISagaRetryPolicy, not IRetryPolicy");
	}

	[Fact]
	public void ImplementISagaRetryPolicyOnDefaultSagaRetryPolicy()
	{
		// Assert
		typeof(ISagaRetryPolicy).IsAssignableFrom(typeof(DefaultSagaRetryPolicy)).ShouldBeTrue(
			"DefaultSagaRetryPolicy must implement ISagaRetryPolicy");
	}

	#endregion

	#region A.2: ParallelFailurePolicy Enum

	[Fact]
	public void DefineThreeParallelFailurePolicyValues()
	{
		// Act
		var values = Enum.GetValues<ParallelFailurePolicy>();

		// Assert
		values.Length.ShouldBe(3);
	}

	[Theory]
	[InlineData(ParallelFailurePolicy.FailFast, 0)]
	[InlineData(ParallelFailurePolicy.ContinueOnFailure, 1)]
	[InlineData(ParallelFailurePolicy.RequireAll, 2)]
	public void HaveExpectedEnumValues(ParallelFailurePolicy policy, int expectedValue)
	{
		((int)policy).ShouldBe(expectedValue);
	}

	[Fact]
	public void ExposeFailurePolicyOnIParallelSagaStep()
	{
		// Arrange
		var prop = typeof(IParallelSagaStep<>).GetProperty("FailurePolicy");

		// Assert
		prop.ShouldNotBeNull("IParallelSagaStep<T> must expose FailurePolicy property");
		prop.PropertyType.ShouldBe(typeof(ParallelFailurePolicy));
	}

	[Fact]
	public void NotExposeBooleanTrapProperties()
	{
		// Verify the old boolean properties are removed from the interface
		var requireAllSuccess = typeof(IParallelSagaStep<>).GetProperty("RequireAllSuccess");
		var continueOnFailure = typeof(IParallelSagaStep<>).GetProperty("ContinueOnFailure");

		requireAllSuccess.ShouldBeNull("RequireAllSuccess boolean trap must be removed");
		continueOnFailure.ShouldBeNull("ContinueOnFailure boolean trap must be removed");
	}

	[Fact]
	public void DefaultToRequireAllFailurePolicy()
	{
		// Arrange
		var step = A.Fake<ISagaStep<TestSagaData>>();
		A.CallTo(() => step.Name).Returns("Test");
		A.CallTo(() => step.Timeout).Returns(TimeSpan.FromMinutes(1));

		var sut = new ParallelSagaStep<TestSagaData>(
			"TestStep",
			[step],
			null);

		// Assert
		sut.FailurePolicy.ShouldBe(ParallelFailurePolicy.RequireAll,
			"Default failure policy should be RequireAll for backward compatibility");
	}

	#endregion
}
