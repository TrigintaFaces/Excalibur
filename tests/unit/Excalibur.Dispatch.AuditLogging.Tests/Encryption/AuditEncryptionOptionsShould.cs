using Excalibur.Dispatch.AuditLogging.Encryption;

namespace Excalibur.Dispatch.AuditLogging.Tests.Encryption;

public class AuditEncryptionOptionsShould
{
    [Fact]
    public void Default_encrypt_actor_id_to_true()
    {
        var options = new AuditEncryptionOptions();

        options.EncryptActorId.ShouldBeTrue();
    }

    [Fact]
    public void Default_encrypt_ip_address_to_true()
    {
        var options = new AuditEncryptionOptions();

        options.EncryptIpAddress.ShouldBeTrue();
    }

    [Fact]
    public void Default_encrypt_reason_to_false()
    {
        var options = new AuditEncryptionOptions();

        options.EncryptReason.ShouldBeFalse();
    }

    [Fact]
    public void Default_encrypt_user_agent_to_false()
    {
        var options = new AuditEncryptionOptions();

        options.EncryptUserAgent.ShouldBeFalse();
    }

    [Fact]
    public void Default_encryption_purpose_to_audit_event_field()
    {
        var options = new AuditEncryptionOptions();

        options.EncryptionPurpose.ShouldBe("audit-event-field");
    }

    [Fact]
    public void Allow_setting_all_flags()
    {
        var options = new AuditEncryptionOptions
        {
            EncryptActorId = false,
            EncryptIpAddress = false,
            EncryptReason = true,
            EncryptUserAgent = true,
            EncryptionPurpose = "custom-purpose"
        };

        options.EncryptActorId.ShouldBeFalse();
        options.EncryptIpAddress.ShouldBeFalse();
        options.EncryptReason.ShouldBeTrue();
        options.EncryptUserAgent.ShouldBeTrue();
        options.EncryptionPurpose.ShouldBe("custom-purpose");
    }
}
