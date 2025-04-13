using Excalibur.Core;

using Shouldly;

namespace Excalibur.Tests.Unit.Core;

public class ExcaliburHeaderNamesShould
{
	[Fact]
	public void HaveCorrectCorrelationIdHeader()
	{
		// Act & Assert
		ExcaliburHeaderNames.CorrelationId.ShouldBe("excalibur-correlation-id");
	}

	[Fact]
	public void HaveCorrectETagHeader()
	{
		// Act & Assert
		ExcaliburHeaderNames.ETag.ShouldBe("excalibur-etag");
	}

	[Fact]
	public void HaveCorrectTenantIdHeader()
	{
		// Act & Assert
		ExcaliburHeaderNames.TenantId.ShouldBe("excalibur-tenant-id");
	}

	[Fact]
	public void HaveCorrectRaisedByHeader()
	{
		// Act & Assert
		ExcaliburHeaderNames.RaisedBy.ShouldBe("excalibur-raised-by");
	}
}
