// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Encryption;

/// <summary>
/// Unit tests for <see cref="ReEncryptionService"/>.
/// Tests re-encryption, batch processing, and estimation per AD-256-1.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
public sealed class ReEncryptionServiceShould
{
	private readonly IEncryptionProviderRegistry _registry;
	private readonly ReEncryptionService _sut;

	public ReEncryptionServiceShould()
	{
		_registry = A.Fake<IEncryptionProviderRegistry>();
		_sut = new ReEncryptionService(
			_registry,
			NullLogger<ReEncryptionService>.Instance);
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenRegistryIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() => new ReEncryptionService(
			null!,
			NullLogger<ReEncryptionService>.Instance));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() => new ReEncryptionService(
			_registry,
			null!));
	}

	#endregion

	#region ReEncryptAsync Tests

	[Fact]
	public async Task ReEncryptAsync_ThrowsArgumentNullException_WhenEntityIsNull()
	{
		// Arrange
		var context = new ReEncryptionContext();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.ReEncryptAsync<TestEntityNoEncryption>(null!, context, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ReEncryptAsync_ThrowsArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		var entity = new TestEntityNoEncryption();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.ReEncryptAsync(entity, null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ReEncryptAsync_ReturnsSuccessWithZeroFields_WhenEntityHasNoEncryptedProperties()
	{
		// Arrange
		var entity = new TestEntityNoEncryption { Name = "test" };
		var context = new ReEncryptionContext();

		// Act
		var result = await _sut.ReEncryptAsync(entity, context, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();
		result.FieldsReEncrypted.ShouldBe(0);
	}

	[Fact]
	public async Task ReEncryptAsync_SkipsNullEncryptedFields()
	{
		// Arrange
		var entity = new TestEntityWithEncryptedField { Data = null };
		var context = new ReEncryptionContext();

		// Act
		var result = await _sut.ReEncryptAsync(entity, context, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();
		result.FieldsReEncrypted.ShouldBe(0);
	}

	[Fact]
	public async Task ReEncryptAsync_SkipsEmptyEncryptedFields()
	{
		// Arrange
		var entity = new TestEntityWithEncryptedField { Data = Array.Empty<byte>() };
		var context = new ReEncryptionContext();

		// Act
		var result = await _sut.ReEncryptAsync(entity, context, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();
		result.FieldsReEncrypted.ShouldBe(0);
	}

	[Fact]
	public async Task ReEncryptAsync_SkipsNonEncryptedData()
	{
		// Arrange - data without the magic bytes header
		var entity = new TestEntityWithEncryptedField { Data = new byte[] { 1, 2, 3, 4, 5 } };
		var context = new ReEncryptionContext();

		// Act
		var result = await _sut.ReEncryptAsync(entity, context, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();
		result.FieldsReEncrypted.ShouldBe(0);
	}

	#endregion

	#region ReEncryptBatchAsync Tests

	[Fact]
	public async Task ReEncryptBatchAsync_ThrowsArgumentNullException_WhenEntitiesIsNull()
	{
		// Arrange
		var options = new ReEncryptionOptions();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
		{
			await foreach (var _ in _sut.ReEncryptBatchAsync<TestEntityNoEncryption>(null!, options, CancellationToken.None).ConfigureAwait(false))
			{
				// consume
			}
		}).ConfigureAwait(false);
	}

	[Fact]
	public async Task ReEncryptBatchAsync_ThrowsArgumentNullException_WhenOptionsIsNull()
	{
		// Arrange
		var source = ToAsyncEnumerable(Array.Empty<TestEntityNoEncryption>());

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
		{
			await foreach (var _ in _sut.ReEncryptBatchAsync(source, null!, CancellationToken.None).ConfigureAwait(false))
			{
				// consume
			}
		}).ConfigureAwait(false);
	}

	[Fact]
	public async Task ReEncryptBatchAsync_ProcessesEntitiesWithNoEncryptedFields()
	{
		// Arrange
		var entities = new[]
		{
			new TestEntityNoEncryption { Name = "entity1" },
			new TestEntityNoEncryption { Name = "entity2" }
		};
		var source = ToAsyncEnumerable(entities);
		var options = new ReEncryptionOptions { BatchSize = 10 };

		// Act
		var results = new List<ReEncryptionResult<TestEntityNoEncryption>>();
		await foreach (var result in _sut.ReEncryptBatchAsync(source, options, CancellationToken.None).ConfigureAwait(false))
		{
			results.Add(result);
		}

		// Assert
		results.Count.ShouldBe(2);
		results.ShouldAllBe(r => r.Success);
	}

	#endregion

	#region EstimateAsync Tests

	[Fact]
	public async Task EstimateAsync_ThrowsArgumentNullException_WhenOptionsIsNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.EstimateAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task EstimateAsync_ReturnsEstimateWithWarnings_WhenNoTypeSpecified()
	{
		// Arrange
		var options = new ReEncryptionOptions();

		// Act
		var estimate = await _sut.EstimateAsync(options, CancellationToken.None).ConfigureAwait(false);

		// Assert
		estimate.ShouldNotBeNull();
		estimate.EstimatedItemCount.ShouldBe(0);
		estimate.EstimatedFieldsPerItem.ShouldBe(0);
		estimate.EstimatedDuration.ShouldBe(TimeSpan.Zero);
		estimate.IsSampled.ShouldBeFalse();
		estimate.Warnings.ShouldNotBeEmpty();
	}

	#endregion

	#region EstimateForTypeAsync Tests

	[Fact]
	public async Task EstimateForTypeAsync_ThrowsArgumentOutOfRangeException_WhenItemCountIsNegative()
	{
		await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => _sut.EstimateForTypeAsync<TestEntityNoEncryption>(-1, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task EstimateForTypeAsync_ReturnsZeroDuration_WhenEntityHasNoEncryptedFields()
	{
		// Act
		var estimate = await _sut.EstimateForTypeAsync<TestEntityNoEncryption>(100, CancellationToken.None).ConfigureAwait(false);

		// Assert
		estimate.EstimatedItemCount.ShouldBe(100);
		estimate.EstimatedFieldsPerItem.ShouldBe(0);
		estimate.EstimatedDuration.ShouldBe(TimeSpan.Zero);
		estimate.Warnings.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task EstimateForTypeAsync_ReturnsNonZeroDuration_WhenEntityHasEncryptedFields()
	{
		// Act
		var estimate = await _sut.EstimateForTypeAsync<TestEntityWithEncryptedField>(1000, CancellationToken.None).ConfigureAwait(false);

		// Assert
		estimate.EstimatedItemCount.ShouldBe(1000);
		estimate.EstimatedFieldsPerItem.ShouldBeGreaterThan(0);
		estimate.EstimatedDuration.ShouldBeGreaterThan(TimeSpan.Zero);
		estimate.IsSampled.ShouldBeFalse();
		estimate.Warnings.ShouldBeEmpty();
	}

	[Fact]
	public async Task EstimateForTypeAsync_ThrowsOperationCanceledException_WhenCancelled()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => _sut.EstimateForTypeAsync<TestEntityWithEncryptedField>(100, cts.Token)).ConfigureAwait(false);
	}

	[Fact]
	public async Task EstimateForTypeAsync_ReturnsZeroDuration_WhenItemCountIsZero()
	{
		// Act
		var estimate = await _sut.EstimateForTypeAsync<TestEntityWithEncryptedField>(0, CancellationToken.None).ConfigureAwait(false);

		// Assert
		estimate.EstimatedItemCount.ShouldBe(0);
		estimate.EstimatedDuration.ShouldBe(TimeSpan.Zero);
	}

	#endregion

	#region Helpers

	private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> items,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		foreach (var item in items)
		{
			cancellationToken.ThrowIfCancellationRequested();
			yield return item;
			await Task.CompletedTask.ConfigureAwait(false);
		}
	}

	private sealed class TestEntityNoEncryption
	{
		public string Name { get; set; } = string.Empty;
	}

	private sealed class TestEntityWithEncryptedField
	{
		[EncryptedField]
		public byte[]? Data { get; set; }
	}

	#endregion
}
