using Excalibur.Dispatch.Compliance.Diagnostics;

namespace Excalibur.Dispatch.Compliance.Tests.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ComplianceEventIdShould
{
	[Fact]
	public void Have_data_retention_ids_in_92000_range()
	{
		ComplianceEventId.RetentionPolicyServiceCreated.ShouldBe(92000);
		ComplianceEventId.RetentionPolicyEvaluated.ShouldBe(92001);
		ComplianceEventId.RetentionPeriodExpired.ShouldBe(92002);
		ComplianceEventId.DataPurgeScheduled.ShouldBe(92003);
		ComplianceEventId.DataPurgeCompleted.ShouldBe(92004);
		ComplianceEventId.RetentionExceptionApplied.ShouldBe(92005);
	}

	[Fact]
	public void Have_data_sovereignty_ids_in_92100_range()
	{
		ComplianceEventId.DataSovereigntyValidatorCreated.ShouldBe(92100);
		ComplianceEventId.DataResidencyValidated.ShouldBe(92101);
		ComplianceEventId.DataResidencyViolation.ShouldBe(92102);
		ComplianceEventId.CrossBorderTransferBlocked.ShouldBe(92103);
		ComplianceEventId.RegionRoutingApplied.ShouldBe(92104);
	}

	[Fact]
	public void Have_field_encryption_ids_in_92200_range()
	{
		ComplianceEventId.FieldEncryptionServiceCreated.ShouldBe(92200);
		ComplianceEventId.FieldEncrypted.ShouldBe(92201);
		ComplianceEventId.FieldDecrypted.ShouldBe(92202);
		ComplianceEventId.FieldEncryptionFailed.ShouldBe(92203);
		ComplianceEventId.FieldEncryptionKeyRotated.ShouldBe(92204);
	}

	[Fact]
	public void Have_compliance_validation_ids_in_92300_range()
	{
		ComplianceEventId.ComplianceValidationExecuting.ShouldBe(92300);
		ComplianceEventId.ComplianceCheckPassed.ShouldBe(92301);
		ComplianceEventId.ComplianceCheckFailed.ShouldBe(92302);
		ComplianceEventId.PiiDetected.ShouldBe(92303);
		ComplianceEventId.PiiMasked.ShouldBe(92304);
		ComplianceEventId.ComplianceRuleEvaluated.ShouldBe(92305);
	}

	[Fact]
	public void Have_regulatory_reporting_ids_in_92400_range()
	{
		ComplianceEventId.RegulatoryReportGeneratorCreated.ShouldBe(92400);
		ComplianceEventId.RegulatoryReportGenerated.ShouldBe(92401);
		ComplianceEventId.AuditTrailExported.ShouldBe(92402);
		ComplianceEventId.ComplianceCertificateGenerated.ShouldBe(92403);
		ComplianceEventId.RegulatorySubmissionCompleted.ShouldBe(92404);
	}

	[Fact]
	public void Have_key_management_ids_in_92500_range()
	{
		ComplianceEventId.KeyManagementServiceCreated.ShouldBe(92500);
		ComplianceEventId.EncryptionKeyCreated.ShouldBe(92501);
		ComplianceEventId.EncryptionKeyRotated.ShouldBe(92502);
		ComplianceEventId.EncryptionKeyRevoked.ShouldBe(92503);
		ComplianceEventId.KeyAccessLogged.ShouldBe(92504);
		ComplianceEventId.DevEncryptionWarning.ShouldBe(92510);
	}

	[Fact]
	public void Have_key_rotation_service_ids_in_92540_range()
	{
		ComplianceEventId.KeyRotationServiceDisabled.ShouldBe(92540);
		ComplianceEventId.KeyRotationServiceStarted.ShouldBe(92541);
		ComplianceEventId.KeyRotationCheckCompleted.ShouldBe(92542);
		ComplianceEventId.KeyRotationCheckNoKeys.ShouldBe(92543);
		ComplianceEventId.KeyRotationCheckError.ShouldBe(92544);
		ComplianceEventId.KeyRotationServiceStopped.ShouldBe(92545);
	}

	[Fact]
	public void Have_cloud_provider_ids_in_92600_range()
	{
		ComplianceEventId.CloudComplianceAdapterCreated.ShouldBe(92600);
		ComplianceEventId.AwsComplianceCheckCompleted.ShouldBe(92601);
		ComplianceEventId.AzureComplianceCheckCompleted.ShouldBe(92602);
		ComplianceEventId.VaultIntegrationConfigured.ShouldBe(92603);
		ComplianceEventId.CloudKmsConfigured.ShouldBe(92604);
	}

	[Fact]
	public void Have_erasure_ids_in_92700_range()
	{
		ComplianceEventId.ErasureRequestProcessing.ShouldBe(92700);
		ComplianceEventId.ErasureBlockedByLegalHold.ShouldBe(92701);
		ComplianceEventId.ErasureScheduled.ShouldBe(92702);
		ComplianceEventId.ErasureRequestFailed.ShouldBe(92703);
		ComplianceEventId.ErasureRequestCompleted.ShouldBe(92709);
	}

	[Fact]
	public void Have_erasure_scheduler_ids_in_92750_range()
	{
		ComplianceEventId.ErasureSchedulerDisabled.ShouldBe(92750);
		ComplianceEventId.ErasureSchedulerStarting.ShouldBe(92751);
		ComplianceEventId.ErasureSchedulerProcessingError.ShouldBe(92752);
		ComplianceEventId.ErasureSchedulerStopped.ShouldBe(92753);
		ComplianceEventId.ErasureSchedulerNoScheduledRequests.ShouldBe(92754);
		ComplianceEventId.ErasureSchedulerProcessingBatch.ShouldBe(92755);
		ComplianceEventId.ErasureSchedulerRequestCompleted.ShouldBe(92757);
	}

	[Fact]
	public void Have_legal_hold_ids_in_92780_range()
	{
		ComplianceEventId.LegalHoldExpirationDisabled.ShouldBe(92780);
		ComplianceEventId.LegalHoldExpirationStarting.ShouldBe(92781);
		ComplianceEventId.LegalHoldExpirationStopped.ShouldBe(92782);
		ComplianceEventId.LegalHoldExpirationProcessingError.ShouldBe(92783);
		ComplianceEventId.LegalHoldCreated.ShouldBe(92790);
		ComplianceEventId.LegalHoldReleased.ShouldBe(92791);
	}

	[Fact]
	public void Have_compliance_monitoring_ids_in_92800_range()
	{
		ComplianceEventId.ComplianceGapAlertCritical.ShouldBe(92800);
		ComplianceEventId.ComplianceGapAlertHigh.ShouldBe(92801);
		ComplianceEventId.ComplianceGapAlertMedium.ShouldBe(92802);
		ComplianceEventId.ComplianceGapAlertLow.ShouldBe(92803);
		ComplianceEventId.ComplianceRestored.ShouldBe(92820);
		ComplianceEventId.ComplianceLost.ShouldBe(92821);
	}

	[Fact]
	public void Have_cascade_erasure_ids_in_92900_range()
	{
		ComplianceEventId.CascadeErasureStarted.ShouldBe(92900);
		ComplianceEventId.CascadeErasureCompleted.ShouldBe(92901);
		ComplianceEventId.CascadeErasureFailed.ShouldBe(92902);
	}

	[Fact]
	public void Have_data_portability_ids_in_92910_range()
	{
		ComplianceEventId.DataPortabilityExportStarted.ShouldBe(92910);
		ComplianceEventId.DataPortabilityExportCompleted.ShouldBe(92911);
		ComplianceEventId.DataPortabilityExportFailed.ShouldBe(92912);
	}

	[Fact]
	public void Have_retention_enforcement_ids_in_92960_range()
	{
		ComplianceEventId.RetentionEnforcementStarted.ShouldBe(92960);
		ComplianceEventId.RetentionEnforcementCompleted.ShouldBe(92961);
		ComplianceEventId.RetentionEnforcementFailed.ShouldBe(92962);
		ComplianceEventId.RetentionEnforcementDisabled.ShouldBe(92963);
		ComplianceEventId.RetentionEnforcementServiceStarting.ShouldBe(92964);
		ComplianceEventId.RetentionEnforcementServiceStopped.ShouldBe(92965);
	}

	[Fact]
	public void Have_all_ids_in_92000_to_92999_range()
	{
		var fields = typeof(ComplianceEventId)
			.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
			.Where(f => f.FieldType == typeof(int));

		foreach (var field in fields)
		{
			var value = (int)field.GetValue(null)!;
			value.ShouldBeGreaterThanOrEqualTo(92000, $"Field {field.Name} has value {value} below 92000");
			value.ShouldBeLessThanOrEqualTo(92999, $"Field {field.Name} has value {value} above 92999");
		}
	}

	[Fact]
	public void Have_no_duplicate_event_ids()
	{
		var fields = typeof(ComplianceEventId)
			.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
			.Where(f => f.FieldType == typeof(int))
			.Select(f => new { f.Name, Value = (int)f.GetValue(null)! })
			.ToList();

		var duplicates = fields
			.GroupBy(f => f.Value)
			.Where(g => g.Count() > 1)
			.Select(g => $"Value {g.Key}: {string.Join(", ", g.Select(f => f.Name))}")
			.ToList();

		duplicates.ShouldBeEmpty($"Duplicate event IDs found:\n{string.Join("\n", duplicates)}");
	}
}
