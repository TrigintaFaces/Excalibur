// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Excalibur.Dispatch.Security;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Excalibur.Dispatch.Compliance;
namespace Excalibur.Dispatch.Integration.Tests.Compliance.Encryption;

/// <summary>
/// Integration tests for encryption estimation services.
/// </summary>
/// <remarks>
/// Per Sprint 397, these tests verify estimation functionality works correctly
/// when services are wired up through dependency injection with realistic scenarios.
/// </remarks>
[Trait("Category", TestCategories.Integration)]
[Trait("Component", "Compliance")]
public sealed class EncryptionEstimationIntegrationShould : IDisposable
{
	private readonly ServiceProvider _serviceProvider;
	private readonly IReEncryptionService _reEncryptionService;
	private readonly IEncryptionMigrationService _migrationService;

	public EncryptionEstimationIntegrationShould()
	{
		var services = new ServiceCollection();

		// Add logging
		_ = services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
		_ = services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

		// Add encryption services with mock provider
		var mockProvider = CreateMockEncryptionProvider();
		_ = services.AddSingleton(mockProvider);

		// Add encryption provider registry
		var mockRegistry = CreateMockEncryptionRegistry(mockProvider);
		_ = services.AddSingleton(mockRegistry);

		// Add services under test
		_ = services.AddSingleton<IReEncryptionService, ReEncryptionService>();
		_ = services.AddSingleton<IEncryptionMigrationService, EncryptionMigrationService>();

		_serviceProvider = services.BuildServiceProvider();
		_reEncryptionService = _serviceProvider.GetRequiredService<IReEncryptionService>();
		_migrationService = _serviceProvider.GetRequiredService<IEncryptionMigrationService>();
	}

	public void Dispose()
	{
		_serviceProvider.Dispose();
	}

	#region ReEncryptionService Integration Tests

	[Fact]
	public async Task ReEncryptionService_ResolveFromDI_Successfully()
	{
		// Assert
		_ = _reEncryptionService.ShouldNotBeNull();
		_ = _reEncryptionService.ShouldBeOfType<ReEncryptionService>();
	}

	[Fact]
	public async Task ReEncryptionService_EstimateAsync_ReturnGuidanceWithoutEntityType()
	{
		// Arrange
		var options = new ReEncryptionOptions
		{
			SourceProviderId = "source-provider",
			TargetProviderId = "target-provider",
		};

		// Act
		var result = await _reEncryptionService.EstimateAsync(options, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		_ = result.Warnings.ShouldNotBeNull();
		result.Warnings.Count.ShouldBeGreaterThan(0);
		result.Warnings.ShouldContain(w => w.Contains("EstimateForTypeAsync"));
	}

	[Fact]
	public async Task ReEncryptionService_EstimateForType_CalculateCorrectly()
	{
		// Arrange - Cast to concrete type to access the typed method
		var service = (ReEncryptionService)_reEncryptionService;
		const long itemCount = 10_000;

		// Act
		var result = await service.EstimateForTypeAsync<SampleEncryptedEntity>(
			itemCount, CancellationToken.None);

		// Assert
		result.EstimatedItemCount.ShouldBe(itemCount);
		result.EstimatedFieldsPerItem.ShouldBe(2); // SampleEncryptedEntity has 2 encrypted fields
		result.EstimatedDuration.ShouldBeGreaterThan(TimeSpan.Zero);
		result.IsSampled.ShouldBeFalse();
		result.Warnings.ShouldBeEmpty();
	}

	[Fact]
	public async Task ReEncryptionService_EstimateForType_HandleCancellation()
	{
		// Arrange
		var service = (ReEncryptionService)_reEncryptionService;
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(
			() => service.EstimateForTypeAsync<SampleEncryptedEntity>(100, cts.Token));
	}

	[Fact]
	public async Task ReEncryptionService_EstimateForType_DetectNoEncryptedFields()
	{
		// Arrange
		var service = (ReEncryptionService)_reEncryptionService;

		// Act
		var result = await service.EstimateForTypeAsync<SampleNonEncryptedEntity>(
			100, CancellationToken.None);

		// Assert
		result.EstimatedFieldsPerItem.ShouldBe(0);
		result.Warnings.ShouldContain(w => w.Contains("No encrypted fields"));
	}

	[Theory]
	[InlineData(100)]
	[InlineData(10_000)]
	[InlineData(1_000_000)]
	public async Task ReEncryptionService_EstimateForType_ScaleLinearlyWithItemCount(long itemCount)
	{
		// Arrange
		var service = (ReEncryptionService)_reEncryptionService;

		// Act
		var result = await service.EstimateForTypeAsync<SampleEncryptedEntity>(
			itemCount, CancellationToken.None);

		// Assert - Verify formula: itemCount × fields × 5ms × 1.2
		var expectedMs = itemCount * 2 * 5.0 * 1.2; // 2 encrypted fields
		result.EstimatedDuration.TotalMilliseconds.ShouldBe(expectedMs);
	}

	#endregion

	#region EncryptionMigrationService Integration Tests

	[Fact]
	public async Task MigrationService_ResolveFromDI_Successfully()
	{
		// Assert
		_ = _migrationService.ShouldNotBeNull();
		_ = _migrationService.ShouldBeOfType<EncryptionMigrationService>();
	}

	[Fact]
	public async Task MigrationService_EstimateAsync_ReturnGuidanceWithoutDataSource()
	{
		// Arrange
		var policy = MigrationPolicy.Default;

		// Act
		var result = await _migrationService.EstimateMigrationAsync(policy, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.EstimatedItemCount.ShouldBe(0);
		_ = result.Warnings.ShouldNotBeNull();
		result.Warnings.ShouldContain(w => w.Contains("EstimateMigrationAsync"));
	}

	[Fact]
	public async Task MigrationService_EstimateTyped_CalculateCorrectly()
	{
		// Arrange
		var service = (EncryptionMigrationService)_migrationService;
		var policy = MigrationPolicy.Default;
		const int itemCount = 5000;
		const long dataSizeBytes = 50 * 1024 * 1024; // 50MB

		// Act
		var result = await service.EstimateMigrationAsync(policy, itemCount, dataSizeBytes, CancellationToken.None);

		// Assert
		result.EstimatedItemCount.ShouldBe(itemCount);
		result.EstimatedDataSizeBytes.ShouldBe(dataSizeBytes);
		result.EstimatedDuration.ShouldBeGreaterThan(TimeSpan.Zero);
		result.EstimatedAt.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
	}

	[Fact]
	public async Task MigrationService_EstimateTyped_HandleZeroItems()
	{
		// Arrange
		var service = (EncryptionMigrationService)_migrationService;
		var policy = MigrationPolicy.Default;

		// Act
		var result = await service.EstimateMigrationAsync(policy, 0, 0, CancellationToken.None);

		// Assert
		result.EstimatedItemCount.ShouldBe(0);
		result.EstimatedDuration.ShouldBe(TimeSpan.Zero);
		result.Warnings.ShouldContain("No items to migrate");
	}

	[Fact]
	public async Task MigrationService_EstimateTyped_WarnForShortMaxKeyAge()
	{
		// Arrange
		var service = (EncryptionMigrationService)_migrationService;
		var policy = new MigrationPolicy { MaxKeyAge = TimeSpan.FromDays(15) };

		// Act
		var result = await service.EstimateMigrationAsync(policy, 100, 1024, CancellationToken.None);

		// Assert
		_ = result.Warnings.ShouldNotBeNull();
		result.Warnings.ShouldContain(w => w.Contains("frequent migrations"));
	}

	[Fact]
	public async Task MigrationService_EstimateTyped_WarnForLongDuration()
	{
		// Arrange
		var service = (EncryptionMigrationService)_migrationService;
		var policy = MigrationPolicy.Default;
		const int largeItemCount = 5_000_000; // Large enough to exceed 1 hour

		// Act
		var result = await service.EstimateMigrationAsync(policy, largeItemCount, 0, CancellationToken.None);

		// Assert
		_ = result.Warnings.ShouldNotBeNull();
		result.Warnings.ShouldContain(w => w.Contains("1 hour"));
	}

	[Theory]
	[InlineData(1000, 0)]
	[InlineData(1000, 1024 * 1024 * 100)] // 100MB
	[InlineData(100_000, 1024 * 1024 * 1024)] // 1GB
	public async Task MigrationService_EstimateTyped_ScaleWithInputs(int itemCount, long dataSizeBytes)
	{
		// Arrange
		var service = (EncryptionMigrationService)_migrationService;
		var policy = MigrationPolicy.Default;

		// Act
		var result = await service.EstimateMigrationAsync(policy, itemCount, dataSizeBytes, CancellationToken.None);

		// Assert
		result.EstimatedItemCount.ShouldBe(itemCount);
		result.EstimatedDataSizeBytes.ShouldBe(dataSizeBytes);

		// Verify formula: (itemCount × 3ms + dataSizeBytes / 100MB/s) × 1.2
		const double msPerItem = 3.0;
		const double ioOverhead = 1.2;
		const long ioBytesPerSecond = 100 * 1024 * 1024;
		var encryptionMs = itemCount * msPerItem;
		var ioMs = dataSizeBytes / (double)ioBytesPerSecond * 1000;
		var expectedMs = (encryptionMs + ioMs) * ioOverhead;
		result.EstimatedDuration.TotalMilliseconds.ShouldBe(expectedMs);
	}

	#endregion

	#region Realistic Scenario Tests

	[Fact]
	public async Task EstimateReEncryption_ForSmallDatabase_ReturnReasonableEstimate()
	{
		// Scenario: Small database with 10,000 encrypted records, 2 fields each
		var service = (ReEncryptionService)_reEncryptionService;
		const long recordCount = 10_000;

		// Act
		var result = await service.EstimateForTypeAsync<SampleEncryptedEntity>(
			recordCount, CancellationToken.None);

		// Assert - Should complete in ~2 minutes
		// 10,000 × 2 fields × 5ms × 1.2 = 120,000ms = 2 minutes
		result.EstimatedDuration.TotalMinutes.ShouldBeLessThanOrEqualTo(3);
		result.EstimatedDuration.TotalSeconds.ShouldBeGreaterThan(100);
	}

	[Fact]
	public async Task EstimateReEncryption_ForMediumDatabase_ReturnReasonableEstimate()
	{
		// Scenario: Medium database with 1M encrypted records
		var service = (ReEncryptionService)_reEncryptionService;
		const long recordCount = 1_000_000;

		// Act
		var result = await service.EstimateForTypeAsync<SampleEncryptedEntity>(
			recordCount, CancellationToken.None);

		// Assert - Should complete in ~3.3 hours
		// 1M × 2 fields × 5ms × 1.2 = 12,000,000ms = 200 minutes = 3.3 hours
		result.EstimatedDuration.TotalHours.ShouldBeGreaterThan(3);
		result.EstimatedDuration.TotalHours.ShouldBeLessThan(4);
	}

	[Fact]
	public async Task EstimateMigration_ForKeyRotation_ReturnReasonableEstimate()
	{
		// Scenario: Key rotation for 50,000 records with 10GB of data
		var service = (EncryptionMigrationService)_migrationService;
		var policy = MigrationPolicy.Default;
		const int recordCount = 50_000;
		const long dataSizeBytes = 10L * 1024 * 1024 * 1024; // 10GB

		// Act
		var result = await service.EstimateMigrationAsync(policy, recordCount, dataSizeBytes, CancellationToken.None);

		// Assert
		result.EstimatedItemCount.ShouldBe(recordCount);
		result.EstimatedDataSizeBytes.ShouldBe(dataSizeBytes);
		result.EstimatedDuration.ShouldBeGreaterThan(TimeSpan.FromMinutes(1));
		// Formula: (50,000 × 3ms + 10GB/100MB/s) × 1.2 = (150,000ms + 102,400ms) × 1.2 = ~5 minutes
		// No warnings expected for this relatively quick operation
		result.Warnings.ShouldBeNull();
	}

	[Fact]
	public async Task EstimateMigration_ForCompliance_WithStrictPolicy()
	{
		// Scenario: Compliance-driven migration with aggressive key rotation
		var service = (EncryptionMigrationService)_migrationService;
		var policy = new MigrationPolicy
		{
			MaxKeyAge = TimeSpan.FromDays(7), // Very aggressive rotation
			DeprecatedAlgorithms = new HashSet<EncryptionAlgorithm> { EncryptionAlgorithm.Aes256CbcHmac },
		};

		// Act
		var result = await service.EstimateMigrationAsync(policy, 1000, 1024 * 1024, CancellationToken.None);

		// Assert - Should warn about frequent migrations
		_ = result.Warnings.ShouldNotBeNull();
		result.Warnings.ShouldContain(w => w.Contains("frequent migrations"));
	}

	#endregion

	#region Helper Classes and Methods

	/// <summary>
	/// Sample entity with encrypted fields for testing.
	/// </summary>
	private sealed class SampleEncryptedEntity
	{
		public string Id { get; set; } = string.Empty;

		[EncryptedField]
		public byte[]? SensitiveField1 { get; set; }

		[EncryptedField]
		public byte[]? SensitiveField2 { get; set; }

		public string? NonEncryptedField { get; set; }
	}

	/// <summary>
	/// Sample entity without encrypted fields.
	/// </summary>
	private sealed class SampleNonEncryptedEntity
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public byte[]? Data { get; set; } // Not marked with [EncryptedField]
	}

	private static IEncryptionProvider CreateMockEncryptionProvider()
	{
		var provider = FakeItEasy.A.Fake<IEncryptionProvider>();

		_ = FakeItEasy.A.CallTo(() => provider.EncryptAsync(
				FakeItEasy.A<byte[]>.Ignored,
				FakeItEasy.A<EncryptionContext>.Ignored,
				FakeItEasy.A<CancellationToken>.Ignored))
			.ReturnsLazily((byte[] data, EncryptionContext _, CancellationToken _) =>
			{
				return Task.FromResult(new EncryptedData
				{
					Ciphertext = data,
					Iv = new byte[12],
					AuthTag = new byte[16],
					Algorithm = EncryptionAlgorithm.Aes256Gcm,
					KeyId = "test-key",
					KeyVersion = 1,
					EncryptedAt = DateTimeOffset.UtcNow
				});
			});

		_ = FakeItEasy.A.CallTo(() => provider.DecryptAsync(
				FakeItEasy.A<EncryptedData>.Ignored,
				FakeItEasy.A<EncryptionContext>.Ignored,
				FakeItEasy.A<CancellationToken>.Ignored))
			.ReturnsLazily(call =>
			{
				var data = call.GetArgument<EncryptedData>(0);
				return Task.FromResult(data.Ciphertext);
			});

		return provider;
	}

	private static IEncryptionProviderRegistry CreateMockEncryptionRegistry(IEncryptionProvider provider)
	{
		var registry = FakeItEasy.A.Fake<IEncryptionProviderRegistry>();

		_ = FakeItEasy.A.CallTo(() => registry.GetProvider(FakeItEasy.A<string>.Ignored))
			.Returns(provider);

		_ = FakeItEasy.A.CallTo(() => registry.GetPrimary())
			.Returns(provider);

		_ = FakeItEasy.A.CallTo(() => registry.FindDecryptionProvider(FakeItEasy.A<EncryptedData>.Ignored))
			.Returns(provider);

		return registry;
	}

	#endregion
}
