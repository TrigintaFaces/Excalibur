using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class WellKnownHeaderNamesShould
{
	[Fact]
	public void DefineCorrelationId()
	{
		WellKnownHeaderNames.CorrelationId.ShouldBe("X-Correlation-Id");
	}

	[Fact]
	public void DefineCausationId()
	{
		WellKnownHeaderNames.CausationId.ShouldBe("X-Causation-Id");
	}

	[Fact]
	public void DefineETag()
	{
		WellKnownHeaderNames.ETag.ShouldBe("X-Etag");
	}

	[Fact]
	public void DefineTenantId()
	{
		WellKnownHeaderNames.TenantId.ShouldBe("X-Tenant-Id");
	}

	[Fact]
	public void DefineRaisedBy()
	{
		WellKnownHeaderNames.RaisedBy.ShouldBe("X-Raised-By");
	}
}
