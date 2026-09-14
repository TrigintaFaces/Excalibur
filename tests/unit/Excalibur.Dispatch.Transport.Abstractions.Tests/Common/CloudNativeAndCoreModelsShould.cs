using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Common;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class CloudNativeAndCoreModelsShould
{
	[Fact]
	public void AccessRule_StoresPrincipalAndPermissions()
	{
		var rule = new AccessRule
		{
			Principal = "svc-orders",
			Permissions = AccessPermissions.Receive | AccessPermissions.Send
		};

		rule.Principal.ShouldBe("svc-orders");
		rule.Permissions.ShouldBe(AccessPermissions.Receive | AccessPermissions.Send);
	}

}
