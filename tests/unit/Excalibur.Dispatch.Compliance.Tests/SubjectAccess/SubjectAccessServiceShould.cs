using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance.Tests.SubjectAccess;

public class SubjectAccessServiceShould
{
    private readonly SubjectAccessService _sut;
    private readonly SubjectAccessOptions _options = new();

    public SubjectAccessServiceShould()
    {
        _sut = new SubjectAccessService(
            Microsoft.Extensions.Options.Options.Create(_options),
            NullLogger<SubjectAccessService>.Instance);
    }

    [Fact]
    public async Task Create_request_with_pending_status()
    {
        var request = new SubjectAccessRequest
        {
            SubjectId = "user-1",
            RequestType = SubjectAccessRequestType.Access
        };

        var result = await _sut.CreateRequestAsync(request, CancellationToken.None);

        result.ShouldNotBeNull();
        result.RequestId.ShouldNotBeNullOrWhiteSpace();
        result.Status.ShouldBe(SubjectAccessRequestStatus.Pending);
    }

    [Fact]
    public async Task Set_deadline_based_on_options()
    {
        var request = new SubjectAccessRequest
        {
            SubjectId = "user-1",
            RequestType = SubjectAccessRequestType.Access,
            RequestedAt = DateTimeOffset.UtcNow
        };

        var result = await _sut.CreateRequestAsync(request, CancellationToken.None);

        result.Deadline.ShouldNotBeNull();
    }

    [Fact]
    public async Task Auto_fulfill_when_option_is_enabled()
    {
        var options = new SubjectAccessOptions { AutoFulfill = true };
        var sut = new SubjectAccessService(
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<SubjectAccessService>.Instance);

        var request = new SubjectAccessRequest
        {
            SubjectId = "user-1",
            RequestType = SubjectAccessRequestType.Access
        };

        var result = await sut.CreateRequestAsync(request, CancellationToken.None);

        result.Status.ShouldBe(SubjectAccessRequestStatus.Fulfilled);
        result.FulfilledAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Get_request_status()
    {
        var request = new SubjectAccessRequest
        {
            SubjectId = "user-1",
            RequestType = SubjectAccessRequestType.Rectification
        };
        var created = await _sut.CreateRequestAsync(request, CancellationToken.None);

        var status = await _sut.GetRequestStatusAsync(created.RequestId, CancellationToken.None);

        status.ShouldNotBeNull();
        status.RequestId.ShouldBe(created.RequestId);
    }

    [Fact]
    public async Task Return_null_for_unknown_request()
    {
        var status = await _sut.GetRequestStatusAsync("unknown-id", CancellationToken.None);

        status.ShouldBeNull();
    }

    [Fact]
    public async Task Fulfill_request()
    {
        var request = new SubjectAccessRequest
        {
            SubjectId = "user-1",
            RequestType = SubjectAccessRequestType.Erasure
        };
        var created = await _sut.CreateRequestAsync(request, CancellationToken.None);

        var fulfilled = await _sut.FulfillRequestAsync(created.RequestId, CancellationToken.None);

        fulfilled.Status.ShouldBe(SubjectAccessRequestStatus.Fulfilled);
        fulfilled.FulfilledAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Throw_when_fulfilling_unknown_request()
    {
        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.FulfillRequestAsync("unknown-id", CancellationToken.None));
    }

    [Fact]
    public async Task Throw_when_fulfilling_already_fulfilled_request()
    {
        var request = new SubjectAccessRequest
        {
            SubjectId = "user-1",
            RequestType = SubjectAccessRequestType.Access
        };
        var created = await _sut.CreateRequestAsync(request, CancellationToken.None);
        await _sut.FulfillRequestAsync(created.RequestId, CancellationToken.None);

        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.FulfillRequestAsync(created.RequestId, CancellationToken.None));
    }

    [Fact]
    public async Task Throw_when_creating_null_request()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.CreateRequestAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Throw_when_getting_status_with_null_id()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.GetRequestStatusAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Throw_when_fulfilling_with_null_id()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.FulfillRequestAsync(null!, CancellationToken.None));
    }

    [Fact]
    public void Throw_when_options_are_null()
    {
        Should.Throw<ArgumentNullException>(
            () => new SubjectAccessService(null!, NullLogger<SubjectAccessService>.Instance));
    }

    [Fact]
    public void Throw_when_logger_is_null()
    {
        Should.Throw<ArgumentNullException>(
            () => new SubjectAccessService(Microsoft.Extensions.Options.Options.Create(new SubjectAccessOptions()), null!));
    }
}
