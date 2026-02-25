using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptionContextShould
{
	[Fact]
	public void Have_all_null_defaults()
	{
		var context = new EncryptionContext();

		context.KeyId.ShouldBeNull();
		context.KeyVersion.ShouldBeNull();
		context.Algorithm.ShouldBeNull();
		context.TenantId.ShouldBeNull();
		context.Purpose.ShouldBeNull();
		context.AssociatedData.ShouldBeNull();
		context.Classification.ShouldBeNull();
		context.RequireFipsCompliance.ShouldBeFalse();
	}

	[Fact]
	public void Provide_default_static_instance()
	{
		var context = EncryptionContext.Default;

		context.ShouldNotBeNull();
		context.KeyId.ShouldBeNull();
		context.TenantId.ShouldBeNull();
	}

	[Fact]
	public void Create_context_for_tenant()
	{
		var context = EncryptionContext.ForTenant("tenant-42");

		context.TenantId.ShouldBe("tenant-42");
		context.KeyId.ShouldBeNull();
	}

	[Fact]
	public void Allow_setting_all_properties()
	{
		var aad = new byte[] { 1, 2, 3 };
		var context = new EncryptionContext
		{
			KeyId = "key-1",
			KeyVersion = 3,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			TenantId = "tenant-1",
			Purpose = "field-encryption",
			AssociatedData = aad,
			Classification = DataClassification.Restricted,
			RequireFipsCompliance = true
		};

		context.KeyId.ShouldBe("key-1");
		context.KeyVersion.ShouldBe(3);
		context.Algorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
		context.TenantId.ShouldBe("tenant-1");
		context.Purpose.ShouldBe("field-encryption");
		context.AssociatedData.ShouldBe(aad);
		context.Classification.ShouldBe(DataClassification.Restricted);
		context.RequireFipsCompliance.ShouldBeTrue();
	}

	[Fact]
	public void Support_record_equality()
	{
		var context1 = new EncryptionContext { KeyId = "key-1", TenantId = "t1" };
		var context2 = new EncryptionContext { KeyId = "key-1", TenantId = "t1" };

		context1.ShouldBe(context2);
	}
}
