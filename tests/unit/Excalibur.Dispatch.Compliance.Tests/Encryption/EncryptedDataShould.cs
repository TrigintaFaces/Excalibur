using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptedDataShould
{
	[Fact]
	public void Have_expected_magic_bytes()
	{
		var magic = EncryptedData.MagicBytes;

		magic.Length.ShouldBe(4);
		magic[0].ShouldBe((byte)0x45); // 'E'
		magic[1].ShouldBe((byte)0x58); // 'X'
		magic[2].ShouldBe((byte)0x43); // 'C'
		magic[3].ShouldBe((byte)0x52); // 'R'
	}

	[Fact]
	public void Detect_encrypted_data_from_span()
	{
		byte[] data = [0x45, 0x58, 0x43, 0x52, 0x01, 0x02, 0x03];

		EncryptedData.IsFieldEncrypted(data.AsSpan()).ShouldBeTrue();
	}

	[Fact]
	public void Not_detect_non_encrypted_data_from_span()
	{
		byte[] data = [0x00, 0x01, 0x02, 0x03, 0x04];

		EncryptedData.IsFieldEncrypted(data.AsSpan()).ShouldBeFalse();
	}

	[Fact]
	public void Not_detect_short_data_from_span()
	{
		byte[] data = [0x45, 0x58];

		EncryptedData.IsFieldEncrypted(data.AsSpan()).ShouldBeFalse();
	}

	[Fact]
	public void Detect_encrypted_data_from_byte_array()
	{
		byte[] data = [0x45, 0x58, 0x43, 0x52, 0xFF];

		EncryptedData.IsFieldEncrypted(data).ShouldBeTrue();
	}

	[Fact]
	public void Return_false_for_null_byte_array()
	{
		EncryptedData.IsFieldEncrypted((byte[]?)null).ShouldBeFalse();
	}

	[Fact]
	public void Store_all_required_properties()
	{
		var ciphertext = new byte[] { 1, 2, 3, 4 };
		var iv = new byte[] { 5, 6, 7 };

		var data = new EncryptedData
		{
			Ciphertext = ciphertext,
			KeyId = "key-1",
			KeyVersion = 2,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Iv = iv
		};

		data.Ciphertext.ShouldBe(ciphertext);
		data.KeyId.ShouldBe("key-1");
		data.KeyVersion.ShouldBe(2);
		data.Algorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
		data.Iv.ShouldBe(iv);
	}

	[Fact]
	public void Have_null_auth_tag_by_default()
	{
		var data = new EncryptedData
		{
			Ciphertext = [1],
			KeyId = "k",
			KeyVersion = 1,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Iv = [2]
		};

		data.AuthTag.ShouldBeNull();
	}

	[Fact]
	public void Have_null_tenant_id_by_default()
	{
		var data = new EncryptedData
		{
			Ciphertext = [1],
			KeyId = "k",
			KeyVersion = 1,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Iv = [2]
		};

		data.TenantId.ShouldBeNull();
	}
}
