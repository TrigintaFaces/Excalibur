using Microsoft.Extensions.Logging.Abstractions;

using Excalibur.Compliance.Encryption;

using Excalibur.Compliance;namespace Excalibur.Compliance.Tests.Encryption;

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
	public async Task RefuseToTakeTheTenantFromAByteArrayEnvelopeToo()
	{
		// THE CARRIER TYPE WAS NEVER THE BOUNDARY, and an earlier version of this guard assumed it was.
		// It refused a string-carried field on the reasoning that a varchar column is consumer-writable
		// while opaque bytes are not. A varbinary column is exactly as writable as a varchar one by anyone
		// with write access to the row, so the threat model is type-agnostic: the string carrier widened
		// the affected COLUMN POPULATION, it did not create the hole and it never bounded it.
		//
		// This arm is the one that fails against the carrier-conditional form of the guard, which is why
		// it exists separately from its string sibling rather than as a theory alongside it.
		var envelope = new EncryptedData
		{
			Ciphertext = [1, 2, 3],
			KeyId = "victim-tenant-key",
			KeyVersion = 1,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Iv = [11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22],
			TenantId = "a-tenant-the-writer-does-not-own",
		};

		var framed = new byte[EncryptedData.MagicBytes.Length + 2];
		EncryptedData.MagicBytes.CopyTo(framed);
		var entity = new EntityWithByteArrayEncryptedField { SecretData = framed };

		var result = await _sut.ReEncryptAsync(entity, new ReEncryptionContext(), CancellationToken.None)
			.ConfigureAwait(false);

		result.Success.ShouldBeFalse(
			"a byte[] envelope must not be allowed to name the key and tenant it is re-encrypted under");
		result.ErrorMessage.ShouldContain("explicit EncryptionContext");
		result.FieldsReEncrypted.ShouldBe(0);

		// Refusing must not partially rewrite the field: the stored bytes are handed back untouched.
		entity.SecretData.ShouldBe(framed);
		_ = envelope;
	}

	[Fact]
	public async Task StillReEncryptAByteArrayFieldWhenTheCallerNamesTheTenant()
	{
		// LIVENESS, and it is the arm that separates this fix from an over-fix. A guard that refuses every
		// byte[] re-encryption satisfies the safety arm above and breaks the feature: the supported path -
		// a caller who supplies the context - must still reach the provider. Asserted through the SAME
		// surface, so a refusal that leaked into the supported path would be visible here as a failure
		// rather than hidden behind a different code path.
		var entity = new EntityWithByteArrayEncryptedField { SecretData = null };

		var context = new ReEncryptionContext
		{
			EncryptionContext = new EncryptionContext
			{
				KeyId = "caller-supplied-key",
				KeyVersion = 1,
				TenantId = "the-tenant-the-caller-names",
			},
		};

		var result = await _sut.ReEncryptAsync(entity, context, CancellationToken.None).ConfigureAwait(false);

		result.Success.ShouldBeTrue(
			"supplying an explicit EncryptionContext is the supported path and must not be refused");
	}

	[Fact]
	public async Task RefuseToTakeTheTenantFromAConsumerWritableStringEnvelope()
	{
		// A string column is consumer-writable, so every byte after the marker is attacker-chosen. Letting
		// the stored value name its own KeyId/TenantId would let a writer steer key selection and tenant
		// attribution to a tenant it has no claim to. Where the provider is authenticated encryption the
		// decryption then fails at the tag, but IEncryptionProvider only says implementations SHOULD use
		// AES-256-GCM - so against an unauthenticated provider a steered key fetch can disclose plaintext.
		// The key lookup happens FIRST regardless, so the identity must not come from the value at all.
		var envelope = new EncryptedData
		{
			Ciphertext = [1, 2, 3],
			KeyId = "victim-tenant-key",
			KeyVersion = 1,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Iv = [11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22],
			TenantId = "a-tenant-the-writer-does-not-own",
		};
		var framed = new byte[EncryptedData.MagicBytes.Length + 2];
		EncryptedData.MagicBytes.CopyTo(framed);
		var entity = new EntityWithStringEncryptedField
		{
			Secret = EncryptedFieldBinding.StringEnvelopePrefix + Convert.ToBase64String(framed),
		};

		// No EncryptionContext supplied: the only source of tenant identity would be the stored value.
		// This API reports failure as a RESULT rather than an exception, so fail-closed means Success=false
		// and the field left untouched - not a throw.
		var result = await _sut.ReEncryptAsync(entity, new ReEncryptionContext(), CancellationToken.None)
			.ConfigureAwait(false);

		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("explicit EncryptionContext");
		result.FieldsReEncrypted.ShouldBe(0);

		// And the stored value is untouched - refusing must not partially rewrite the field.
		entity.Secret.ShouldStartWith(EncryptedFieldBinding.StringEnvelopePrefix);
		_ = envelope;
	}

	[Fact]
	public async Task EstimateReportsAnUnhonourableAnnotationInsteadOfRefusing()
	{
		// An ESTIMATE reports; it does not refuse. The decisive case is a type carrying ONE GOOD annotation
		// beside one that cannot be honoured: refusing would answer "zero fields" for a type that really has
		// one, which is a confidently wrong number rather than a failure.
		var estimate = await _sut.EstimateForTypeAsync<MixedAnnotationEntity>(100, CancellationToken.None)
			.ConfigureAwait(false);

		estimate.EstimatedFieldsPerItem.ShouldBe(1);
		estimate.Warnings.ShouldContain(w => w.Contains("ReadOnlySecret", StringComparison.Ordinal));
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
		result.ErrorMessage!.ShouldBeNull();
		result.Exception!.ShouldBeNull();
	}

	[Fact]
	public void Create_failed_result_via_factory()
	{
		var ex = new InvalidOperationException("test error");
		var result = ReEncryptionResult.Failed("test error", ex);
		result.Success.ShouldBeFalse();
		result.ErrorMessage!.ShouldBe("test error");
		result.Exception!.ShouldBeSameAs(ex);
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
		result.ErrorMessage!.ShouldBe("oops");
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

/// <summary>
/// A projection whose encrypted field is carried by a byte[] column. Declared beside its string sibling on
/// purpose: the pair is what makes the guard's carrier-independence observable rather than asserted.
/// </summary>
public sealed class EntityWithByteArrayEncryptedField
{
	[EncryptedField]
	public byte[]? SecretData { get; set; }
}

/// <summary>A projection whose encrypted field is carried by a consumer-writable string column.</summary>
public sealed class EntityWithStringEncryptedField
{
	[EncryptedField]
	public string? Secret { get; set; }
}

/// <summary>
/// One honourable [EncryptedField] beside one that is not. The unhonourable property models the case that
/// decides the decrypt contract: a property that HAD a setter, stored ciphertext under it, and lost the
/// setter in a later release still holds that ciphertext.
/// </summary>
public sealed class MixedAnnotationEntity
{
	[EncryptedField]
	public byte[]? Encryptable { get; set; }

	[EncryptedField]
	public string ReadOnlySecret => "EXCR1:not-writable";
}
