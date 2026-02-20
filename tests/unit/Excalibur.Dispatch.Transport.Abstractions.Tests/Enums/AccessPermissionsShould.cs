using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Enums;

public class AccessPermissionsShould
{
    [Fact]
    public void Should_Have_None_As_Zero()
    {
        ((int)AccessPermissions.None).ShouldBe(0);
    }

    [Fact]
    public void Should_Have_Send_As_1()
    {
        ((int)AccessPermissions.Send).ShouldBe(1);
    }

    [Fact]
    public void Should_Have_Receive_As_2()
    {
        ((int)AccessPermissions.Receive).ShouldBe(2);
    }

    [Fact]
    public void Should_Have_Manage_As_4()
    {
        ((int)AccessPermissions.Manage).ShouldBe(4);
    }

    [Fact]
    public void All_Should_Combine_Send_Receive_Manage()
    {
        AccessPermissions.All.ShouldBe(
            AccessPermissions.Send | AccessPermissions.Receive | AccessPermissions.Manage);
    }

    [Fact]
    public void Should_Support_Flags_Combination()
    {
        var permissions = AccessPermissions.Send | AccessPermissions.Receive;

        permissions.HasFlag(AccessPermissions.Send).ShouldBeTrue();
        permissions.HasFlag(AccessPermissions.Receive).ShouldBeTrue();
        permissions.HasFlag(AccessPermissions.Manage).ShouldBeFalse();
    }

    [Fact]
    public void Should_Be_Flags_Enum()
    {
        typeof(AccessPermissions).GetCustomAttributes(typeof(FlagsAttribute), false)
            .Length.ShouldBe(1);
    }
}
