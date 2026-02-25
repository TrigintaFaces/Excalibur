using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ReEncryptionServiceShould
{
	private readonly IEncryptionProviderRegistry _registry = A.Fake<IEncryptionProviderRegistry>();
	private readonly ReEncryptionService _sut;

	public ReEncryptionServiceShould()
	{
		_sut = new ReEncryptionService(
			_registry,
			NullLogger<ReEncryptionService>.Instance);
	}

	[Fact]
	public async Task Return_success_with_zero_fields_for_entity_without_encrypted_fields()
	{
		// Arrange
		var entity = new EntityWithoutEncryptedFields { Name = "test" };
		var context = new ReEncryptionContext();

		// Act
		var result = await _sut.ReEncryptAsync(entity, context, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Success.ShouldBeTrue();
		result.FieldsReEncrypted.ShouldBe(0);
	}

	[Fact]
	public async Task Throw_on_null_entity()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.ReEncryptAsync<EntityWithoutEncryptedFields>(null!, new ReEncryptionContext(), CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_on_null_context()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.ReEncryptAsync(new EntityWithoutEncryptedFields(), null!, CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Estimate_with_zero_counts_when_no_type_specified()
	{
		// Arrange
		var options = new ReEncryptionOptions();

		// Act
		var estimate = await _sut.EstimateAsync(options, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		estimate.EstimatedItemCount.ShouldBe(0);
		estimate.EstimatedFieldsPerItem.ShouldBe(0);
		estimate.EstimatedDuration.ShouldBe(TimeSpan.Zero);
		estimate.Warnings.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task Throw_on_null_options_for_estimate()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.EstimateAsync(null!, CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Estimate_for_type_with_no_encrypted_fields()
	{
		// Act
		var estimate = await _sut.EstimateForTypeAsync<EntityWithoutEncryptedFields>(100, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		estimate.EstimatedItemCount.ShouldBe(100);
		estimate.EstimatedFieldsPerItem.ShouldBe(0);
		estimate.EstimatedDuration.ShouldBe(TimeSpan.Zero);
		estimate.Warnings.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task Estimate_for_type_with_encrypted_fields()
	{
		// Act
		var estimate = await _sut.EstimateForTypeAsync<EntityWithEncryptedField>(1000, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		estimate.EstimatedItemCount.ShouldBe(1000);
		estimate.EstimatedFieldsPerItem.ShouldBe(1);
		estimate.EstimatedDuration.ShouldBeGreaterThan(TimeSpan.Zero);
		estimate.Warnings.ShouldBeEmpty();
	}

	[Fact]
	public async Task Throw_for_negative_item_count()
	{
		await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => _sut.EstimateForTypeAsync<EntityWithEncryptedField>(-1, CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public void Throw_for_null_registry()
	{
		Should.Throw<ArgumentNullException>(
			() => new ReEncryptionService(null!, NullLogger<ReEncryptionService>.Instance));
	}

	[Fact]
	public void Throw_for_null_logger()
	{
		Should.Throw<ArgumentNullException>(
			() => new ReEncryptionService(_registry, null!));
	}

	[Fact]
	public void Have_default_batch_size_of_100()
	{
		var options = new ReEncryptionOptions();
		options.BatchSize.ShouldBe(100);
	}

	[Fact]
	public void Have_default_parallelism_of_4()
	{
		var options = new ReEncryptionOptions();
		options.MaxDegreeOfParallelism.ShouldBe(4);
	}

	[Fact]
	public void Have_default_continue_on_error_false()
	{
		var options = new ReEncryptionOptions();
		options.ContinueOnError.ShouldBeFalse();
	}

	[Fact]
	public void Have_default_verify_before_reencrypt_true()
	{
		var options = new ReEncryptionOptions();
		options.VerifyBeforeReEncrypt.ShouldBeTrue();
	}

	[Fact]
	public void Have_default_item_timeout_of_30_seconds()
	{
		var options = new ReEncryptionOptions();
		options.ItemTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void Have_context_defaults()
	{
		var context = new ReEncryptionContext();
		context.SourceProviderId.ShouldBeNull();
		context.TargetProviderId.ShouldBeNull();
		context.EncryptionContext.ShouldBeNull();
		context.VerifyBeforeReEncrypt.ShouldBeTrue();
	}

	[Fact]
	public void Create_successful_result_via_factory()
	{
		var result = ReEncryptionResult.Succeeded("src", "tgt", 5, TimeSpan.FromSeconds(1));
		result.Success.ShouldBeTrue();
		result.SourceProviderId.ShouldBe("src");
		result.TargetProviderId.ShouldBe("tgt");
		result.FieldsReEncrypted.ShouldBe(5);
		result.Duration.ShouldBe(TimeSpan.FromSeconds(1));
		result.ErrorMessage.ShouldBeNull();
		result.Exception.ShouldBeNull();
	}

	[Fact]
	public void Create_failed_result_via_factory()
	{
		var ex = new InvalidOperationException("test error");
		var result = ReEncryptionResult.Failed("test error", ex);
		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldBe("test error");
		result.Exception.ShouldBeSameAs(ex);
	}

	[Fact]
	public void Create_successful_typed_result_via_factory()
	{
		var entity = new EntityWithoutEncryptedFields { Name = "x" };
		var result = ReEncryptionResult<EntityWithoutEncryptedFields>.Succeeded(
			entity, "src", "tgt", 1, TimeSpan.FromMilliseconds(50));
		result.Success.ShouldBeTrue();
		result.Entity.ShouldBeSameAs(entity);
		result.SourceProviderId.ShouldBe("src");
		result.TargetProviderId.ShouldBe("tgt");
		result.FieldsReEncrypted.ShouldBe(1);
	}

	[Fact]
	public void Create_failed_typed_result_via_factory()
	{
		var entity = new EntityWithoutEncryptedFields { Name = "x" };
		var result = ReEncryptionResult<EntityWithoutEncryptedFields>.Failed(entity, "oops");
		result.Success.ShouldBeFalse();
		result.Entity.ShouldBeSameAs(entity);
		result.ErrorMessage.ShouldBe("oops");
	}

	[Fact]
	public void Create_estimate_with_defaults()
	{
		var estimate = new ReEncryptionEstimate();
		estimate.EstimatedItemCount.ShouldBe(0);
		estimate.EstimatedFieldsPerItem.ShouldBe(0);
		estimate.EstimatedDuration.ShouldBe(TimeSpan.Zero);
		estimate.Warnings.ShouldBeEmpty();
		estimate.IsSampled.ShouldBeFalse();
	}

	// Test helper entity types
	private sealed class EntityWithoutEncryptedFields
	{
		public string Name { get; set; } = "";
	}

	private sealed class EntityWithEncryptedField
	{
		public string Name { get; set; } = "";

		[EncryptedField]
		public byte[]? SecretData { get; set; }
	}
}
