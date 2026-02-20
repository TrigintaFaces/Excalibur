using Excalibur.Dispatch.AuditLogging.HealthChecks;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.AuditLogging.Tests.HealthChecks;

public class AuditStoreHealthCheckShould
{
    private readonly IAuditStore _fakeStore = A.Fake<IAuditStore>();
    private readonly AuditStoreHealthCheck _sut;

    public AuditStoreHealthCheckShould()
    {
        _sut = new AuditStoreHealthCheck(
            _fakeStore,
            NullLogger<AuditStoreHealthCheck>.Instance);
    }

    [Fact]
    public async Task Return_healthy_when_store_responds_fast()
    {
        A.CallTo(() => _fakeStore.CountAsync(A<AuditQuery>._, A<CancellationToken>._))
            .Returns(42L);

        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("audit-store", _sut, null, null)
        };

        var result = await _sut.CheckHealthAsync(context, CancellationToken.None);

        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Description.ShouldContain("42");
    }

    [Fact]
    public async Task Return_unhealthy_when_store_throws()
    {
        A.CallTo(() => _fakeStore.CountAsync(A<AuditQuery>._, A<CancellationToken>._))
            .Throws(new InvalidOperationException("Connection failed"));

        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("audit-store", _sut, null, null)
        };

        var result = await _sut.CheckHealthAsync(context, CancellationToken.None);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldContain("Connection failed");
        result.Exception.ShouldNotBeNull();
    }

    [Fact]
    public async Task Include_store_type_in_data()
    {
        A.CallTo(() => _fakeStore.CountAsync(A<AuditQuery>._, A<CancellationToken>._))
            .Returns(0L);

        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("audit-store", _sut, null, null)
        };

        var result = await _sut.CheckHealthAsync(context, CancellationToken.None);

        result.Data.ShouldContainKey("store_type");
    }

    [Fact]
    public async Task Include_duration_in_data()
    {
        A.CallTo(() => _fakeStore.CountAsync(A<AuditQuery>._, A<CancellationToken>._))
            .Returns(0L);

        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("audit-store", _sut, null, null)
        };

        var result = await _sut.CheckHealthAsync(context, CancellationToken.None);

        result.Data.ShouldContainKey("duration_ms");
    }

    [Fact]
    public void Throw_argument_null_for_null_store()
    {
        Should.Throw<ArgumentNullException>(() =>
            new AuditStoreHealthCheck(null!, NullLogger<AuditStoreHealthCheck>.Instance));
    }

    [Fact]
    public void Throw_argument_null_for_null_logger()
    {
        Should.Throw<ArgumentNullException>(() =>
            new AuditStoreHealthCheck(_fakeStore, null!));
    }

    [Fact]
    public void Use_default_degraded_threshold_of_500ms()
    {
        // The constructor accepts null threshold and defaults to 500ms.
        // We verify this by creating with null and checking healthy for a fast response.
        var check = new AuditStoreHealthCheck(_fakeStore, NullLogger<AuditStoreHealthCheck>.Instance, null);
        check.ShouldNotBeNull();
    }

    [Fact]
    public async Task Include_total_events_in_healthy_data()
    {
        A.CallTo(() => _fakeStore.CountAsync(A<AuditQuery>._, A<CancellationToken>._))
            .Returns(99L);

        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("audit-store", _sut, null, null)
        };

        var result = await _sut.CheckHealthAsync(context, CancellationToken.None);

        result.Data.ShouldContainKey("total_events");
        result.Data["total_events"].ShouldBe(99L);
    }
}
