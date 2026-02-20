// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


// SOC 2 Trust Services Criteria use industry-standard naming with underscores (CC1, A1, PI1, etc.)
#pragma warning disable CA1707 // Identifiers should not contain underscores

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// SOC 2 Trust Services Criteria categories.
/// </summary>
public enum TrustServicesCriterion
{
	// Security (Common Criteria)
	/// <summary>CC1: Control Environment.</summary>
	CC1_ControlEnvironment,
	/// <summary>CC2: Communication and Information.</summary>
	CC2_Communication,
	/// <summary>CC3: Risk Assessment.</summary>
	CC3_RiskAssessment,
	/// <summary>CC4: Monitoring Activities.</summary>
	CC4_Monitoring,
	/// <summary>CC5: Control Activities.</summary>
	CC5_ControlActivities,
	/// <summary>CC6: Logical and Physical Access Controls.</summary>
	CC6_LogicalAccess,
	/// <summary>CC7: System Operations.</summary>
	CC7_SystemOperations,
	/// <summary>CC8: Change Management.</summary>
	CC8_ChangeManagement,
	/// <summary>CC9: Risk Mitigation.</summary>
	CC9_RiskMitigation,

	// Availability
	/// <summary>A1: Infrastructure and Software Management.</summary>
	A1_InfrastructureManagement,
	/// <summary>A2: Capacity Management.</summary>
	A2_CapacityManagement,
	/// <summary>A3: Backup and Recovery.</summary>
	A3_BackupRecovery,

	// Processing Integrity
	/// <summary>PI1: Input Validation.</summary>
	PI1_InputValidation,
	/// <summary>PI2: Processing Accuracy.</summary>
	PI2_ProcessingAccuracy,
	/// <summary>PI3: Output Completeness.</summary>
	PI3_OutputCompleteness,

	// Confidentiality
	/// <summary>C1: Data Classification.</summary>
	C1_DataClassification,
	/// <summary>C2: Data Protection.</summary>
	C2_DataProtection,
	/// <summary>C3: Data Disposal.</summary>
	C3_DataDisposal,

	// Privacy
	/// <summary>P1: Notice.</summary>
	P1_Notice,
	/// <summary>P2: Choice and Consent.</summary>
	P2_ChoiceConsent,
	/// <summary>P3: Collection.</summary>
	P3_Collection,
	/// <summary>P4: Use and Retention.</summary>
	P4_UseRetention,
	/// <summary>P5: Access.</summary>
	P5_Access,
	/// <summary>P6: Disclosure.</summary>
	P6_Disclosure,
	/// <summary>P7: Quality.</summary>
	P7_Quality,
	/// <summary>P8: Monitoring and Enforcement.</summary>
	P8_MonitoringEnforcement
}

/// <summary>
/// SOC 2 Trust Services Categories.
/// </summary>
public enum TrustServicesCategory
{
	/// <summary>Required for all SOC 2 reports - protection against unauthorized access.</summary>
	Security,

	/// <summary>Optional - system uptime and reliability.</summary>
	Availability,

	/// <summary>Optional - data processing accuracy and completeness.</summary>
	ProcessingIntegrity,

	/// <summary>Optional - protection of confidential information.</summary>
	Confidentiality,

	/// <summary>Optional - personal information handling.</summary>
	Privacy
}

/// <summary>
/// Extension methods for Trust Services Criteria.
/// </summary>
public static class TrustServicesCriteriaExtensions
{
	/// <summary>
	/// Gets the category for a specific criterion.
	/// </summary>
	/// <param name="criterion">The criterion.</param>
	/// <returns>The Trust Services category.</returns>
	public static TrustServicesCategory GetCategory(this TrustServicesCriterion criterion)
	{
		return criterion switch
		{
			>= TrustServicesCriterion.CC1_ControlEnvironment and
			<= TrustServicesCriterion.CC9_RiskMitigation => TrustServicesCategory.Security,

			>= TrustServicesCriterion.A1_InfrastructureManagement and
			<= TrustServicesCriterion.A3_BackupRecovery => TrustServicesCategory.Availability,

			>= TrustServicesCriterion.PI1_InputValidation and
			<= TrustServicesCriterion.PI3_OutputCompleteness => TrustServicesCategory.ProcessingIntegrity,

			>= TrustServicesCriterion.C1_DataClassification and
			<= TrustServicesCriterion.C3_DataDisposal => TrustServicesCategory.Confidentiality,

			>= TrustServicesCriterion.P1_Notice and
			<= TrustServicesCriterion.P8_MonitoringEnforcement => TrustServicesCategory.Privacy,

			_ => throw new ArgumentOutOfRangeException(nameof(criterion))
		};
	}

	/// <summary>
	/// Gets the display name for a criterion.
	/// </summary>
	/// <param name="criterion">The criterion.</param>
	/// <returns>A human-readable name.</returns>
	public static string GetDisplayName(this TrustServicesCriterion criterion)
	{
		return criterion switch
		{
			TrustServicesCriterion.CC1_ControlEnvironment => "CC1 - Control Environment",
			TrustServicesCriterion.CC2_Communication => "CC2 - Communication and Information",
			TrustServicesCriterion.CC3_RiskAssessment => "CC3 - Risk Assessment",
			TrustServicesCriterion.CC4_Monitoring => "CC4 - Monitoring Activities",
			TrustServicesCriterion.CC5_ControlActivities => "CC5 - Control Activities",
			TrustServicesCriterion.CC6_LogicalAccess => "CC6 - Logical and Physical Access",
			TrustServicesCriterion.CC7_SystemOperations => "CC7 - System Operations",
			TrustServicesCriterion.CC8_ChangeManagement => "CC8 - Change Management",
			TrustServicesCriterion.CC9_RiskMitigation => "CC9 - Risk Mitigation",
			TrustServicesCriterion.A1_InfrastructureManagement => "A1 - Infrastructure Management",
			TrustServicesCriterion.A2_CapacityManagement => "A2 - Capacity Management",
			TrustServicesCriterion.A3_BackupRecovery => "A3 - Backup and Recovery",
			TrustServicesCriterion.PI1_InputValidation => "PI1 - Input Validation",
			TrustServicesCriterion.PI2_ProcessingAccuracy => "PI2 - Processing Accuracy",
			TrustServicesCriterion.PI3_OutputCompleteness => "PI3 - Output Completeness",
			TrustServicesCriterion.C1_DataClassification => "C1 - Data Classification",
			TrustServicesCriterion.C2_DataProtection => "C2 - Data Protection",
			TrustServicesCriterion.C3_DataDisposal => "C3 - Data Disposal",
			TrustServicesCriterion.P1_Notice => "P1 - Notice",
			TrustServicesCriterion.P2_ChoiceConsent => "P2 - Choice and Consent",
			TrustServicesCriterion.P3_Collection => "P3 - Collection",
			TrustServicesCriterion.P4_UseRetention => "P4 - Use and Retention",
			TrustServicesCriterion.P5_Access => "P5 - Access",
			TrustServicesCriterion.P6_Disclosure => "P6 - Disclosure",
			TrustServicesCriterion.P7_Quality => "P7 - Quality",
			TrustServicesCriterion.P8_MonitoringEnforcement => "P8 - Monitoring and Enforcement",
			_ => criterion.ToString()
		};
	}

	/// <summary>
	/// Gets all criteria for a specific category.
	/// </summary>
	/// <param name="category">The category.</param>
	/// <returns>All criteria belonging to the category.</returns>
	public static IEnumerable<TrustServicesCriterion> GetCriteria(this TrustServicesCategory category)
	{
		return category switch
		{
			TrustServicesCategory.Security => new[]
			{
				TrustServicesCriterion.CC1_ControlEnvironment,
				TrustServicesCriterion.CC2_Communication,
				TrustServicesCriterion.CC3_RiskAssessment,
				TrustServicesCriterion.CC4_Monitoring,
				TrustServicesCriterion.CC5_ControlActivities,
				TrustServicesCriterion.CC6_LogicalAccess,
				TrustServicesCriterion.CC7_SystemOperations,
				TrustServicesCriterion.CC8_ChangeManagement,
				TrustServicesCriterion.CC9_RiskMitigation
			},
			TrustServicesCategory.Availability =>
			[
				TrustServicesCriterion.A1_InfrastructureManagement,
				TrustServicesCriterion.A2_CapacityManagement,
				TrustServicesCriterion.A3_BackupRecovery
			],
			TrustServicesCategory.ProcessingIntegrity =>
			[
				TrustServicesCriterion.PI1_InputValidation,
				TrustServicesCriterion.PI2_ProcessingAccuracy,
				TrustServicesCriterion.PI3_OutputCompleteness
			],
			TrustServicesCategory.Confidentiality =>
			[
				TrustServicesCriterion.C1_DataClassification,
				TrustServicesCriterion.C2_DataProtection,
				TrustServicesCriterion.C3_DataDisposal
			],
			TrustServicesCategory.Privacy =>
			[
				TrustServicesCriterion.P1_Notice,
				TrustServicesCriterion.P2_ChoiceConsent,
				TrustServicesCriterion.P3_Collection,
				TrustServicesCriterion.P4_UseRetention,
				TrustServicesCriterion.P5_Access,
				TrustServicesCriterion.P6_Disclosure,
				TrustServicesCriterion.P7_Quality,
				TrustServicesCriterion.P8_MonitoringEnforcement
			],
			_ => throw new ArgumentOutOfRangeException(nameof(category))
		};
	}
}
