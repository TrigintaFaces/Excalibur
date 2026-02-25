namespace Excalibur.Dispatch.Compliance.Tests.Erasure;

public class InMemoryLegalHoldStoreShould
{
    private readonly InMemoryLegalHoldStore _sut = new();

    private static LegalHold CreateHold(Guid? holdId = null, string? dataSubjectIdHash = null, bool isActive = true) => new()
    {
        HoldId = holdId ?? Guid.NewGuid(),
        DataSubjectIdHash = dataSubjectIdHash ?? "hash-123",
        IdType = DataSubjectIdType.UserId,
        Basis = LegalHoldBasis.LitigationHold,
        CaseReference = "CASE-001",
        Description = "Test hold",
        IsActive = isActive,
        CreatedBy = "admin",
        CreatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task Save_and_retrieve_hold()
    {
        var hold = CreateHold();
        await _sut.SaveHoldAsync(hold, CancellationToken.None);

        var result = await _sut.GetHoldAsync(hold.HoldId, CancellationToken.None);

        result.ShouldNotBeNull();
        result.HoldId.ShouldBe(hold.HoldId);
    }

    [Fact]
    public async Task Return_null_for_nonexistent_hold()
    {
        var result = await _sut.GetHoldAsync(Guid.NewGuid(), CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Throw_on_duplicate_hold_id()
    {
        var holdId = Guid.NewGuid();
        await _sut.SaveHoldAsync(CreateHold(holdId), CancellationToken.None);

        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.SaveHoldAsync(CreateHold(holdId), CancellationToken.None));
    }

    [Fact]
    public async Task Update_hold_successfully()
    {
        var hold = CreateHold();
        await _sut.SaveHoldAsync(hold, CancellationToken.None);

        var released = hold with { IsActive = false, ReleasedBy = "admin" };
        var updated = await _sut.UpdateHoldAsync(released, CancellationToken.None);

        updated.ShouldBeTrue();
    }

    [Fact]
    public async Task Return_false_when_updating_nonexistent_hold()
    {
        var hold = CreateHold();
        var updated = await _sut.UpdateHoldAsync(hold, CancellationToken.None);

        updated.ShouldBeFalse();
    }

    [Fact]
    public async Task Get_active_holds_for_data_subject()
    {
        var hash = "subject-hash-1";
        await _sut.SaveHoldAsync(CreateHold(dataSubjectIdHash: hash), CancellationToken.None);
        await _sut.SaveHoldAsync(CreateHold(dataSubjectIdHash: "other-hash"), CancellationToken.None);

        var holds = await _sut.GetActiveHoldsForDataSubjectAsync(hash, null, CancellationToken.None);

        holds.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Get_active_holds_for_tenant()
    {
        var hold = CreateHold() with { TenantId = "tenant-1" };
        await _sut.SaveHoldAsync(hold, CancellationToken.None);

        var holds = await _sut.GetActiveHoldsForTenantAsync("tenant-1", CancellationToken.None);

        holds.Count.ShouldBe(1);
    }

    [Fact]
    public async Task List_active_holds()
    {
        await _sut.SaveHoldAsync(CreateHold(), CancellationToken.None);
        await _sut.SaveHoldAsync(CreateHold(isActive: false), CancellationToken.None);

        var holds = await _sut.ListActiveHoldsAsync(null, CancellationToken.None);

        holds.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Get_expired_holds()
    {
        var expired = CreateHold() with { ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1) };
        await _sut.SaveHoldAsync(expired, CancellationToken.None);

        var notExpired = CreateHold() with { ExpiresAt = DateTimeOffset.UtcNow.AddDays(30) };
        await _sut.SaveHoldAsync(notExpired, CancellationToken.None);

        var results = await _sut.GetExpiredHoldsAsync(CancellationToken.None);

        results.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Track_hold_count()
    {
        _sut.HoldCount.ShouldBe(0);

        await _sut.SaveHoldAsync(CreateHold(), CancellationToken.None);

        _sut.HoldCount.ShouldBe(1);
    }

    [Fact]
    public async Task Track_active_hold_count()
    {
        await _sut.SaveHoldAsync(CreateHold(isActive: true), CancellationToken.None);

        _sut.ActiveHoldCount.ShouldBe(1);
    }

    [Fact]
    public void Return_query_store_via_get_service()
    {
        var service = _sut.GetService(typeof(ILegalHoldQueryStore));

        service.ShouldNotBeNull();
        service.ShouldBe(_sut);
    }

    [Fact]
    public void Return_null_for_unsupported_service()
    {
        var service = _sut.GetService(typeof(string));

        service.ShouldBeNull();
    }

    [Fact]
    public async Task Clear_all_holds()
    {
        await _sut.SaveHoldAsync(CreateHold(), CancellationToken.None);
        _sut.HoldCount.ShouldBe(1);

        _sut.Clear();

        _sut.HoldCount.ShouldBe(0);
    }

    [Fact]
    public void Throw_when_saving_null_hold()
    {
        Should.Throw<ArgumentNullException>(
            () => _sut.SaveHoldAsync(null!, CancellationToken.None));
    }

    [Fact]
    public void Throw_when_updating_null_hold()
    {
        Should.Throw<ArgumentNullException>(
            () => _sut.UpdateHoldAsync(null!, CancellationToken.None));
    }

    [Fact]
    public void Throw_when_get_service_null_type()
    {
        Should.Throw<ArgumentNullException>(() => _sut.GetService(null!));
    }
}
