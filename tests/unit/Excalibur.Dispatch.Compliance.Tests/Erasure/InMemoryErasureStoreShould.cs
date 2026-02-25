namespace Excalibur.Dispatch.Compliance.Tests.Erasure;

public class InMemoryErasureStoreShould
{
    private readonly InMemoryErasureStore _sut = new();

    private static ErasureRequest CreateRequest(Guid? requestId = null) => new()
    {
        RequestId = requestId ?? Guid.NewGuid(),
        DataSubjectId = "user-1",
        IdType = DataSubjectIdType.UserId,
        LegalBasis = ErasureLegalBasis.DataSubjectRequest,
        RequestedBy = "admin"
    };

    [Fact]
    public async Task Save_and_retrieve_request_status()
    {
        var request = CreateRequest();
        await _sut.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddHours(72), CancellationToken.None);

        var status = await _sut.GetStatusAsync(request.RequestId, CancellationToken.None);

        status.ShouldNotBeNull();
        status.RequestId.ShouldBe(request.RequestId);
        status.Status.ShouldBe(ErasureRequestStatus.Scheduled);
    }

    [Fact]
    public async Task Return_null_for_nonexistent_request()
    {
        var status = await _sut.GetStatusAsync(Guid.NewGuid(), CancellationToken.None);

        status.ShouldBeNull();
    }

    [Fact]
    public async Task Throw_on_duplicate_request_id()
    {
        var requestId = Guid.NewGuid();
        var request1 = CreateRequest(requestId);
        await _sut.SaveRequestAsync(request1, DateTimeOffset.UtcNow.AddHours(72), CancellationToken.None);

        var request2 = CreateRequest(requestId);
        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.SaveRequestAsync(request2, DateTimeOffset.UtcNow.AddHours(72), CancellationToken.None));
    }

    [Fact]
    public async Task Update_status_successfully()
    {
        var request = CreateRequest();
        await _sut.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddHours(72), CancellationToken.None);

        var updated = await _sut.UpdateStatusAsync(
            request.RequestId, ErasureRequestStatus.InProgress, null, CancellationToken.None);

        updated.ShouldBeTrue();
        var status = await _sut.GetStatusAsync(request.RequestId, CancellationToken.None);
        status!.Status.ShouldBe(ErasureRequestStatus.InProgress);
    }

    [Fact]
    public async Task Return_false_when_updating_nonexistent_request()
    {
        var updated = await _sut.UpdateStatusAsync(
            Guid.NewGuid(), ErasureRequestStatus.InProgress, null, CancellationToken.None);

        updated.ShouldBeFalse();
    }

    [Fact]
    public async Task Prevent_concurrent_transition_to_in_progress()
    {
        var request = CreateRequest();
        await _sut.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddHours(72), CancellationToken.None);

        var first = await _sut.UpdateStatusAsync(
            request.RequestId, ErasureRequestStatus.InProgress, null, CancellationToken.None);
        var second = await _sut.UpdateStatusAsync(
            request.RequestId, ErasureRequestStatus.InProgress, null, CancellationToken.None);

        first.ShouldBeTrue();
        second.ShouldBeFalse();
    }

    [Fact]
    public async Task Record_completion()
    {
        var request = CreateRequest();
        var certId = Guid.NewGuid();
        await _sut.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddHours(72), CancellationToken.None);

        await _sut.RecordCompletionAsync(request.RequestId, 5, 100, certId, CancellationToken.None);

        var status = await _sut.GetStatusAsync(request.RequestId, CancellationToken.None);
        status!.Status.ShouldBe(ErasureRequestStatus.Completed);
        status.KeysDeleted.ShouldBe(5);
        status.RecordsAffected.ShouldBe(100);
    }

    [Fact]
    public async Task Throw_on_completion_for_nonexistent_request()
    {
        await Should.ThrowAsync<KeyNotFoundException>(
            () => _sut.RecordCompletionAsync(Guid.NewGuid(), 0, 0, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Track_request_count()
    {
        _sut.RequestCount.ShouldBe(0);

        await _sut.SaveRequestAsync(CreateRequest(), DateTimeOffset.UtcNow.AddHours(72), CancellationToken.None);

        _sut.RequestCount.ShouldBe(1);
    }

    [Fact]
    public async Task Return_service_for_certificate_store()
    {
        var service = _sut.GetService(typeof(IErasureCertificateStore));

        service.ShouldNotBeNull();
        service.ShouldBeAssignableTo<IErasureCertificateStore>();
    }

    [Fact]
    public async Task Return_service_for_query_store()
    {
        var service = _sut.GetService(typeof(IErasureQueryStore));

        service.ShouldNotBeNull();
        service.ShouldBeAssignableTo<IErasureQueryStore>();
    }

    [Fact]
    public void Return_null_for_unsupported_service_type()
    {
        var service = _sut.GetService(typeof(string));

        service.ShouldBeNull();
    }

    [Fact]
    public void Throw_when_getting_service_with_null_type()
    {
        Should.Throw<ArgumentNullException>(() => _sut.GetService(null!));
    }

    [Fact]
    public async Task Clear_all_data()
    {
        await _sut.SaveRequestAsync(CreateRequest(), DateTimeOffset.UtcNow.AddHours(72), CancellationToken.None);
        _sut.RequestCount.ShouldBe(1);

        _sut.Clear();

        _sut.RequestCount.ShouldBe(0);
    }

    [Fact]
    public async Task Save_and_retrieve_certificate()
    {
        var cert = new ErasureCertificate
        {
            CertificateId = Guid.NewGuid(),
            RequestId = Guid.NewGuid(),
            DataSubjectReference = "hash",
            RequestReceivedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            Method = ErasureMethod.CryptographicErasure,
            Summary = new ErasureSummary { KeysDeleted = 1, RecordsAffected = 10, DataCategories = [], TablesAffected = [] },
            Verification = new VerificationSummary { Verified = true, Methods = VerificationMethod.None, VerifiedAt = DateTimeOffset.UtcNow },
            LegalBasis = ErasureLegalBasis.DataSubjectRequest,
            Signature = "sig",
            RetainUntil = DateTimeOffset.UtcNow.AddYears(7)
        };

        await _sut.SaveCertificateAsync(cert, CancellationToken.None);
        var retrieved = await _sut.GetCertificateAsync(cert.RequestId, CancellationToken.None);

        retrieved.ShouldNotBeNull();
        retrieved.CertificateId.ShouldBe(cert.CertificateId);
    }

    [Fact]
    public async Task Return_null_certificate_for_unknown_request()
    {
        var cert = await _sut.GetCertificateAsync(Guid.NewGuid(), CancellationToken.None);

        cert.ShouldBeNull();
    }

    [Fact]
    public async Task Get_scheduled_requests_only_past_execution_time()
    {
        var pastRequest = CreateRequest();
        await _sut.SaveRequestAsync(pastRequest, DateTimeOffset.UtcNow.AddHours(-1), CancellationToken.None);

        var futureRequest = CreateRequest();
        await _sut.SaveRequestAsync(futureRequest, DateTimeOffset.UtcNow.AddHours(1), CancellationToken.None);

        var scheduled = await _sut.GetScheduledRequestsAsync(10, CancellationToken.None);

        scheduled.Count.ShouldBe(1);
        scheduled[0].RequestId.ShouldBe(pastRequest.RequestId);
    }

    [Fact]
    public async Task List_requests_with_status_filter()
    {
        var request = CreateRequest();
        await _sut.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddHours(72), CancellationToken.None);

        var results = await _sut.ListRequestsAsync(
            ErasureRequestStatus.Scheduled, null, null, null, 1, 10, CancellationToken.None);

        results.Count.ShouldBe(1);
    }

    [Fact]
    public async Task List_requests_validates_page_parameters()
    {
        await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => _sut.ListRequestsAsync(null, null, null, null, 0, 10, CancellationToken.None));

        await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => _sut.ListRequestsAsync(null, null, null, null, 1, 0, CancellationToken.None));

        await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => _sut.ListRequestsAsync(null, null, null, null, 1, 1001, CancellationToken.None));
    }
}
