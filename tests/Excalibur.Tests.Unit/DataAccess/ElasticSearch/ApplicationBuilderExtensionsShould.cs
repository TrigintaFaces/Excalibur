using Excalibur.DataAccess.ElasticSearch;

using Microsoft.AspNetCore.Builder;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess.ElasticSearch;

public class ApplicationBuilderExtensionsShould
{
	[Fact]
	public void ThrowArgumentNullExceptionIfApplicationBuilderIsNull()
	{
		IApplicationBuilder? builder = null;

		var exception = Should.Throw<ArgumentNullException>(() =>
			builder!.UseElasticsearchIndexInitialization());

		exception.ParamName.ShouldBe("applicationBuilder");
	}
}
