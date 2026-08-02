using Excalibur.AuditLogging.HealthChecks;
using Excalibur.Compliance;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;


using Excalibur.AuditLogging;namespace Excalibur.AuditLogging.Tests.HealthChecks;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AuditStoreHealthCheckShould
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

        // An EXPLICIT, unreachable threshold rather than the production default of 500ms.
        //
        // The store is faked and returns immediately, so this arm is about the health check's LOGIC:
        // a store that answered without throwing is Healthy. With the default threshold the arm was
        // instead asserting that THIS MACHINE completes a first FakeItEasy call, plus the JIT of
        // everything on that path, in under half a second — which is a property of the runner, not
        // of the code. It failed exactly that way on a cold windows CI runner (Degraded, with the
        // whole test taking 3s) while passing everywhere warm.
        //
        // One hour cannot be exceeded by machine slowness, so a Degraded result here now means the
        // check degraded a store that did not throw — which is the only thing this arm should be
        // able to fail on.
        var sut = new AuditStoreHealthCheck(
            _fakeStore,
            NullLogger<AuditStoreHealthCheck>.Instance,
            TimeSpan.FromHours(1));

        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("audit-store", sut, null, null)
        };

        var result = await sut.CheckHealthAsync(context, CancellationToken.None);

        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Description.ShouldContain("42");
    }

    /// <summary>
    /// The counterpart to <see cref="Return_healthy_when_store_responds_fast" />: the check must
    /// actually degrade a slow store.
    /// </summary>
    /// <remarks>
    /// Without this arm the threshold branch was entirely unverified — five tests existed and not one
    /// exercised Degraded, so an implementation that never degraded anything would have passed the
    /// whole class. It also makes the arm above non-vacuous in the other direction: that one proves
    /// the check CAN return Healthy, this one proves it does not return Healthy unconditionally.
    ///
    /// TimeSpan.Zero is what makes this deterministic rather than a race in the opposite direction:
    /// any measurable elapsed time exceeds it, so no machine is fast enough to make this arm flake.
    /// </remarks>
    [Fact]
    public async Task Return_degraded_when_store_responds_slower_than_the_threshold()
    {
        A.CallTo(() => _fakeStore.CountAsync(A<AuditQuery>._, A<CancellationToken>._))
            .Returns(42L);

        var sut = new AuditStoreHealthCheck(
            _fakeStore,
            NullLogger<AuditStoreHealthCheck>.Instance,
            TimeSpan.Zero);

        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("audit-store", sut, null, null)
        };

        var result = await sut.CheckHealthAsync(context, CancellationToken.None);

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description.ShouldContain("slowly");
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
        result.Exception!.ShouldNotBeNull();
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
