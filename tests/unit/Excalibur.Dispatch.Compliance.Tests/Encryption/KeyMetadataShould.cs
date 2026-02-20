using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class KeyMetadataShould
{
	[Fact]
	public void Store_all_required_properties()
	{
		var createdAt = DateTimeOffset.UtcNow;

		var metadata = new KeyMetadata
		{
			KeyId = "key-1",
			Version = 3,
			Status = KeyStatus.Active,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = createdAt
		};

		metadata.KeyId.ShouldBe("key-1");
		metadata.Version.ShouldBe(3);
		metadata.Status.ShouldBe(KeyStatus.Active);
		metadata.Algorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
		metadata.CreatedAt.ShouldBe(createdAt);
	}

	[Fact]
	public void Have_null_optional_properties_by_default()
	{
		var metadata = new KeyMetadata
		{
			KeyId = "key-1",
			Version = 1,
			Status = KeyStatus.Active,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = DateTimeOffset.UtcNow
		};

		metadata.ExpiresAt.ShouldBeNull();
		metadata.LastRotatedAt.ShouldBeNull();
		metadata.Purpose.ShouldBeNull();
		metadata.IsFipsCompliant.ShouldBeFalse();
	}

	[Fact]
	public void Allow_setting_optional_properties()
	{
		var expiresAt = DateTimeOffset.UtcNow.AddDays(90);
		var rotatedAt = DateTimeOffset.UtcNow.AddDays(-30);

		var metadata = new KeyMetadata
		{
			KeyId = "key-1",
			Version = 2,
			Status = KeyStatus.DecryptOnly,
			Algorithm = EncryptionAlgorithm.Aes256CbcHmac,
			CreatedAt = DateTimeOffset.UtcNow,
			ExpiresAt = expiresAt,
			LastRotatedAt = rotatedAt,
			Purpose = "field-encryption",
			IsFipsCompliant = true
		};

		metadata.ExpiresAt.ShouldBe(expiresAt);
		metadata.LastRotatedAt.ShouldBe(rotatedAt);
		metadata.Purpose.ShouldBe("field-encryption");
		metadata.IsFipsCompliant.ShouldBeTrue();
	}

	[Fact]
	public void Support_record_equality()
	{
		var createdAt = DateTimeOffset.UtcNow;

		var meta1 = new KeyMetadata
		{
			KeyId = "key-1",
			Version = 1,
			Status = KeyStatus.Active,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = createdAt
		};
		var meta2 = new KeyMetadata
		{
			KeyId = "key-1",
			Version = 1,
			Status = KeyStatus.Active,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = createdAt
		};

		meta1.ShouldBe(meta2);
	}
}

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class KeyStatusShould
{
	[Theory]
	[InlineData(KeyStatus.Active, 0)]
	[InlineData(KeyStatus.DecryptOnly, 1)]
	[InlineData(KeyStatus.PendingDestruction, 2)]
	[InlineData(KeyStatus.Destroyed, 3)]
	[InlineData(KeyStatus.Suspended, 4)]
	public void Have_expected_integer_values(KeyStatus status, int expectedValue)
	{
		((int)status).ShouldBe(expectedValue);
	}

	[Fact]
	public void Have_exactly_five_values()
	{
		Enum.GetValues<KeyStatus>().Length.ShouldBe(5);
	}
}

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptionAlgorithmShould
{
	[Theory]
	[InlineData(EncryptionAlgorithm.Aes256Gcm, 0)]
	[InlineData(EncryptionAlgorithm.Aes256CbcHmac, 1)]
	public void Have_expected_integer_values(EncryptionAlgorithm algorithm, int expectedValue)
	{
		((int)algorithm).ShouldBe(expectedValue);
	}

	[Fact]
	public void Have_exactly_two_values()
	{
		Enum.GetValues<EncryptionAlgorithm>().Length.ShouldBe(2);
	}
}
