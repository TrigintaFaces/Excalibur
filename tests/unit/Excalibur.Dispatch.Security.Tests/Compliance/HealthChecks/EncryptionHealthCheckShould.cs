// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Security.Tests.Compliance.HealthChecks;

[Trait("Category", TestCategories.Unit)]
public sealed class EncryptionHealthCheckShould
{
	private readonly IEncryptionProvider _encryptionProvider;
	private readonly IKeyManagementProvider _keyManagementProvider;
	private readonly IEncryptionTelemetry _telemetry;
	private readonly IEncryptionTelemetryDetails _telemetryDetails;
	private readonly ILogger<EncryptionHealthCheck> _logger;

	public EncryptionHealthCheckShould()
	{
		_encryptionProvider = A.Fake<IEncryptionProvider>();
		_keyManagementProvider = A.Fake<IKeyManagementProvider>();
		_telemetry = A.Fake<IEncryptionTelemetry>();
		_telemetryDetails = A.Fake<IEncryptionTelemetryDetails>();
		_logger = A.Fake<ILogger<EncryptionHealthCheck>>();

		// Wire up GetService to return telemetry details sub-interface
		_ = A.CallTo(() => _telemetry.GetService(typeof(IEncryptionTelemetryDetails)))
			.Returns(_telemetryDetails);

		// Default: encryption works
		_ = A.CallTo(() => _encryptionProvider.EncryptAsync(
				A<byte[]>._,
				A<EncryptionContext>._,
				A<CancellationToken>._))
			.ReturnsLazily((byte[] data, EncryptionContext _, CancellationToken _) =>
				Task.FromResult(new EncryptedData
				{
					Ciphertext = data,
					Algorithm = EncryptionAlgorithm.Aes256Gcm,
					KeyId = "test-key",
					KeyVersion = 1,
					Iv = new byte[12],
					EncryptedAt = DateTimeOffset.UtcNow,
				}));

		_ = A.CallTo(() => _encryptionProvider.DecryptAsync(
				A<EncryptedData>._,
				A<EncryptionContext>._,
				A<CancellationToken>._))
			.ReturnsLazily((EncryptedData data, EncryptionContext _, CancellationToken _) =>
				Task.FromResult(data.Ciphertext));

		// Default: active key available
		_ = A.CallTo(() => _keyManagementProvider.GetActiveKeyAsync(
				A<string?>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(new KeyMetadata
			{
				KeyId = "active-key-1",
				Version = 1,
				Status = KeyStatus.Active,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				CreatedAt = DateTimeOffset.UtcNow,
			}));
	}

	[Fact]
	public async Task ReturnHealthy_WhenEncryptionWorks()
	{
		// Arrange
		var sut = CreateSut();
		var context = new HealthCheckContext();

		// Act
		var result = await sut.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("healthy");
		result.Data.ShouldContainKey("round_trip_result");
		result.Data.ShouldContainKey("key_management_result");
	}

	[Fact]
	public async Task ReturnUnhealthy_WhenEncryptionFails()
	{
		// Arrange
		_ = A.CallTo(() => _encryptionProvider.EncryptAsync(
				A<byte[]>._,
				A<EncryptionContext>._,
				A<CancellationToken>._))
			.Throws(new EncryptionException("Test encryption failure"));

		var sut = CreateSut();
		var context = new HealthCheckContext();

		// Act
		var result = await sut.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("failed");
	}

	[Fact]
	public async Task ReturnUnhealthy_WhenDecryptionFails()
	{
		// Arrange
		_ = A.CallTo(() => _encryptionProvider.DecryptAsync(
				A<EncryptedData>._,
				A<EncryptionContext>._,
				A<CancellationToken>._))
			.Throws(new EncryptionException("Test decryption failure"));

		var sut = CreateSut();
		var context = new HealthCheckContext();

		// Act
		var result = await sut.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
	}

	[Fact]
	public async Task ReturnUnhealthy_WhenRoundTripDataMismatches()
	{
		// Arrange
		_ = A.CallTo(() => _encryptionProvider.DecryptAsync(
				A<EncryptedData>._,
				A<EncryptionContext>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult(new byte[] { 0x01, 0x02, 0x03 })); // Different data

		var sut = CreateSut();
		var context = new HealthCheckContext();

		// Act
		var result = await sut.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("mismatch");
	}

	[Fact]
	public async Task ReturnUnhealthy_WhenNoActiveKeyAvailable()
	{
		// Arrange
		_ = A.CallTo(() => _keyManagementProvider.GetActiveKeyAsync(
				A<string?>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(null));

		var sut = CreateSut();
		var context = new HealthCheckContext();

		// Act
		var result = await sut.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("No active key");
	}

	[Fact]
	public async Task ReturnHealthy_WhenKeyManagementNotConfigured()
	{
		// Arrange - no key management provider
		var sut = CreateSut(keyProvider: null);
		var context = new HealthCheckContext();

		// Act
		var result = await sut.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
	}

	[Fact]
	public async Task ReturnHealthy_WhenKeyManagementVerificationDisabled()
	{
		// Arrange
		_ = A.CallTo(() => _keyManagementProvider.GetActiveKeyAsync(
				A<string?>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult<KeyMetadata?>(null)); // Would fail if checked

		var options = new EncryptionHealthCheckOptions
		{
			VerifyKeyManagement = false,
		};

		var sut = CreateSut(options: options);
		var context = new HealthCheckContext();

		// Act
		var result = await sut.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
	}

	[Fact]
	public async Task IncludeActiveKeyIdInData()
	{
		// Arrange
		var sut = CreateSut();
		var context = new HealthCheckContext();

		// Act
		var result = await sut.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result.Data.ShouldContainKey("active_key_id");
		result.Data["active_key_id"].ShouldBe("active-key-1");
	}

	[Fact]
	public async Task IncludeProviderNameInData()
	{
		// Arrange
		var sut = CreateSut();
		var context = new HealthCheckContext();

		// Act
		var result = await sut.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result.Data.ShouldContainKey("provider_name");
	}

	[Fact]
	public async Task ReportHealthToTelemetry()
	{
		// Arrange
		var sut = CreateSut();
		var context = new HealthCheckContext();

		// Act
		_ = await sut.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _telemetryDetails.UpdateProviderHealth(
				A<string>._,
				"healthy",
				100))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReportUnhealthyToTelemetry_WhenFailed()
	{
		// Arrange
		_ = A.CallTo(() => _encryptionProvider.EncryptAsync(
				A<byte[]>._,
				A<EncryptionContext>._,
				A<CancellationToken>._))
			.Throws(new EncryptionException("Test failure"));

		var sut = CreateSut();
		var context = new HealthCheckContext();

		// Act
		_ = await sut.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _telemetryDetails.UpdateProviderHealth(
				A<string>._,
				"unhealthy",
				0))
			.MustHaveHappenedOnceExactly();
	}

	private EncryptionHealthCheck CreateSut(
													IKeyManagementProvider? keyProvider = null,
		EncryptionHealthCheckOptions? options = null)
	{
		return new EncryptionHealthCheck(
			_encryptionProvider,
			keyProvider ?? _keyManagementProvider,
			_telemetry,
			_logger,
			options ?? EncryptionHealthCheckOptions.Default);
	}
}

[Trait("Category", TestCategories.Unit)]
public sealed class EncryptionHealthCheckOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = EncryptionHealthCheckOptions.Default;

		// Assert
		options.DegradedThreshold.ShouldBe(TimeSpan.FromMilliseconds(100));
		options.VerifyKeyManagement.ShouldBeTrue();
	}

	[Fact]
	public void AllowCustomization()
	{
		// Arrange & Act
		var options = new EncryptionHealthCheckOptions
		{
			DegradedThreshold = TimeSpan.FromMilliseconds(50),
			VerifyKeyManagement = false,
		};

		// Assert
		options.DegradedThreshold.ShouldBe(TimeSpan.FromMilliseconds(50));
		options.VerifyKeyManagement.ShouldBeFalse();
	}
}
