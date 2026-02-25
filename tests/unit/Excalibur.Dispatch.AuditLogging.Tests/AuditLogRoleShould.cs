namespace Excalibur.Dispatch.AuditLogging.Tests;

public class AuditLogRoleShould
{
    [Fact]
    public void Define_none_as_zero()
    {
        ((int)AuditLogRole.None).ShouldBe(0);
    }

    [Fact]
    public void Define_developer_as_one()
    {
        ((int)AuditLogRole.Developer).ShouldBe(1);
    }

    [Fact]
    public void Define_security_analyst_as_two()
    {
        ((int)AuditLogRole.SecurityAnalyst).ShouldBe(2);
    }

    [Fact]
    public void Define_compliance_officer_as_three()
    {
        ((int)AuditLogRole.ComplianceOfficer).ShouldBe(3);
    }

    [Fact]
    public void Define_administrator_as_four()
    {
        ((int)AuditLogRole.Administrator).ShouldBe(4);
    }

    [Fact]
    public void Order_roles_by_increasing_privilege()
    {
        ((int)AuditLogRole.None).ShouldBeLessThan((int)AuditLogRole.Developer);
        ((int)AuditLogRole.Developer).ShouldBeLessThan((int)AuditLogRole.SecurityAnalyst);
        ((int)AuditLogRole.SecurityAnalyst).ShouldBeLessThan((int)AuditLogRole.ComplianceOfficer);
        ((int)AuditLogRole.ComplianceOfficer).ShouldBeLessThan((int)AuditLogRole.Administrator);
    }

    [Fact]
    public void Have_five_defined_values()
    {
        var values = Enum.GetValues<AuditLogRole>();

        values.Length.ShouldBe(5);
    }
}
