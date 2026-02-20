using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Erasure;

public class LegalHoldServiceShould
{
	private readonly ILegalHoldStore _store;
	private readonly ILegalHoldQueryStore _queryStore;
	private readonly LegalHoldService _sut;

	public LegalHoldServiceShould()
	{
		_store = A.Fake<ILegalHoldStore>();
		_queryStore = A.Fake<ILegalHoldQueryStore>();

		A.CallTo(() => _store.GetService(typeof(ILegalHoldQueryStore)))
			.Returns(_queryStore);

		_sut = new LegalHoldService(
			_store,
			NullLogger<LegalHoldService>.Instance);
	}

	[Fact]
	public async Task Create_hold_with_valid_request()
	{
		var request = new LegalHoldRequest
		{
			DataSubjectId = "user-123",
			IdType = DataSubjectIdType.UserId,
			Basis = LegalHoldBasis.LegalObligation,
			CaseReference = "CASE-001",
			Description = "Litigation hold for pending lawsuit",
			CreatedBy = "legal-team"
		};

		var hold = await _sut.CreateHoldAsync(request, CancellationToken.None);

		hold.ShouldNotBeNull();
		hold.HoldId.ShouldNotBe(Guid.Empty);
		hold.IsActive.ShouldBeTrue();
		hold.Basis.ShouldBe(LegalHoldBasis.LegalObligation);
		hold.CaseReference.ShouldBe("CASE-001");
		hold.CreatedBy.ShouldBe("legal-team");
		hold.DataSubjectIdHash.ShouldNotBeNullOrWhiteSpace();

		A.CallTo(() => _store.SaveHoldAsync(
			A<LegalHold>.That.Matches(h => h.IsActive && h.CaseReference == "CASE-001"),
			CancellationToken.None))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Create_tenant_wide_hold_without_data_subject_id()
	{
		var request = new LegalHoldRequest
		{
			TenantId = "tenant-1",
			Basis = LegalHoldBasis.RegulatoryInvestigation,
			CaseReference = "INVEST-001",
			Description = "Regulatory investigation hold",
			CreatedBy = "compliance"
		};

		var hold = await _sut.CreateHoldAsync(request, CancellationToken.None);

		hold.DataSubjectIdHash.ShouldBeNull();
		hold.TenantId.ShouldBe("tenant-1");
		hold.IsActive.ShouldBeTrue();
	}

	[Fact]
	public async Task Throw_when_create_request_is_null()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.CreateHoldAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task Throw_when_request_missing_both_subject_and_tenant()
	{
		var request = new LegalHoldRequest
		{
			Basis = LegalHoldBasis.LitigationHold,
			CaseReference = "CASE-002",
			Description = "Hold description",
			CreatedBy = "admin"
		};

		await Should.ThrowAsync<ArgumentException>(
			() => _sut.CreateHoldAsync(request, CancellationToken.None));
	}

	[Fact]
	public async Task Throw_when_case_reference_missing()
	{
		var request = new LegalHoldRequest
		{
			DataSubjectId = "user-1",
			Basis = LegalHoldBasis.LegalObligation,
			CaseReference = "",
			Description = "Hold description",
			CreatedBy = "admin"
		};

		await Should.ThrowAsync<ArgumentException>(
			() => _sut.CreateHoldAsync(request, CancellationToken.None));
	}

	[Fact]
	public async Task Throw_when_description_missing()
	{
		var request = new LegalHoldRequest
		{
			DataSubjectId = "user-1",
			Basis = LegalHoldBasis.LegalObligation,
			CaseReference = "CASE-003",
			Description = "",
			CreatedBy = "admin"
		};

		await Should.ThrowAsync<ArgumentException>(
			() => _sut.CreateHoldAsync(request, CancellationToken.None));
	}

	[Fact]
	public async Task Throw_when_created_by_missing()
	{
		var request = new LegalHoldRequest
		{
			DataSubjectId = "user-1",
			Basis = LegalHoldBasis.LegalObligation,
			CaseReference = "CASE-004",
			Description = "Hold description",
			CreatedBy = ""
		};

		await Should.ThrowAsync<ArgumentException>(
			() => _sut.CreateHoldAsync(request, CancellationToken.None));
	}

	[Fact]
	public async Task Throw_when_expires_at_is_in_past()
	{
		var request = new LegalHoldRequest
		{
			DataSubjectId = "user-1",
			Basis = LegalHoldBasis.LegalObligation,
			CaseReference = "CASE-005",
			Description = "Hold description",
			CreatedBy = "admin",
			ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1)
		};

		await Should.ThrowAsync<ArgumentException>(
			() => _sut.CreateHoldAsync(request, CancellationToken.None));
	}

	[Fact]
	public async Task Release_active_hold()
	{
		var holdId = Guid.NewGuid();
		var existingHold = new LegalHold
		{
			HoldId = holdId,
			DataSubjectIdHash = "hash",
			Basis = LegalHoldBasis.LitigationHold,
			CaseReference = "CASE-006",
			Description = "Active hold",
			IsActive = true,
			CreatedBy = "legal",
			CreatedAt = DateTimeOffset.UtcNow.AddDays(-10)
		};

		A.CallTo(() => _store.GetHoldAsync(holdId, CancellationToken.None))
			.Returns(existingHold);
		A.CallTo(() => _store.UpdateHoldAsync(A<LegalHold>._, CancellationToken.None))
			.Returns(true);

		await _sut.ReleaseHoldAsync(holdId, "Case resolved", "legal-admin", CancellationToken.None);

		A.CallTo(() => _store.UpdateHoldAsync(
			A<LegalHold>.That.Matches(h =>
				!h.IsActive &&
				h.ReleasedBy == "legal-admin" &&
				h.ReleaseReason == "Case resolved"),
			CancellationToken.None))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Throw_when_releasing_nonexistent_hold()
	{
		var holdId = Guid.NewGuid();

		A.CallTo(() => _store.GetHoldAsync(holdId, CancellationToken.None))
			.Returns((LegalHold?)null);

		await Should.ThrowAsync<KeyNotFoundException>(
			() => _sut.ReleaseHoldAsync(holdId, "reason", "admin", CancellationToken.None));
	}

	[Fact]
	public async Task Throw_when_releasing_already_released_hold()
	{
		var holdId = Guid.NewGuid();
		var releasedHold = new LegalHold
		{
			HoldId = holdId,
			Basis = LegalHoldBasis.LegalObligation,
			CaseReference = "CASE-007",
			Description = "Released hold",
			IsActive = false,
			CreatedBy = "legal",
			CreatedAt = DateTimeOffset.UtcNow.AddDays(-10),
			ReleasedBy = "admin",
			ReleasedAt = DateTimeOffset.UtcNow.AddDays(-1)
		};

		A.CallTo(() => _store.GetHoldAsync(holdId, CancellationToken.None))
			.Returns(releasedHold);

		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.ReleaseHoldAsync(holdId, "reason", "admin", CancellationToken.None));
	}

	[Fact]
	public async Task Throw_when_release_reason_is_empty()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.ReleaseHoldAsync(Guid.NewGuid(), "", "admin", CancellationToken.None));
	}

	[Fact]
	public async Task Throw_when_released_by_is_empty()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.ReleaseHoldAsync(Guid.NewGuid(), "reason", "", CancellationToken.None));
	}

	[Fact]
	public async Task Check_holds_returns_no_holds_when_none_active()
	{
		A.CallTo(() => _queryStore.GetActiveHoldsForDataSubjectAsync(
			A<string>._, A<string?>._, CancellationToken.None))
			.Returns(new List<LegalHold>());

		var result = await _sut.CheckHoldsAsync("user-1", DataSubjectIdType.UserId, null, CancellationToken.None);

		result.HasActiveHolds.ShouldBeFalse();
		result.ErasureBlocked.ShouldBeFalse();
	}

	[Fact]
	public async Task Check_holds_returns_active_holds_when_present()
	{
		var hold = new LegalHold
		{
			HoldId = Guid.NewGuid(),
			DataSubjectIdHash = "hash",
			Basis = LegalHoldBasis.LitigationHold,
			CaseReference = "CASE-008",
			Description = "Active hold",
			IsActive = true,
			CreatedBy = "legal",
			CreatedAt = DateTimeOffset.UtcNow
		};

		A.CallTo(() => _queryStore.GetActiveHoldsForDataSubjectAsync(
			A<string>._, A<string?>._, CancellationToken.None))
			.Returns(new List<LegalHold> { hold });

		var result = await _sut.CheckHoldsAsync("user-1", DataSubjectIdType.UserId, null, CancellationToken.None);

		result.HasActiveHolds.ShouldBeTrue();
		result.ActiveHolds.Count.ShouldBe(1);
		result.ActiveHolds[0].CaseReference.ShouldBe("CASE-008");
	}

	[Fact]
	public async Task Check_holds_includes_tenant_wide_holds()
	{
		var subjectHold = new LegalHold
		{
			HoldId = Guid.NewGuid(),
			DataSubjectIdHash = "hash",
			Basis = LegalHoldBasis.LitigationHold,
			CaseReference = "SUBJECT-CASE",
			Description = "Subject hold",
			IsActive = true,
			CreatedBy = "legal",
			CreatedAt = DateTimeOffset.UtcNow
		};

		var tenantHold = new LegalHold
		{
			HoldId = Guid.NewGuid(),
			DataSubjectIdHash = null, // Tenant-wide
			TenantId = "tenant-1",
			Basis = LegalHoldBasis.RegulatoryInvestigation,
			CaseReference = "TENANT-CASE",
			Description = "Tenant-wide hold",
			IsActive = true,
			CreatedBy = "compliance",
			CreatedAt = DateTimeOffset.UtcNow
		};

		A.CallTo(() => _queryStore.GetActiveHoldsForDataSubjectAsync(
			A<string>._, A<string?>._, CancellationToken.None))
			.Returns(new List<LegalHold> { subjectHold });

		A.CallTo(() => _queryStore.GetActiveHoldsForTenantAsync("tenant-1", CancellationToken.None))
			.Returns(new List<LegalHold> { tenantHold });

		var result = await _sut.CheckHoldsAsync("user-1", DataSubjectIdType.UserId, "tenant-1", CancellationToken.None);

		result.HasActiveHolds.ShouldBeTrue();
		result.ActiveHolds.Count.ShouldBe(2);
	}

	[Fact]
	public async Task Throw_when_check_holds_data_subject_id_empty()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.CheckHoldsAsync("", DataSubjectIdType.UserId, null, CancellationToken.None));
	}

	[Fact]
	public async Task Get_hold_delegates_to_store()
	{
		var holdId = Guid.NewGuid();
		var hold = new LegalHold
		{
			HoldId = holdId,
			Basis = LegalHoldBasis.LegalObligation,
			CaseReference = "CASE-009",
			Description = "Hold",
			IsActive = true,
			CreatedBy = "admin",
			CreatedAt = DateTimeOffset.UtcNow
		};

		A.CallTo(() => _store.GetHoldAsync(holdId, CancellationToken.None))
			.Returns(hold);

		var result = await _sut.GetHoldAsync(holdId, CancellationToken.None);

		result.ShouldNotBeNull();
		result.HoldId.ShouldBe(holdId);
	}

	[Fact]
	public async Task List_active_holds_delegates_to_query_store()
	{
		var holds = new List<LegalHold>
		{
			new()
			{
				HoldId = Guid.NewGuid(),
				Basis = LegalHoldBasis.LegalObligation,
				CaseReference = "CASE-010",
				Description = "Hold 1",
				IsActive = true,
				CreatedBy = "admin",
				CreatedAt = DateTimeOffset.UtcNow
			}
		};

		A.CallTo(() => _queryStore.ListActiveHoldsAsync("tenant-1", CancellationToken.None))
			.Returns(holds);

		var result = await _sut.ListActiveHoldsAsync("tenant-1", CancellationToken.None);

		result.Count.ShouldBe(1);
	}

	[Fact]
	public void Throw_when_store_is_null()
	{
		Should.Throw<ArgumentNullException>(
			() => new LegalHoldService(null!, NullLogger<LegalHoldService>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		var store = A.Fake<ILegalHoldStore>();
		A.CallTo(() => store.GetService(typeof(ILegalHoldQueryStore)))
			.Returns(A.Fake<ILegalHoldQueryStore>());

		Should.Throw<ArgumentNullException>(
			() => new LegalHoldService(store, null!));
	}

	[Fact]
	public void Throw_when_store_does_not_support_query()
	{
		var store = A.Fake<ILegalHoldStore>();
		A.CallTo(() => store.GetService(typeof(ILegalHoldQueryStore)))
			.Returns(null);

		Should.Throw<InvalidOperationException>(
			() => new LegalHoldService(store, NullLogger<LegalHoldService>.Instance));
	}
}
