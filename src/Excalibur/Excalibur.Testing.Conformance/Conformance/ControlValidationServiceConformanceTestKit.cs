// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Compliance;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for IControlValidationService conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateService"/> to verify that
/// your control validation service implementation conforms to the IControlValidationService contract.
/// </para>
/// <para>
/// The test kit verifies core validation operations including:
/// <list type="bullet">
/// <item><description>ValidateControlAsync returns result for registered controls</description></item>
/// <item><description>ValidateControlAsync handles unregistered controls gracefully</description></item>
/// <item><description>ValidateCriterionAsync returns results for all controls in a criterion</description></item>
/// <item><description>ValidateCriterionAsync returns empty for unregistered criteria</description></item>
/// <item><description>ValidateCriterionAsync reports a verdict for EACH control in a multi-control
/// criterion, rather than one control's verdict standing in for the whole criterion</description></item>
/// <item><description>RunControlTestAsync returns result for registered controls</description></item>
/// <item><description>RunControlTestAsync handles unregistered controls gracefully</description></item>
/// <item><description>GetAvailableControls returns non-null list</description></item>
/// <item><description>GetControlsForCriterion returns controls mapped to the criterion</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>SERVICE PATTERN:</strong> IControlValidationService is a SOC 2 control validation orchestrator
/// that coordinates multiple IControlValidator instances via collection injection.
/// </para>
/// <para>
/// <strong>COLLECTION INJECTION:</strong> The service constructor accepts <c>IEnumerable&lt;IControlValidator&gt;</c>.
/// For testing, provide at least one validator (e.g., <c>AuditLogControlValidator</c>).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class ControlValidationServiceConformanceTests : ControlValidationServiceConformanceTestKit
/// {
///     protected override IControlValidationService CreateService()
///     {
///         // Inject validators into the service
///         var validators = new[] { new AuditLogControlValidator() };
///         return new ControlValidationService(validators);
///     }
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
public abstract class ControlValidationServiceConformanceTestKit : ConformanceTestKit
{
	/// <summary>
	/// Creates a fresh control validation service instance for testing.
	/// </summary>
	/// <returns>An IControlValidationService implementation to test.</returns>
	/// <remarks>
	/// <para>
	/// For ControlValidationService, the typical implementation:
	/// </para>
	/// <code>
	/// protected override IControlValidationService CreateService()
	/// {
	///     var validators = new[] { new AuditLogControlValidator() };
	///     return new ControlValidationService(validators);
	/// }
	/// </code>
	/// </remarks>
	protected abstract IControlValidationService CreateService();

	/// <summary>
	/// Gets an unregistered control ID for testing.
	/// </summary>
	protected virtual string UnregisteredControlId => "UNREGISTERED-001";

	/// <summary>
	/// Gets an unregistered criterion for testing.
	/// </summary>
	/// <remarks>
	/// Uses P1_Notice (Privacy) criterion which is unlikely to have validators registered.
	/// </remarks>
	protected virtual TrustServicesCriterion UnregisteredCriterion => TrustServicesCriterion.P1_Notice;

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

	#region ValidateControlAsync Method Tests

	/// <summary>
	/// Verifies that <see cref="IControlValidationService.ValidateControlAsync"/> returns a result for registered controls.
	/// </summary>
	public virtual async Task ValidateControlAsync_RegisteredControl_ShouldReturnResult()
	{
		// Arrange
		var service = CreateService();
		var availableControls = service.GetAvailableControls();
		if (availableControls == null || availableControls.Count == 0)
		{
			throw new TestFixtureAssertionException(
				"Expected GetAvailableControls to return non-empty list for testing.");
		}

		var controlId = availableControls[0];

		// Act
		var result = await service.ValidateControlAsync(controlId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		if (result == null)
		{
			throw new TestFixtureAssertionException(
				"Expected ValidateControlAsync to return non-null result for registered control.");
		}

		if (result.ControlId != controlId)
		{
			throw new TestFixtureAssertionException(
				$"Expected result.ControlId to be '{controlId}', but got '{result.ControlId}'.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="IControlValidationService.ValidateControlAsync"/> returns result with required properties.
	/// </summary>
	public virtual async Task ValidateControlAsync_RegisteredControl_ShouldReturnResultWithRequiredProperties()
	{
		// Arrange
		var service = CreateService();
		var availableControls = service.GetAvailableControls();
		if (availableControls == null || availableControls.Count == 0)
		{
			throw new TestFixtureAssertionException(
				"Expected GetAvailableControls to return non-empty list for testing.");
		}

		var controlId = availableControls[0];

		// Act
		var result = await service.ValidateControlAsync(controlId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		if (result == null)
		{
			throw new TestFixtureAssertionException(
				"Expected ValidateControlAsync to return non-null result.");
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
	}

	/// <summary>
	/// Verifies that <see cref="IControlValidationService.ValidateControlAsync"/> returns failure for unregistered controls.
	/// </summary>
	public virtual async Task ValidateControlAsync_UnregisteredControl_ShouldReturnFailure()
	{
		// Arrange
		var service = CreateService();

		// Act
		var result = await service.ValidateControlAsync(UnregisteredControlId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		if (result == null)
		{
			throw new TestFixtureAssertionException(
				"Expected ValidateControlAsync to return non-null result for unregistered control.");
		}

		if (result.IsConfigured)
		{
			throw new TestFixtureAssertionException(
				"Expected ValidateControlAsync to return IsConfigured=false for unregistered control.");
		}

		if (result.ConfigurationIssues == null || result.ConfigurationIssues.Count == 0)
		{
			throw new TestFixtureAssertionException(
				"Expected ConfigurationIssues to be non-empty for unregistered control.");
		}
	}

	#endregion

	#region ValidateCriterionAsync Method Tests

	/// <summary>
	/// Verifies that <see cref="IControlValidationService.ValidateCriterionAsync"/> returns results for registered criterion.
	/// </summary>
	public virtual async Task ValidateCriterionAsync_RegisteredCriterion_ShouldReturnResults()
	{
		// Arrange
		var service = CreateService();
		var availableControls = service.GetAvailableControls();
		if (availableControls == null || availableControls.Count == 0)
		{
			throw new TestFixtureAssertionException(
				"Expected GetAvailableControls to return non-empty list for testing.");
		}

		// Use CC1_ControlEnvironment as it's commonly registered by AuditLogControlValidator
		var criterion = TrustServicesCriterion.CC1_ControlEnvironment;

		// Act
		var results = await service.ValidateCriterionAsync(criterion, CancellationToken.None).ConfigureAwait(false);

		// Assert
		if (results == null)
		{
			throw new TestFixtureAssertionException(
				"Expected ValidateCriterionAsync to return non-null list.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="IControlValidationService.ValidateCriterionAsync"/> validates all controls in criterion.
	/// </summary>
	public virtual async Task ValidateCriterionAsync_RegisteredCriterion_ShouldValidateAllControls()
	{
		// Arrange
		var service = CreateService();
		var criterion = TrustServicesCriterion.CC1_ControlEnvironment;
		var controlsForCriterion = service.GetControlsForCriterion(criterion);

		// Act
		var results = await service.ValidateCriterionAsync(criterion, CancellationToken.None).ConfigureAwait(false);

		// Assert
		if (results == null)
		{
			throw new TestFixtureAssertionException(
				"Expected ValidateCriterionAsync to return non-null list.");
		}

		if (results.Count != controlsForCriterion.Count)
		{
			throw new TestFixtureAssertionException(
				$"Expected {controlsForCriterion.Count} results but got {results.Count}.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="IControlValidationService.ValidateCriterionAsync" /> reports a verdict for
	/// EACH control in the criterion, rather than one control's verdict standing in for the criterion.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A criterion spans many controls, and those controls can be in mixed state. An implementation that
	/// examines one of them and returns that verdict is not folding the criterion — it is sampling it, and
	/// an external assessor reading the output cannot tell the difference. The result is a compliance
	/// statement that is true of one control and asserted of all of them.
	/// </para>
	/// <para>
	/// Counting results does not detect this. An implementation that repeats a single control's verdict
	/// once per control returns exactly the expected NUMBER of results, so a cardinality check passes while
	/// every verdict after the first is fabricated. This arm asserts the mapping instead: the control
	/// identifiers carried by the results must be the criterion's own controls, each appearing once. A
	/// repeated verdict collapses those identifiers and fails here.
	/// </para>
	/// <para>
	/// The arm selects the first criterion that maps two or more controls, because a criterion with one
	/// control cannot distinguish sampling from folding. If no criterion maps two, the fixture cannot
	/// exercise the property and this reports a fixture inadequacy rather than passing — a silent pass here
	/// would be indistinguishable from a provider that samples.
	/// </para>
	/// </remarks>
	public virtual async Task ValidateCriterionAsync_MultiControlCriterion_ShouldReportAVerdictPerControl()
	{
		// Arrange
		var service = CreateService();

		TrustServicesCriterion? subject = null;
		IReadOnlyList<string> expectedControls = [];

		foreach (var candidate in Enum.GetValues<TrustServicesCriterion>())
		{
			var controls = service.GetControlsForCriterion(candidate);
			if (controls is { Count: >= 2 })
			{
				subject = candidate;
				expectedControls = controls;
				break;
			}
		}

		if (subject is null)
		{
			throw new TestFixtureAssertionException(
				"No criterion maps two or more controls, so this arm cannot tell a per-control verdict from "
				+ "a single control's verdict repeated. Register validators so that at least one criterion "
				+ "covers two controls.");
		}

		// Act
		var results = await service.ValidateCriterionAsync(subject.Value, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		if (results == null)
		{
			throw new TestFixtureAssertionException(
				"Expected ValidateCriterionAsync to return non-null list.");
		}

		var reported = results.Select(static r => r.ControlId).ToList();
		var distinct = reported.Distinct(StringComparer.Ordinal).ToList();

		if (distinct.Count != reported.Count)
		{
			throw new TestFixtureAssertionException(
				$"Criterion {subject.Value} covers {expectedControls.Count} controls and "
				+ $"ValidateCriterionAsync reported {reported.Count} results carrying only "
				+ $"{distinct.Count} distinct control identifiers. A verdict was repeated, so at least one "
				+ "control was reported without being examined.");
		}

		var missing = expectedControls.Where(id => !reported.Contains(id, StringComparer.Ordinal)).ToList();
		if (missing.Count != 0)
		{
			throw new TestFixtureAssertionException(
				$"Criterion {subject.Value} covers control(s) {string.Join(", ", missing)}, but "
				+ "ValidateCriterionAsync reported no verdict for them. Every control the criterion covers "
				+ "must be accounted for in its result.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="IControlValidationService.ValidateCriterionAsync"/> returns empty for unregistered criterion.
	/// </summary>
	public virtual async Task ValidateCriterionAsync_UnregisteredCriterion_ShouldReturnEmpty()
	{
		// Arrange
		var service = CreateService();

		// Act
		var results = await service.ValidateCriterionAsync(UnregisteredCriterion, CancellationToken.None).ConfigureAwait(false);

		// Assert
		if (results == null)
		{
			throw new TestFixtureAssertionException(
				"Expected ValidateCriterionAsync to return non-null list for unregistered criterion.");
		}

		// May be empty if no validators registered for Privacy criterion
		// This is acceptable behavior
	}

	#endregion

	#region RunControlTestAsync Method Tests

	/// <summary>
	/// Verifies that <see cref="IControlValidationService.RunControlTestAsync"/> returns a result for registered controls.
	/// </summary>
	public virtual async Task RunControlTestAsync_RegisteredControl_ShouldReturnResult()
	{
		// Arrange
		var service = CreateService();
		var availableControls = service.GetAvailableControls();
		if (availableControls == null || availableControls.Count == 0)
		{
			throw new TestFixtureAssertionException(
				"Expected GetAvailableControls to return non-empty list for testing.");
		}

		var controlId = availableControls[0];
		var parameters = CreateTestParameters();

		// Act
		var result = await service.RunControlTestAsync(controlId, parameters, CancellationToken.None).ConfigureAwait(false);

		// Assert
		if (result == null)
		{
			throw new TestFixtureAssertionException(
				"Expected RunControlTestAsync to return non-null result for registered control.");
		}

		if (result.ControlId != controlId)
		{
			throw new TestFixtureAssertionException(
				$"Expected result.ControlId to be '{controlId}', but got '{result.ControlId}'.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="IControlValidationService.RunControlTestAsync"/> result has valid properties.
	/// </summary>
	public virtual async Task RunControlTestAsync_RegisteredControl_ShouldReturnResultWithValidProperties()
	{
		// Arrange
		var service = CreateService();
		var availableControls = service.GetAvailableControls();
		if (availableControls == null || availableControls.Count == 0)
		{
			throw new TestFixtureAssertionException(
				"Expected GetAvailableControls to return non-empty list for testing.");
		}

		var controlId = availableControls[0];
		var parameters = CreateTestParameters();

		// Act
		var result = await service.RunControlTestAsync(controlId, parameters, CancellationToken.None).ConfigureAwait(false);

		// Assert
		if (result == null)
		{
			throw new TestFixtureAssertionException(
				"Expected RunControlTestAsync to return non-null result.");
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
	/// Verifies that <see cref="IControlValidationService.RunControlTestAsync"/> reports an unregistered
	/// control as never tested, rather than as a control that was tested and failed.
	/// </summary>
	/// <remarks>
	/// An assessor reads this outcome as evidence. A control nobody exercised has produced no evidence
	/// either way, so reporting a failure attests to something that did not happen; the result must say
	/// only that no test ran, and say why.
	/// </remarks>
	public virtual async Task RunControlTestAsync_UnregisteredControl_ShouldReturnFailure()
	{
		// Arrange
		var service = CreateService();
		var parameters = CreateTestParameters();

		// Act
		var result = await service.RunControlTestAsync(UnregisteredControlId, parameters, CancellationToken.None).ConfigureAwait(false);

		// Assert
		if (result == null)
		{
			throw new TestFixtureAssertionException(
				"Expected RunControlTestAsync to return non-null result for unregistered control.");
		}

		if (result.Outcome != TestOutcome.NotTested)
		{
			throw new TestFixtureAssertionException(
				$"Expected Outcome to be NotTested for an unregistered control - no test ran, so neither "
				+ $"effectiveness nor failure was observed - but got {result.Outcome}.");
		}

		if (string.IsNullOrWhiteSpace(result.Notes))
		{
			throw new TestFixtureAssertionException(
				"Expected Notes to state why no test ran. A NotTested outcome with no reason tells an "
				+ "assessor nothing they can act on.");
		}

		if (result.Exceptions.Count != 0)
		{
			throw new TestFixtureAssertionException(
				$"Expected no exceptions for an unregistered control, but got {result.Exceptions.Count}. "
				+ "A test that never ran cannot have found an exception.");
		}
	}

	#endregion

	#region GetAvailableControls Method Tests

	/// <summary>
	/// Verifies that <see cref="IControlValidationService.GetAvailableControls"/> returns a non-null list.
	/// </summary>
	public virtual void GetAvailableControls_ShouldNotBeNull()
	{
		// Arrange
		var service = CreateService();

		// Act
		var result = service.GetAvailableControls();

		// Assert
		if (result == null)
		{
			throw new TestFixtureAssertionException(
				"Expected GetAvailableControls to return non-null list.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="IControlValidationService.GetAvailableControls"/> returns control IDs from validators.
	/// </summary>
	public virtual void GetAvailableControls_WithValidators_ShouldReturnControlIds()
	{
		// Arrange
		var service = CreateService();

		// Act
		var result = service.GetAvailableControls();

		// Assert
		if (result == null)
		{
			throw new TestFixtureAssertionException(
				"Expected GetAvailableControls to return non-null list.");
		}

		if (result.Count == 0)
		{
			throw new TestFixtureAssertionException(
				"Expected GetAvailableControls to return control IDs when validators are registered.");
		}

		// Verify control IDs are non-empty strings
		foreach (var controlId in result)
		{
			if (string.IsNullOrEmpty(controlId))
			{
				throw new TestFixtureAssertionException(
					"Expected all control IDs to be non-null and non-empty.");
			}
		}
	}

	#endregion

	#region GetControlsForCriterion Method Tests

	/// <summary>
	/// Verifies that <see cref="IControlValidationService.GetControlsForCriterion"/> returns a non-null list.
	/// </summary>
	public virtual void GetControlsForCriterion_ShouldNotBeNull()
	{
		// Arrange
		var service = CreateService();
		var criterion = TrustServicesCriterion.CC1_ControlEnvironment;

		// Act
		var result = service.GetControlsForCriterion(criterion);

		// Assert
		if (result == null)
		{
			throw new TestFixtureAssertionException(
				"Expected GetControlsForCriterion to return non-null list.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="IControlValidationService.GetControlsForCriterion"/> returns controls for registered criterion.
	/// </summary>
	public virtual void GetControlsForCriterion_RegisteredCriterion_ShouldReturnControls()
	{
		// Arrange
		var service = CreateService();
		var criterion = TrustServicesCriterion.CC1_ControlEnvironment;

		// Act
		var result = service.GetControlsForCriterion(criterion);

		// Assert
		if (result == null)
		{
			throw new TestFixtureAssertionException(
				"Expected GetControlsForCriterion to return non-null list.");
		}

		// With AuditLogControlValidator registered, CC1_ControlEnvironment should have controls
		if (result.Count == 0)
		{
			throw new TestFixtureAssertionException(
				"Expected GetControlsForCriterion to return controls for registered criterion.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="IControlValidationService.GetControlsForCriterion"/> returns empty for unregistered criterion.
	/// </summary>
	public virtual void GetControlsForCriterion_UnregisteredCriterion_ShouldReturnEmpty()
	{
		// Arrange
		var service = CreateService();

		// Act
		var result = service.GetControlsForCriterion(UnregisteredCriterion);

		// Assert
		if (result == null)
		{
			throw new TestFixtureAssertionException(
				"Expected GetControlsForCriterion to return non-null list for unregistered criterion.");
		}

		if (result.Count != 0)
		{
			throw new TestFixtureAssertionException(
				"Expected GetControlsForCriterion to return empty list for unregistered criterion.");
		}
	}

	#endregion
}
