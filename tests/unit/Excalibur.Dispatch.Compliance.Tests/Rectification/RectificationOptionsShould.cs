using Excalibur.Dispatch.Compliance.Rectification;

namespace Excalibur.Dispatch.Compliance.Tests.Rectification;

public class RectificationOptionsShould
{
    [Fact]
    public void Have_audit_all_changes_enabled_by_default()
    {
        var options = new RectificationOptions();

        options.AuditAllChanges.ShouldBeTrue();
    }

    [Fact]
    public void Have_require_approval_disabled_by_default()
    {
        var options = new RectificationOptions();

        options.RequireApproval.ShouldBeFalse();
    }

    [Fact]
    public void Allow_setting_audit_all_changes()
    {
        var options = new RectificationOptions { AuditAllChanges = false };

        options.AuditAllChanges.ShouldBeFalse();
    }

    [Fact]
    public void Allow_setting_require_approval()
    {
        var options = new RectificationOptions { RequireApproval = true };

        options.RequireApproval.ShouldBeTrue();
    }
}
