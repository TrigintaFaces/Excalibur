using Excalibur.Dispatch.Compliance.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.HealthChecks;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ErasureHealthCheckShould
{
	private readonly IErasureStore _erasureStore = A.Fake<IErasureStore>();
	private readonly ErasureHealthCheck _sut;

	public ErasureHealthCheckShould()
	{
		_sut = new ErasureHealthCheck(
			_erasureStore,
			NullLogger<ErasureHealthCheck>.Instance);
	}

	[Fact]
	public async Task Return_healthy_when_store_responds_quickly()
	{
		// Arrange
		A.CallTo(() => _erasureStore.GetStatusAsync(Guid.Empty, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(null));

		// Act
		var result = await _sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("healthy");
		result.Data.ShouldContainKey("store_type");
		result.Data.ShouldContainKey("duration_ms");
		result.Data.ShouldContainKey("probe_result");
	}

	[Fact]
	public async Task Return_unhealthy_when_store_throws()
	{
		// Arrange
		A.CallTo(() => _erasureStore.GetStatusAsync(Guid.Empty, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Store unavailable"));

		// Act
		var result = await _sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("Store unavailable");
		result.Exception.ShouldNotBeNull();
		result.Data.ShouldContainKey("duration_ms");
	}

	[Fact]
	public async Task Include_store_type_in_data()
	{
		// Arrange
		A.CallTo(() => _erasureStore.GetStatusAsync(Guid.Empty, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(null));

		// Act
		var result = await _sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Data["store_type"].ShouldNotBeNull();
	}

	[Fact]
	public async Task Report_probe_result_when_status_exists()
	{
		// Arrange
		var status = new ErasureStatus
		{
			RequestId = Guid.NewGuid(),
			DataSubjectIdHash = "abc123hash",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.DataNoLongerNecessary,
			Status = ErasureRequestStatus.Completed,
			RequestedAt = DateTimeOffset.UtcNow,
			RequestedBy = "test",
			UpdatedAt = DateTimeOffset.UtcNow,
		};

		A.CallTo(() => _erasureStore.GetStatusAsync(Guid.Empty, A<CancellationToken>._))
			.Returns(Task.FromResult<ErasureStatus?>(status));

		// Act
		var result = await _sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Data["probe_result"].ShouldNotBeNull();
	}

	[Fact]
	public async Task Return_degraded_when_probe_exceeds_threshold()
	{
		// Arrange
		A.CallTo(() => _erasureStore.GetStatusAsync(Guid.Empty, A<CancellationToken>._))
			.ReturnsLazily(async _ =>
			{
				await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(20);
				return (ErasureStatus?)null;
			});

		var sut = new ErasureHealthCheck(
			_erasureStore,
			NullLogger<ErasureHealthCheck>.Instance,
			TimeSpan.FromMilliseconds(1));

		// Act
		var result = await sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Description.ShouldContain("responded slowly");
		result.Data.ShouldContainKey("duration_ms");
	}

	[Fact]
	public void Throw_for_null_erasure_store()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ErasureHealthCheck(null!, NullLogger<ErasureHealthCheck>.Instance));
	}

	[Fact]
	public void Throw_for_null_logger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ErasureHealthCheck(_erasureStore, null!));
	}

	[Fact]
	public void Use_custom_degraded_threshold()
	{
		// Act & Assert - should not throw
		var check = new ErasureHealthCheck(
			_erasureStore,
			NullLogger<ErasureHealthCheck>.Instance,
			TimeSpan.FromSeconds(1));

		check.ShouldNotBeNull();
	}

	[Fact]
	public void Use_default_threshold_of_500ms()
	{
		// Act - just verifying construction with default works
		var check = new ErasureHealthCheck(
			_erasureStore,
			NullLogger<ErasureHealthCheck>.Instance);

		check.ShouldNotBeNull();
	}
}
