// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Compliance;
using Excalibur.Compliance.Soc2.Validators;

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
public abstract class ControlValidatorConformanceTestKit : ConformanceTestKit
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
	public virtual void SupportedControls_ShouldNotBeNull()
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
	public virtual void SupportedControls_ShouldNotBeEmpty()
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
	public virtual void SupportedCriteria_ShouldNotBeNull()
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
	public virtual void SupportedCriteria_ShouldNotBeEmpty()
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
	public virtual async Task ValidateAsync_SupportedControl_ShouldReturnResult()
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
	/// Verifies that <see cref="IControlValidator.ValidateAsync"/> reports <c>NotVerified</c> for a control the
	/// validator does not support.
	/// </summary>
	/// <remarks>
	/// A control the validator does not support cannot have been examined, so neither verdict is available to it.
	/// <c>Effective</c> is an assurance nobody earned; <c>Deficient</c> is an accusation nobody earned, and it is the
	/// more damaging of the two, because a deficiency travels into the report a consumer hands to an assessor as a
	/// finding against them. <c>NotVerified</c> is the only outcome the validator is entitled to produce here.
	/// </remarks>
	public virtual async Task ValidateAsync_UnsupportedControl_ShouldReturnFailure()
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

		if (result.Outcome != ControlOutcome.NotVerified)
		{
			throw new TestFixtureAssertionException(
				$"Expected ValidateAsync to report NotVerified for an unsupported control, but it "
				+ $"reported {result.Outcome}. A control the validator does not support cannot have been "
				+ "examined, so it may claim neither verdict: Effective is an assurance nobody earned, and "
				+ "Deficient is an accusation nobody earned. A deficiency reaches the assessor as a finding "
				+ "against the consumer, so reporting one for a control that was never examined is the more "
				+ "damaging of the two errors. NotVerified is the only outcome available here.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="IControlValidator.ValidateAsync"/> result has required properties populated.
	/// </summary>
	public virtual async Task ValidateAsync_SupportedControl_ShouldReturnResultWithRequiredProperties()
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

		// This used to test "is the score between 0 and 100", which no validator could fail: the
		// interesting wrong answers -- 75, 50, 30 -- are all inside that range, and the shipped report
		// reads every one of them as a deficiency the consumer never earned. A range check over a
		// continuous-looking scale cannot detect a wrong point on that scale. These two can.
		if (!Enum.IsDefined(result.EffectivenessScore))
		{
			throw new TestFixtureAssertionException(
				$"Expected the reported effectiveness to be a declared ControlEffectiveness band, but got "
				+ $"the undeclared value {(int)result.EffectivenessScore}. The bands are a closed, ordered "
				+ "set -- a value outside it has no place in that order, so nothing downstream can say what "
				+ "it means, and a plausible-looking number reaches an external assessor as a finding "
				+ "against you.");
		}

		var impliedByBand = result.EffectivenessScore switch
		{
			ControlEffectiveness.Effective => ControlOutcome.Effective,
			ControlEffectiveness.Unverified => ControlOutcome.NotVerified,
			_ => ControlOutcome.Deficient,
		};

		if (result.Outcome != impliedByBand)
		{
			throw new TestFixtureAssertionException(
				$"Expected Outcome to be {impliedByBand}, which is what a band of "
				+ $"{result.EffectivenessScore} asserts, but the result reports {result.Outcome}. A verdict "
				+ "that contradicts its own band is a self-contradictory result, and it travels into the "
				+ "document handed to an external assessor.");
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
	public virtual async Task RunTestAsync_SupportedControl_ShouldReturnResult()
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
	public virtual async Task RunTestAsync_SupportedControl_ShouldReturnResultWithValidProperties()
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
	/// Verifies that <see cref="IControlValidator.RunTestAsync"/> does not report a clean pass for a
	/// control it does not support.
	/// </summary>
	/// <remarks>
	/// This arm was named <c>ShouldReturnExceptions</c> and its body was an <c>if</c> with nothing in
	/// it, so every implementation passed it, including one that fabricated a clean result. Tightening it
	/// to reject <see cref="TestOutcome.NoExceptions"/> closed the fabricated-assurance half and left the
	/// fabricated-accusation half open: a validator could report <see cref="TestOutcome.ControlFailure"/>
	/// or <see cref="TestOutcome.SignificantExceptions"/> about a control it never examined and still pass
	/// conformance. That is the more damaging direction, and this kit's own sibling arm
	/// (<see cref="ValidateAsync_UnsupportedControl_ShouldReturnFailure"/>) already says so: an accusation
	/// nobody earned travels into the SOC 2 report a consumer hands an assessor, as a finding against them.
	/// A validator that ran no test may claim neither verdict, so <see cref="TestOutcome.NotTested"/> is the
	/// only outcome available here - matching the sibling's single-value requirement rather than excluding
	/// one value and permitting the rest.
	/// </remarks>
	public virtual async Task RunTestAsync_UnsupportedControl_ShouldNotFabricateAPass()
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

		if (result.Outcome != TestOutcome.NotTested)
		{
			throw new TestFixtureAssertionException(
				$"Expected RunTestAsync to report NotTested for an unsupported control, but it reported "
				+ $"{result.Outcome}. A control the validator does not support cannot have been examined, so "
				+ "it may claim neither direction: NoExceptions attests to a verification that did not happen, "
				+ "and ControlFailure or SignificantExceptions accuse a control nobody tested. Soc2Report "
				+ "collects every non-NoExceptions outcome into the report findings, so a fabricated exception "
				+ "reaches an external assessor as a finding against the consumer. NotTested says only what is "
				+ "true.");
		}
	}

	#endregion

	#region GetControlDescription Method Tests

	/// <summary>
	/// Verifies that <see cref="IControlValidator.GetControlDescription"/> returns non-null for supported controls.
	/// </summary>
	public virtual void GetControlDescription_SupportedControl_ShouldReturnDescription()
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
	public virtual void GetControlDescription_UnsupportedControl_ShouldReturnNull()
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
