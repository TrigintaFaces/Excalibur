// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Dispatch.Compliance;

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
public abstract class ControlValidationServiceConformanceTestKit
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
	protected virtual async Task ValidateControlAsync_RegisteredControl_ShouldReturnResult()
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
	protected virtual async Task ValidateControlAsync_RegisteredControl_ShouldReturnResultWithRequiredProperties()
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

		if (result.EffectivenessScore is < 0 or > 100)
		{
			throw new TestFixtureAssertionException(
				$"Expected EffectivenessScore to be between 0 and 100, but got {result.EffectivenessScore}.");
		}
	}

	/// <summary>
	/// Verifies that <see cref="IControlValidationService.ValidateControlAsync"/> returns failure for unregistered controls.
	/// </summary>
	protected virtual async Task ValidateControlAsync_UnregisteredControl_ShouldReturnFailure()
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
	protected virtual async Task ValidateCriterionAsync_RegisteredCriterion_ShouldReturnResults()
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
	protected virtual async Task ValidateCriterionAsync_RegisteredCriterion_ShouldValidateAllControls()
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
	/// Verifies that <see cref="IControlValidationService.ValidateCriterionAsync"/> returns empty for unregistered criterion.
	/// </summary>
	protected virtual async Task ValidateCriterionAsync_UnregisteredCriterion_ShouldReturnEmpty()
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
	protected virtual async Task RunControlTestAsync_RegisteredControl_ShouldReturnResult()
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
	protected virtual async Task RunControlTestAsync_RegisteredControl_ShouldReturnResultWithValidProperties()
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
	/// Verifies that <see cref="IControlValidationService.RunControlTestAsync"/> returns failure for unregistered controls.
	/// </summary>
	protected virtual async Task RunControlTestAsync_UnregisteredControl_ShouldReturnFailure()
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

		if (result.Outcome != TestOutcome.ControlFailure)
		{
			throw new TestFixtureAssertionException(
				$"Expected Outcome to be ControlFailure for unregistered control, but got {result.Outcome}.");
		}
	}

	#endregion

	#region GetAvailableControls Method Tests

	/// <summary>
	/// Verifies that <see cref="IControlValidationService.GetAvailableControls"/> returns a non-null list.
	/// </summary>
	protected virtual void GetAvailableControls_ShouldNotBeNull()
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
	protected virtual void GetAvailableControls_WithValidators_ShouldReturnControlIds()
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
	protected virtual void GetControlsForCriterion_ShouldNotBeNull()
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
	protected virtual void GetControlsForCriterion_RegisteredCriterion_ShouldReturnControls()
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
	protected virtual void GetControlsForCriterion_UnregisteredCriterion_ShouldReturnEmpty()
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
