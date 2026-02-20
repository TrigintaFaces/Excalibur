using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class KeyRotationResultShould
{
	[Fact]
	public void Create_successful_result_with_new_key()
	{
		var newKey = new KeyMetadata
		{
			KeyId = "key-2",
			Version = 2,
			Status = KeyStatus.Active,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = DateTimeOffset.UtcNow
		};

		var result = KeyRotationResult.Succeeded(newKey);

		result.Success.ShouldBeTrue();
		result.NewKey.ShouldBeSameAs(newKey);
		result.PreviousKey.ShouldBeNull();
		result.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void Create_successful_result_with_previous_key()
	{
		var newKey = new KeyMetadata
		{
			KeyId = "key-2",
			Version = 2,
			Status = KeyStatus.Active,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = DateTimeOffset.UtcNow
		};
		var previousKey = new KeyMetadata
		{
			KeyId = "key-1",
			Version = 1,
			Status = KeyStatus.DecryptOnly,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			CreatedAt = DateTimeOffset.UtcNow.AddDays(-90)
		};

		var result = KeyRotationResult.Succeeded(newKey, previousKey);

		result.Success.ShouldBeTrue();
		result.NewKey.ShouldBeSameAs(newKey);
		result.PreviousKey.ShouldBeSameAs(previousKey);
	}

	[Fact]
	public void Create_failed_result()
	{
		var result = KeyRotationResult.Failed("Provider unavailable");

		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldBe("Provider unavailable");
		result.NewKey.ShouldBeNull();
		result.PreviousKey.ShouldBeNull();
	}
}
