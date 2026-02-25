using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Erasure;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ErasureAbstractionTypesShould
{
	[Fact]
	public void Create_erasure_request_with_defaults()
	{
		var request = new ErasureRequest
		{
			DataSubjectId = "user-1",
			IdType = DataSubjectIdType.UserId,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			RequestedBy = "admin"
		};

		request.RequestId.ShouldNotBe(Guid.Empty);
		request.Scope.ShouldBe(ErasureScope.User);
		request.TenantId.ShouldBeNull();
		request.ExternalReference.ShouldBeNull();
		request.GracePeriodOverride.ShouldBeNull();
		request.DataCategories.ShouldBeNull();
		request.RequestedAt.ShouldNotBe(default);
		request.Metadata.ShouldBeNull();
	}

	[Fact]
	public void Create_erasure_request_with_all_properties()
	{
		var request = new ErasureRequest
		{
			DataSubjectId = "user-1",
			IdType = DataSubjectIdType.Email,
			TenantId = "tenant-1",
			Scope = ErasureScope.Selective,
			LegalBasis = ErasureLegalBasis.RightToObject,
			ExternalReference = "TICKET-123",
			RequestedBy = "admin@company.com",
			GracePeriodOverride = TimeSpan.FromDays(7),
			DataCategories = ["email", "name", "address"],
			Metadata = new Dictionary<string, string> { ["key"] = "value" }
		};

		request.IdType.ShouldBe(DataSubjectIdType.Email);
		request.TenantId.ShouldBe("tenant-1");
		request.Scope.ShouldBe(ErasureScope.Selective);
		request.ExternalReference.ShouldBe("TICKET-123");
		request.GracePeriodOverride.ShouldBe(TimeSpan.FromDays(7));
		request.DataCategories.ShouldNotBeNull();
		request.DataCategories.Count.ShouldBe(3);
		request.Metadata.ShouldNotBeNull();
	}

	[Fact]
	public void Create_erasure_result_scheduled()
	{
		var requestId = Guid.NewGuid();
		var scheduledTime = DateTimeOffset.UtcNow.AddHours(1);

		var result = ErasureResult.Scheduled(requestId, scheduledTime);

		result.RequestId.ShouldBe(requestId);
		result.Status.ShouldBe(ErasureRequestStatus.Scheduled);
		result.ScheduledExecutionTime.ShouldBe(scheduledTime);
		result.EstimatedCompletionTime.ShouldNotBeNull();
		result.BlockingHold.ShouldBeNull();
		result.Message.ShouldBeNull();
	}

	[Fact]
	public void Create_erasure_result_scheduled_with_inventory()
	{
		var requestId = Guid.NewGuid();
		var scheduledTime = DateTimeOffset.UtcNow.AddHours(1);
		var inventory = new DataInventorySummary
		{
			EncryptedFieldCount = 10,
			KeyCount = 3,
			DataCategories = ["email", "name"],
			AffectedTables = ["users", "profiles"],
			EstimatedDataSizeBytes = 1024 * 1024
		};

		var result = ErasureResult.Scheduled(requestId, scheduledTime, inventory);

		result.InventorySummary.ShouldNotBeNull();
		result.InventorySummary.EncryptedFieldCount.ShouldBe(10);
		result.InventorySummary.KeyCount.ShouldBe(3);
		result.InventorySummary.DataCategories.Count.ShouldBe(2);
		result.InventorySummary.AffectedTables.Count.ShouldBe(2);
	}

	[Fact]
	public void Create_erasure_result_blocked()
	{
		var requestId = Guid.NewGuid();
		var hold = new LegalHoldInfo
		{
			HoldId = Guid.NewGuid(),
			Basis = LegalHoldBasis.LitigationHold,
			CaseReference = "CASE-456",
			CreatedAt = DateTimeOffset.UtcNow,
			ExpiresAt = DateTimeOffset.UtcNow.AddYears(1)
		};

		var result = ErasureResult.Blocked(requestId, hold);

		result.Status.ShouldBe(ErasureRequestStatus.BlockedByLegalHold);
		result.BlockingHold.ShouldNotBeNull();
		result.BlockingHold.CaseReference.ShouldBe("CASE-456");
		result.Message.ShouldContain("CASE-456");
	}

	[Fact]
	public void Create_erasure_result_failed()
	{
		var requestId = Guid.NewGuid();

		var result = ErasureResult.Failed(requestId, "Database connection failed");

		result.Status.ShouldBe(ErasureRequestStatus.Failed);
		result.Message.ShouldBe("Database connection failed");
	}

	[Fact]
	public void Create_erasure_certificate_with_all_properties()
	{
		var cert = new ErasureCertificate
		{
			CertificateId = Guid.NewGuid(),
			RequestId = Guid.NewGuid(),
			DataSubjectReference = "hash-abc",
			RequestReceivedAt = DateTimeOffset.UtcNow.AddDays(-1),
			CompletedAt = DateTimeOffset.UtcNow,
			Method = ErasureMethod.CryptographicErasure,
			Summary = new ErasureSummary
			{
				KeysDeleted = 5,
				RecordsAffected = 100,
				DataCategories = ["email", "name"],
				TablesAffected = ["users"],
				DataSizeBytes = 2048
			},
			Verification = new VerificationSummary
			{
				Verified = true,
				Methods = VerificationMethod.AuditLog | VerificationMethod.KeyManagementSystem,
				VerifiedAt = DateTimeOffset.UtcNow,
				ReportHash = "sha256-hash",
				DeletedKeyIds = ["key-1", "key-2"]
			},
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			Exceptions =
			[
				new ErasureException
				{
					Basis = LegalHoldBasis.LegalObligation,
					DataCategory = "tax-records",
					Reason = "7-year retention required",
					RetentionPeriod = TimeSpan.FromDays(365 * 7),
					HoldId = Guid.NewGuid()
				}
			],
			Signature = "sig-abc",
			RetainUntil = DateTimeOffset.UtcNow.AddYears(7)
		};

		cert.CertificateId.ShouldNotBe(Guid.Empty);
		cert.Method.ShouldBe(ErasureMethod.CryptographicErasure);
		cert.Summary.KeysDeleted.ShouldBe(5);
		cert.Verification.Verified.ShouldBeTrue();
		cert.Verification.Methods.HasFlag(VerificationMethod.AuditLog).ShouldBeTrue();
		cert.Verification.Methods.HasFlag(VerificationMethod.KeyManagementSystem).ShouldBeTrue();
		cert.Exceptions.ShouldHaveSingleItem();
		cert.Version.ShouldBe("1.0");
		cert.GeneratedAt.ShouldNotBe(default);
	}

	[Fact]
	public void Create_erasure_certificate_with_defaults()
	{
		var cert = new ErasureCertificate
		{
			CertificateId = Guid.NewGuid(),
			RequestId = Guid.NewGuid(),
			DataSubjectReference = "hash",
			RequestReceivedAt = DateTimeOffset.UtcNow,
			CompletedAt = DateTimeOffset.UtcNow,
			Method = ErasureMethod.PhysicalDeletion,
			Summary = new ErasureSummary(),
			Verification = new VerificationSummary
			{
				Verified = true,
				Methods = VerificationMethod.None,
				VerifiedAt = DateTimeOffset.UtcNow
			},
			LegalBasis = ErasureLegalBasis.DataSubjectRequest,
			Signature = "sig",
			RetainUntil = DateTimeOffset.UtcNow.AddYears(7)
		};

		cert.Exceptions.ShouldBeEmpty();
		cert.Version.ShouldBe("1.0");
		cert.Summary.KeysDeleted.ShouldBe(0);
		cert.Summary.DataCategories.ShouldBeEmpty();
		cert.Summary.TablesAffected.ShouldBeEmpty();
		cert.Verification.ReportHash.ShouldBeNull();
		cert.Verification.DeletedKeyIds.ShouldBeEmpty();
		cert.Verification.Warnings.ShouldBeEmpty();
	}

	[Fact]
	public void Legal_hold_info_has_optional_expiry()
	{
		var hold = new LegalHoldInfo
		{
			HoldId = Guid.NewGuid(),
			Basis = LegalHoldBasis.RegulatoryInvestigation,
			CaseReference = "INV-001",
			CreatedAt = DateTimeOffset.UtcNow
		};

		hold.ExpiresAt.ShouldBeNull();
	}

	[Fact]
	public void Data_inventory_summary_with_defaults()
	{
		var summary = new DataInventorySummary();

		summary.EncryptedFieldCount.ShouldBe(0);
		summary.KeyCount.ShouldBe(0);
		summary.DataCategories.ShouldBeEmpty();
		summary.AffectedTables.ShouldBeEmpty();
		summary.EstimatedDataSizeBytes.ShouldBe(0);
	}

	[Fact]
	public void Enumerate_all_erasure_methods()
	{
		var methods = Enum.GetValues<ErasureMethod>();
		methods.Length.ShouldBe(4);
	}

	[Fact]
	public void Enumerate_all_erasure_request_statuses()
	{
		var statuses = Enum.GetValues<ErasureRequestStatus>();
		statuses.Length.ShouldBe(8);
	}

	[Fact]
	public void Enumerate_all_data_subject_id_types()
	{
		var types = Enum.GetValues<DataSubjectIdType>();
		types.Length.ShouldBe(6);
	}

	[Fact]
	public void Enumerate_all_erasure_scopes()
	{
		var scopes = Enum.GetValues<ErasureScope>();
		scopes.Length.ShouldBe(3);
	}

	[Fact]
	public void Enumerate_all_erasure_legal_bases()
	{
		var bases = Enum.GetValues<ErasureLegalBasis>();
		bases.Length.ShouldBe(7);
	}

	[Fact]
	public void Enumerate_all_legal_hold_bases()
	{
		var bases = Enum.GetValues<LegalHoldBasis>();
		bases.Length.ShouldBe(7);
	}

	[Fact]
	public void Verification_method_flags_combine_correctly()
	{
		var combined = VerificationMethod.AuditLog | VerificationMethod.HsmAttestation | VerificationMethod.DecryptionFailure;

		combined.HasFlag(VerificationMethod.AuditLog).ShouldBeTrue();
		combined.HasFlag(VerificationMethod.HsmAttestation).ShouldBeTrue();
		combined.HasFlag(VerificationMethod.DecryptionFailure).ShouldBeTrue();
		combined.HasFlag(VerificationMethod.KeyManagementSystem).ShouldBeFalse();
	}
}
