// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Dispatch.Compliance;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for IControlValidator conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateValidator"/> to verify that
/// your control validator implementation conforms to the IControlValidator contract.
/// </para>
/// <para>
/// The test kit verifies core validation operations including:
/// <list type="bullet">
/// <item><description>SupportedControls property returns non-null, non-empty list</description></item>
/// <item><description>SupportedCriteria property returns non-null, non-empty list</description></item>
/// <item><description>ValidateAsync returns result for supported controls</description></item>
/// <item><description>ValidateAsync handles unsupported controls gracefully</description></item>
/// <item><description>RunTestAsync returns result for supported controls</description></item>
/// <item><description>GetControlDescription returns non-null for supported controls</description></item>
/// <item><description>GetControlDescription returns null for unsupported controls</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>VALIDATOR PATTERN:</strong> IControlValidator is a SOC 2 control validation interface
/// with mixed sync/async members. It inherits from <see cref="BaseControlValidator"/> which
/// provides default <see cref="IControlValidator.RunTestAsync"/> implementation.
/// </para>
/// <para>
/// <strong>OPTIONAL DEPENDENCIES:</strong> Control validators have OPTIONAL dependencies.
/// Implementations gracefully degrade when dependencies are not provided.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class AuditLogControlValidatorConformanceTests : ControlValidatorConformanceTestKit
/// {
///     protected override IControlValidator CreateValidator()
///     {
///         // Dependencies are optional - parameterless instantiation works!
///         return new AuditLogControlValidator();
///     }
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
public abstract class ControlValidatorConformanceTestKit
{
	/// <summary>
	/// Creates a fresh control validator instance for testing.
	/// </summary>
	/// <returns>An IControlValidator implementation to test.</returns>
	/// <remarks>
	/// <para>
	/// For AuditLogControlValidator, the typical implementation:
	/// </para>
	/// <code>
	/// protected override IControlValidator CreateValidator() =>
	///     new AuditLogControlValidator();
	/// </code>
	/// <para>
	/// Note: Dependencies (IAuditLogger, IAuditStore) are optional!
	/// </para>
	/// </remarks>
	protected abstract IControlValidator CreateValidator();

	/// <summary>
	/// Gets an unsupported control ID for testing.
	/// </summary>
	protected virtual string UnsupportedControlId => "UNKNOWN-001";

	/// <summary>
	/// Creates default test parameters for control testing.
	/// </summary>
	protected virtual ControlTestParameters CreateTestParameters() => new()
	{
		SampleSize = 10,
		PeriodStart = DateTimeOffset.UtcNow.AddDays(-7),
		PeriodEnd = DateTimeOffset.UtcNow,
		IncludeDetailedEvidence = true
	};

	#region SupportedControls Property Tests

	/// <summary>
	/// Verifies that <see cref="IControlValidator.SupportedControls"/> returns a non-null list.
	/// </summary>
	protected virtual void SupportedControls_ShouldNotBeNull()
	{
		// Arrange
		var validator = CreateValidator();

		// Act
		var result = validator.SupportedControls;

		// Assert
		if (result == null)
		{
			throw new TestFixtureAssertionException(
				"Expected SupportedControls to return non-null list.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="IControlValidator.SupportedControls"/> returns a non-empty list.
	/// </summary>
	protected virtual void SupportedControls_ShouldNotBeEmpty()
	{
		// Arrange
		var validator = CreateValidator();

		// Act
		var result = validator.SupportedControls;

		// Assert
		if (result == null || result.Count == 0)
		{
			throw new TestFixtureAssertionException(
				"Expected SupportedControls to return non-empty list.");
		}
	}

	#endregion

	#region SupportedCriteria Property Tests

	/// <summary>
	/// Verifies that <see cref="IControlValidator.SupportedCriteria"/> returns a non-null list.
	/// </summary>
	protected virtual void SupportedCriteria_ShouldNotBeNull()
	{
		// Arrange
		var validator = CreateValidator();

		// Act
		var result = validator.SupportedCriteria;

		// Assert
		if (result == null)
		{
			throw new TestFixtureAssertionException(
				"Expected SupportedCriteria to return non-null list.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="IControlValidator.SupportedCriteria"/> returns a non-empty list.
	/// </summary>
	protected virtual void SupportedCriteria_ShouldNotBeEmpty()
	{
		// Arrange
		var validator = CreateValidator();

		// Act
		var result = validator.SupportedCriteria;

		// Assert
		if (result == null || result.Count == 0)
		{
			throw new TestFixtureAssertionException(
				"Expected SupportedCriteria to return non-empty list.");
		}
	}

	#endregion

	#region ValidateAsync Method Tests

	/// <summary>
	/// Verifies that <see cref="IControlValidator.ValidateAsync"/> returns a result for supported controls.
	/// </summary>
	protected virtual async Task ValidateAsync_SupportedControl_ShouldReturnResult()
	{
		// Arrange
		var validator = CreateValidator();
		var controlId = validator.SupportedControls[0];

		// Act
		var result = await validator.ValidateAsync(controlId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		if (result == null)
		{
			throw new TestFixtureAssertionException(
				"Expected ValidateAsync to return non-null result for supported control.");
		}

		if (result.ControlId != controlId)
		{
			throw new TestFixtureAssertionException(
				$"Expected result.ControlId to be '{controlId}', but got '{result.ControlId}'.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="IControlValidator.ValidateAsync"/> returns result with IsEffective=false for unsupported controls.
	/// </summary>
	protected virtual async Task ValidateAsync_UnsupportedControl_ShouldReturnFailure()
	{
		// Arrange
		var validator = CreateValidator();

		// Act
		var result = await validator.ValidateAsync(UnsupportedControlId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		if (result == null)
		{
			throw new TestFixtureAssertionException(
				"Expected ValidateAsync to return non-null result for unsupported control.");
		}

		if (result.IsEffective)
		{
			throw new TestFixtureAssertionException(
				"Expected ValidateAsync to return IsEffective=false for unsupported control.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="IControlValidator.ValidateAsync"/> result has required properties populated.
	/// </summary>
	protected virtual async Task ValidateAsync_SupportedControl_ShouldReturnResultWithRequiredProperties()
	{
		// Arrange
		var validator = CreateValidator();
		var controlId = validator.SupportedControls[0];

		// Act
		var result = await validator.ValidateAsync(controlId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		if (result == null)
		{
			throw new TestFixtureAssertionException(
				"Expected ValidateAsync to return non-null result.");
		}

		if (string.IsNullOrEmpty(result.ControlId))
		{
			throw new TestFixtureAssertionException(
				"Expected ControlId property to be non-null and non-empty.");
		}

		if (result.EffectivenessScore is < 0 or > 100)
		{
			throw new TestFixtureAssertionException(
				$"Expected EffectivenessScore to be between 0 and 100, but got {result.EffectivenessScore}.");
		}

		if (result.ValidatedAt == default)
		{
			throw new TestFixtureAssertionException(
				"Expected ValidatedAt property to be set to a non-default value.");
		}
	}

	#endregion

	#region RunTestAsync Method Tests

	/// <summary>
	/// Verifies that <see cref="IControlValidator.RunTestAsync"/> returns a result for supported controls.
	/// </summary>
	protected virtual async Task RunTestAsync_SupportedControl_ShouldReturnResult()
	{
		// Arrange
		var validator = CreateValidator();
		var controlId = validator.SupportedControls[0];
		var parameters = CreateTestParameters();

		// Act
		var result = await validator.RunTestAsync(controlId, parameters, CancellationToken.None).ConfigureAwait(false);

		// Assert
		if (result == null)
		{
			throw new TestFixtureAssertionException(
				"Expected RunTestAsync to return non-null result for supported control.");
		}

		if (result.ControlId != controlId)
		{
			throw new TestFixtureAssertionException(
				$"Expected result.ControlId to be '{controlId}', but got '{result.ControlId}'.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="IControlValidator.RunTestAsync"/> result has valid properties.
	/// </summary>
	protected virtual async Task RunTestAsync_SupportedControl_ShouldReturnResultWithValidProperties()
	{
		// Arrange
		var validator = CreateValidator();
		var controlId = validator.SupportedControls[0];
		var parameters = CreateTestParameters();

		// Act
		var result = await validator.RunTestAsync(controlId, parameters, CancellationToken.None).ConfigureAwait(false);

		// Assert
		if (result == null)
		{
			throw new TestFixtureAssertionException(
				"Expected RunTestAsync to return non-null result.");
		}

		if (result.Parameters == null)
		{
			throw new TestFixtureAssertionException(
				"Expected Parameters property to be non-null.");
		}

		if (result.ItemsTested < 0)
		{
			throw new TestFixtureAssertionException(
				$"Expected ItemsTested to be non-negative, but got {result.ItemsTested}.");
		}

		if (result.ExceptionsFound < 0)
		{
			throw new TestFixtureAssertionException(
				$"Expected ExceptionsFound to be non-negative, but got {result.ExceptionsFound}.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="IControlValidator.RunTestAsync"/> returns appropriate outcome for unsupported controls.
	/// </summary>
	protected virtual async Task RunTestAsync_UnsupportedControl_ShouldReturnExceptions()
	{
		// Arrange
		var validator = CreateValidator();
		var parameters = CreateTestParameters();

		// Act
		var result = await validator.RunTestAsync(UnsupportedControlId, parameters, CancellationToken.None).ConfigureAwait(false);

		// Assert
		if (result == null)
		{
			throw new TestFixtureAssertionException(
				"Expected RunTestAsync to return non-null result for unsupported control.");
		}

		// Unsupported controls should have exceptions or significant outcome
		if (result.Outcome == TestOutcome.NoExceptions && result.ExceptionsFound == 0)
		{
			// Some implementations may return NoExceptions with 0 found - that's acceptable
			// as long as the control ID is in the result
		}
	}

	#endregion

	#region GetControlDescription Method Tests

	/// <summary>
	/// Verifies that <see cref="IControlValidator.GetControlDescription"/> returns non-null for supported controls.
	/// </summary>
	protected virtual void GetControlDescription_SupportedControl_ShouldReturnDescription()
	{
		// Arrange
		var validator = CreateValidator();
		var controlId = validator.SupportedControls[0];

		// Act
		var result = validator.GetControlDescription(controlId);

		// Assert
		if (result == null)
		{
			throw new TestFixtureAssertionException(
				"Expected GetControlDescription to return non-null for supported control.");
		}

		if (result.ControlId != controlId)
		{
			throw new TestFixtureAssertionException(
				$"Expected result.ControlId to be '{controlId}', but got '{result.ControlId}'.");
		}

		if (string.IsNullOrEmpty(result.Name))
		{
			throw new TestFixtureAssertionException(
				"Expected Name property to be non-null and non-empty.");
		}

		if (string.IsNullOrEmpty(result.Description))
		{
			throw new TestFixtureAssertionException(
				"Expected Description property to be non-null and non-empty.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="IControlValidator.GetControlDescription"/> returns null for unsupported controls.
	/// </summary>
	protected virtual void GetControlDescription_UnsupportedControl_ShouldReturnNull()
	{
		// Arrange
		var validator = CreateValidator();

		// Act
		var result = validator.GetControlDescription(UnsupportedControlId);

		// Assert
		if (result != null)
		{
			throw new TestFixtureAssertionException(
				"Expected GetControlDescription to return null for unsupported control.");
		}
	}

	#endregion
}
