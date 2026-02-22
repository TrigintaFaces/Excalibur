using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.HealthChecks;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptionHealthCheckShould
{
	private readonly IEncryptionProvider _encryptionProvider = A.Fake<IEncryptionProvider>();
	private readonly IKeyManagementProvider _keyManagementProvider = A.Fake<IKeyManagementProvider>();

	[Fact]
	public async Task Return_unhealthy_when_encryption_fails()
	{
		// Arrange
		A.CallTo(() => _encryptionProvider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.Throws(new EncryptionException("Encryption failed"));

		var sut = CreateHealthCheck();

		// Act
		var result = await sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("failed");
	}

	[Fact]
	public async Task Return_healthy_when_round_trip_succeeds()
	{
		// Arrange
		SetupSuccessfulRoundTrip();

		var options = new EncryptionHealthCheckOptions { VerifyKeyManagement = false };
		var sut = CreateHealthCheck(options);

		// Act
		var result = await sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Data.ShouldContainKey("provider_name");
		result.Data.ShouldContainKey("round_trip_result");
		result.Data.ShouldContainKey("round_trip_duration_ms");
	}

	[Fact]
	public async Task Include_key_management_check_when_provider_available()
	{
		// Arrange
		SetupSuccessfulRoundTrip();

		A.CallTo(() => _keyManagementProvider.GetActiveKeyAsync(null, A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(new KeyMetadata
			{
				KeyId = "active-key",
				Version = 1,
				Status = KeyStatus.Active,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				CreatedAt = DateTimeOffset.UtcNow,
			}));

		var sut = CreateHealthCheck();

		// Act
		var result = await sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Data.ShouldContainKey("key_management_result");
	}

	[Fact]
	public async Task Report_key_management_failure_when_no_active_key()
	{
		// Arrange
		SetupSuccessfulRoundTrip();

		A.CallTo(() => _keyManagementProvider.GetActiveKeyAsync(null, A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(null));

		var sut = CreateHealthCheck();

		// Act
		var result = await sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Data.ShouldContainKey("key_management_result");
		result.Status.ShouldBe(HealthStatus.Unhealthy);
	}

	[Fact]
	public async Task Return_unhealthy_when_key_management_throws()
	{
		// Arrange
		SetupSuccessfulRoundTrip();

		A.CallTo(() => _keyManagementProvider.GetActiveKeyAsync(null, A<CancellationToken>._))
			.Throws(new InvalidOperationException("KMS unavailable"));

		var sut = CreateHealthCheck();

		// Act
		var result = await sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
	}

	[Fact]
	public async Task Skip_key_management_check_when_disabled()
	{
		// Arrange
		SetupSuccessfulRoundTrip();

		var options = new EncryptionHealthCheckOptions { VerifyKeyManagement = false };
		var sut = CreateHealthCheck(options);

		// Act
		var result = await sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Data.ShouldNotContainKey("key_management_result");
		A.CallTo(() => _keyManagementProvider.GetActiveKeyAsync(A<string?>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task Skip_key_management_when_provider_null()
	{
		// Arrange
		SetupSuccessfulRoundTrip();

		var sut = new EncryptionHealthCheck(
			_encryptionProvider,
			null,
			null,
			NullLogger<EncryptionHealthCheck>.Instance);

		// Act
		var result = await sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Data.ShouldNotContainKey("key_management_result");
	}

	[Fact]
	public async Task Return_degraded_when_round_trip_exceeds_degraded_threshold()
	{
		// Arrange
		byte[]? capturedPlaintext = null;
		A.CallTo(() => _encryptionProvider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.ReturnsLazily(async call =>
			{
				await Task.Delay(20);
				capturedPlaintext = call.GetArgument<byte[]>(0);
				return new EncryptedData
				{
					Ciphertext = capturedPlaintext!,
					Iv = [4, 5, 6],
					AuthTag = [7, 8, 9],
					KeyId = "test-key",
					KeyVersion = 1,
					Algorithm = EncryptionAlgorithm.Aes256Gcm,
				};
			});
		A.CallTo(() => _encryptionProvider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.ReturnsLazily(async _ =>
			{
				await Task.Delay(20);
				return capturedPlaintext ?? [];
			});

		var options = new EncryptionHealthCheckOptions
		{
			DegradedThreshold = TimeSpan.FromMilliseconds(1),
			VerifyKeyManagement = false
		};
		var sut = CreateHealthCheck(options);

		// Act
		var result = await sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Description.ShouldContain("Round-trip is slow");
	}

	[Fact]
	public async Task Return_degraded_when_key_management_exceeds_degraded_threshold()
	{
		// Arrange
		SetupSuccessfulRoundTrip();
		A.CallTo(() => _keyManagementProvider.GetActiveKeyAsync(null, A<CancellationToken>._))
			.ReturnsLazily(async _ =>
			{
				await Task.Delay(20);
				return new KeyMetadata
				{
					KeyId = "active-key",
					Version = 1,
					Status = KeyStatus.Active,
					Algorithm = EncryptionAlgorithm.Aes256Gcm,
					CreatedAt = DateTimeOffset.UtcNow,
				};
			});

		var options = new EncryptionHealthCheckOptions
		{
			DegradedThreshold = TimeSpan.FromMilliseconds(1),
			VerifyKeyManagement = true
		};
		var sut = CreateHealthCheck(options);

		// Act
		var result = await sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Description.ShouldContain("Key management is slow");
	}

	[Fact]
	public async Task Return_degraded_with_combined_timing_warnings_when_round_trip_and_key_management_are_slow()
	{
		// Arrange
		byte[]? capturedPlaintext = null;
		A.CallTo(() => _encryptionProvider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.ReturnsLazily(async call =>
			{
				await Task.Delay(20);
				capturedPlaintext = call.GetArgument<byte[]>(0);
				return new EncryptedData
				{
					Ciphertext = capturedPlaintext!,
					Iv = [4, 5, 6],
					AuthTag = [7, 8, 9],
					KeyId = "timing-key",
					KeyVersion = 1,
					Algorithm = EncryptionAlgorithm.Aes256Gcm,
				};
			});
		A.CallTo(() => _encryptionProvider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.ReturnsLazily(async _ =>
			{
				await Task.Delay(20);
				return capturedPlaintext ?? [];
			});
		A.CallTo(() => _keyManagementProvider.GetActiveKeyAsync(null, A<CancellationToken>._))
			.ReturnsLazily(async _ =>
			{
				await Task.Delay(20);
				return new KeyMetadata
				{
					KeyId = "active-key",
					Version = 1,
					Status = KeyStatus.Active,
					Algorithm = EncryptionAlgorithm.Aes256Gcm,
					CreatedAt = DateTimeOffset.UtcNow,
				};
			});

		var options = new EncryptionHealthCheckOptions
		{
			DegradedThreshold = TimeSpan.FromMilliseconds(1),
			VerifyKeyManagement = true
		};
		var sut = CreateHealthCheck(options);

		// Act
		var result = await sut.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Description.ShouldContain("Round-trip is slow");
		result.Description.ShouldContain("Key management is slow");
	}

	[Fact]
	public void Throw_for_null_encryption_provider()
	{
		Should.Throw<ArgumentNullException>(() =>
			new EncryptionHealthCheck(null!, null, null, NullLogger<EncryptionHealthCheck>.Instance));
	}

	[Fact]
	public void Throw_for_null_logger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new EncryptionHealthCheck(_encryptionProvider, null, null, null!));
	}

	[Fact]
	public void Throw_for_null_options()
	{
		Should.Throw<ArgumentNullException>(() =>
			new EncryptionHealthCheck(_encryptionProvider, null, null,
				NullLogger<EncryptionHealthCheck>.Instance, null!));
	}

	[Fact]
	public void Have_default_degraded_threshold_of_100ms()
	{
		var options = EncryptionHealthCheckOptions.Default;
		options.DegradedThreshold.ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	[Fact]
	public void Have_default_verify_key_management_enabled()
	{
		var options = EncryptionHealthCheckOptions.Default;
		options.VerifyKeyManagement.ShouldBeTrue();
	}

	private void SetupSuccessfulRoundTrip()
	{
		byte[]? capturedData = null;

		A.CallTo(() => _encryptionProvider.EncryptAsync(A<byte[]>._, A<EncryptionContext>._, A<CancellationToken>._))
			.ReturnsLazily(call =>
			{
				capturedData = call.GetArgument<byte[]>(0);
				return Task.FromResult(new EncryptedData
				{
					Ciphertext = capturedData!,
					Iv = [4, 5, 6],
					AuthTag = [7, 8, 9],
					KeyId = "test-key",
					KeyVersion = 1,
					Algorithm = EncryptionAlgorithm.Aes256Gcm,
				});
			});

		A.CallTo(() => _encryptionProvider.DecryptAsync(A<EncryptedData>._, A<EncryptionContext>._, A<CancellationToken>._))
			.ReturnsLazily(_ => Task.FromResult(capturedData ?? Array.Empty<byte>()));
	}

	private EncryptionHealthCheck CreateHealthCheck(EncryptionHealthCheckOptions? options = null)
	{
		return new EncryptionHealthCheck(
			_encryptionProvider,
			_keyManagementProvider,
			null,
			NullLogger<EncryptionHealthCheck>.Instance,
			options ?? EncryptionHealthCheckOptions.Default);
	}
}
