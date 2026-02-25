using System.Diagnostics;
using System.Reflection;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class MultiRegionKeyProviderShould : IDisposable
{
	private readonly IKeyManagementProvider _primary = A.Fake<IKeyManagementProvider>();
	private readonly IKeyManagementProvider _secondary = A.Fake<IKeyManagementProvider>();
	private readonly MultiRegionOptions _options;
	private readonly MultiRegionKeyProvider _sut;

	public MultiRegionKeyProviderShould()
	{
		_options = new MultiRegionOptions
		{
			Primary = new RegionConfiguration
			{
				RegionId = "us-east-1",
				Endpoint = new Uri("https://primary.example.com"),
			},
			Secondary = new RegionConfiguration
			{
				RegionId = "us-west-2",
				Endpoint = new Uri("https://secondary.example.com"),
			},
			HealthCheckInterval = TimeSpan.FromHours(1), // Very long to avoid background noise in tests
			EnableAutomaticFailover = false,
		};

		// Allow health check to succeed for both
		A.CallTo(() => _primary.ListKeysAsync(A<KeyStatus?>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<KeyMetadata>>([]));
		A.CallTo(() => _secondary.ListKeysAsync(A<KeyStatus?>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult<IReadOnlyList<KeyMetadata>>([]));

		_sut = new MultiRegionKeyProvider(
			_primary,
			_secondary,
			_options,
			NullLogger<MultiRegionKeyProvider>.Instance);
	}

	[Fact]
	public void Report_primary_as_active_region_by_default()
	{
		_sut.ActiveRegionId.ShouldBe("us-east-1");
	}

	[Fact]
	public void Not_be_in_failover_mode_by_default()
	{
		_sut.IsInFailoverMode.ShouldBeFalse();
	}

	[Fact]
	public async Task Delegate_get_key_to_active_provider()
	{
		// Arrange
		var expected = new KeyMetadata
		{
			KeyId = "k1",
			Version = 1,
			Status = KeyStatus.Active,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = DateTimeOffset.UtcNow,
		};
		A.CallTo(() => _primary.GetKeyAsync("k1", A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(expected));

		// Act
		var result = await _sut.GetKeyAsync("k1", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeSameAs(expected);
	}

	[Fact]
	public async Task Delegate_get_active_key_to_active_provider()
	{
		// Arrange
		A.CallTo(() => _primary.GetActiveKeyAsync(A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(null));

		// Act
		var result = await _sut.GetActiveKeyAsync(null, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeNull();
		A.CallTo(() => _primary.GetActiveKeyAsync(A<string?>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Delegate_rotate_key_to_active_provider()
	{
		// Arrange
		var expected = new KeyRotationResult { Success = true };
		A.CallTo(() => _primary.RotateKeyAsync("k1", EncryptionAlgorithm.Aes256Gcm, null, null, A<CancellationToken>._))
			.Returns(Task.FromResult(expected));

		// Act
		var result = await _sut.RotateKeyAsync("k1", EncryptionAlgorithm.Aes256Gcm, null, null, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeSameAs(expected);
	}

	[Fact]
	public async Task Delegate_list_keys_to_active_provider()
	{
		// Arrange
		IReadOnlyList<KeyMetadata> expected = [];
		A.CallTo(() => _primary.ListKeysAsync(A<KeyStatus?>._, A<string?>._, A<CancellationToken>._))
			.Returns(Task.FromResult(expected));

		// Act
		var result = await _sut.ListKeysAsync(null, null, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeSameAs(expected);
	}

	[Fact]
	public async Task Delegate_delete_key_to_active_provider()
	{
		// Arrange
		A.CallTo(() => _primary.DeleteKeyAsync("k1", 90, A<CancellationToken>._))
			.Returns(Task.FromResult(true));

		// Act
		var result = await _sut.DeleteKeyAsync("k1", 90, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task Delegate_suspend_key_to_active_provider()
	{
		// Arrange
		A.CallTo(() => _primary.SuspendKeyAsync("k1", "test", A<CancellationToken>._))
			.Returns(Task.FromResult(true));

		// Act
		var result = await _sut.SuspendKeyAsync("k1", "test", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task Return_replication_status()
	{
		// Act
		var status = await _sut.GetReplicationStatusAsync(CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		status.ShouldNotBeNull();
		status.ReplicationMode.ShouldBe(ReplicationMode.Asynchronous);
	}

	[Fact]
	public async Task Force_failover_switches_active_region()
	{
		// Act
		await _sut.ForceFailoverAsync("test failover", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		_sut.IsInFailoverMode.ShouldBeTrue();
		_sut.ActiveRegionId.ShouldBe("us-west-2");
	}

	[Fact]
	public async Task Throw_when_already_in_failover_mode()
	{
		// Arrange
		await _sut.ForceFailoverAsync("first", CancellationToken.None)
			.ConfigureAwait(false);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.ForceFailoverAsync("second", CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Failback_switches_back_to_primary()
	{
		// Arrange
		await _sut.ForceFailoverAsync("failover", CancellationToken.None)
			.ConfigureAwait(false);
		_sut.IsInFailoverMode.ShouldBeTrue();

		// Act
		await _sut.FailbackToPrimaryAsync("recovered", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		_sut.IsInFailoverMode.ShouldBeFalse();
		_sut.ActiveRegionId.ShouldBe("us-east-1");
	}

	[Fact]
	public async Task Throw_when_failback_not_in_failover_mode()
	{
		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.FailbackToPrimaryAsync("no failover", CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_on_force_failover_with_empty_reason()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.ForceFailoverAsync("", CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_on_failback_with_empty_reason()
	{
		await _sut.ForceFailoverAsync("test", CancellationToken.None)
			.ConfigureAwait(false);

		await Should.ThrowAsync<ArgumentException>(
			() => _sut.FailbackToPrimaryAsync("", CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_on_operations_after_dispose()
	{
		// Arrange
		_sut.Dispose();

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(
			() => _sut.GetKeyAsync("k1", CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public void Allow_double_dispose()
	{
		_sut.Dispose();
		_sut.Dispose(); // Should not throw
	}

	[Fact]
	public void Dispose_waits_for_health_check_task_with_bounded_spin_wait()
	{
		// Arrange
		var primary = A.Fake<IKeyManagementProvider>();
		var secondary = A.Fake<IKeyManagementProvider>();
		var options = new MultiRegionOptions
		{
			Primary = new RegionConfiguration
			{
				RegionId = "us-east-1",
				Endpoint = new Uri("https://primary.example.com"),
			},
			Secondary = new RegionConfiguration
			{
				RegionId = "us-west-2",
				Endpoint = new Uri("https://secondary.example.com"),
			},
			HealthCheckInterval = TimeSpan.FromMilliseconds(1),
			OperationTimeout = TimeSpan.FromSeconds(2),
			EnableAutomaticFailover = false,
		};

		_ = A.CallTo(() => primary.ListKeysAsync(A<KeyStatus?>._, A<string?>._, A<CancellationToken>._))
			.ReturnsLazily(async call =>
			{
				await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(250, call.GetArgument<CancellationToken>(2));
				return (IReadOnlyList<KeyMetadata>)[];
			});
		_ = A.CallTo(() => secondary.ListKeysAsync(A<KeyStatus?>._, A<string?>._, A<CancellationToken>._))
			.ReturnsLazily(async call =>
			{
				await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(250, call.GetArgument<CancellationToken>(2));
				return (IReadOnlyList<KeyMetadata>)[];
			});

		var sut = new MultiRegionKeyProvider(
			primary,
			secondary,
			options,
			NullLogger<MultiRegionKeyProvider>.Instance);

		// Let the background loop start and enter a health-check cycle.
		global::Tests.Shared.Infrastructure.TestTiming.Sleep(30);
		GetHealthCheckTask(sut).IsCompleted.ShouldBeFalse();

		// Act
		var sw = Stopwatch.StartNew();
		sut.Dispose();
		sw.Stop();

		// Assert
		sw.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2));
	}

	[Fact]
	public void Throw_for_null_primary()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MultiRegionKeyProvider(null!, _secondary, _options, NullLogger<MultiRegionKeyProvider>.Instance));
	}

	[Fact]
	public void Throw_for_null_secondary()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MultiRegionKeyProvider(_primary, null!, _options, NullLogger<MultiRegionKeyProvider>.Instance));
	}

	[Fact]
	public void Throw_for_null_options()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MultiRegionKeyProvider(_primary, _secondary, null!, NullLogger<MultiRegionKeyProvider>.Instance));
	}

	[Fact]
	public void Throw_for_null_logger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new MultiRegionKeyProvider(_primary, _secondary, _options, null!));
	}

	[Fact]
	public void Have_default_options_values()
	{
		var options = new MultiRegionOptions
		{
			Primary = new RegionConfiguration { RegionId = "a", Endpoint = new Uri("https://a.com") },
			Secondary = new RegionConfiguration { RegionId = "b", Endpoint = new Uri("https://b.com") },
		};

		options.ReplicationMode.ShouldBe(ReplicationMode.Asynchronous);
		options.RpoTarget.ShouldBe(TimeSpan.FromMinutes(15));
		options.RtoTarget.ShouldBe(TimeSpan.FromMinutes(5));
		options.OperationTimeout.ShouldBe(TimeSpan.FromSeconds(10));
		options.EnableMetrics.ShouldBeTrue();
		options.EnableAuditEvents.ShouldBeTrue();
		options.HealthCheckInterval.ShouldBe(TimeSpan.FromSeconds(30));
		options.FailoverThreshold.ShouldBe(3);
		options.EnableAutomaticFailover.ShouldBeTrue();
	}

	[Fact]
	public void Have_default_region_configuration_values()
	{
		var config = new RegionConfiguration
		{
			RegionId = "us-east-1",
			Endpoint = new Uri("https://kms.example.com"),
		};

		config.MaxAcceptableLatency.ShouldBe(TimeSpan.FromMilliseconds(500));
		config.Enabled.ShouldBeTrue();
		config.Priority.ShouldBe(0);
		config.DisplayName.ShouldBeNull();
		config.ProviderConfiguration.ShouldBeNull();
	}

	[Fact]
	public void Have_default_failover_options_values()
	{
		var options = new FailoverOptions();
		options.Strategy.ShouldBe(FailoverStrategy.GracePeriod);
		options.GracePeriod.ShouldBe(TimeSpan.FromSeconds(30));
		options.EnableNotifications.ShouldBeTrue();
		options.FailoverCooldown.ShouldBe(TimeSpan.FromMinutes(5));
	}

	public void Dispose()
	{
		_sut.Dispose();
	}

	private static Task GetHealthCheckTask(MultiRegionKeyProvider provider)
	{
		var field = typeof(MultiRegionKeyProvider)
			.GetField("_healthCheckTask", BindingFlags.Instance | BindingFlags.NonPublic);
		field.ShouldNotBeNull();
		var value = field!.GetValue(provider);
		value.ShouldNotBeNull();
		value.ShouldBeAssignableTo<Task>();
		return (Task)(value ?? throw new InvalidOperationException("Health check task field should not be null."));
	}
}

