using Excalibur.DataAccess.ElasticSearch;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess.ElasticSearch;

public class HealthChecksBuilderExtensionsShould
{
	[Theory]
	[InlineData(null, "Elastic")]
	[InlineData("builder", null)]
	public void ShouldThrowArgumentNullExceptionWhenAnyArgumentIsNull(string? builderLabel, string? name)
	{
		// Arrange
		var builder = builderLabel == null ? null : A.Fake<IHealthChecksBuilder>();

		// Act & Assert
		var ex = Should.Throw<ArgumentNullException>(() =>
			builder!.AddElasticHealthCheck(name!,
				TimeSpan.FromSeconds(5)));

		_ = ex.ParamName.ShouldNotBeNull();
	}

	[Fact]
	public void ShouldThrowWhenTimeoutIsZeroOrNegative()
	{
		// Arrange
		var builder = A.Fake<IHealthChecksBuilder>();

		// Act & Assert
		var ex = Should.Throw<ArgumentOutOfRangeException>(() =>
			builder.AddElasticHealthCheck("Elastic", TimeSpan.Zero));

		ex.ParamName.ShouldBe("timeout");
	}
}
