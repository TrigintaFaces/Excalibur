using Excalibur.DataAccess.ElasticSearch;
using Excalibur.Tests.Shared;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess.ElasticSearch;

public class ElasticsearchConfigurationSettingsShould
{
	[Fact]
	public void BindCorrectlyFromConfiguration()
	{
		// Arrange & Act
		var settings = ConfigurationTestHelper.BindSettings<ElasticsearchConfigurationSettings>(
			new Dictionary<string, string?>
			{
				["ElasticSearch:Url"] = "http://localhost:9200",
				["ElasticSearch:CertificateFingerprint"] = "ABCDEF123456",
				["ElasticSearch:Username"] = "elastic",
				["ElasticSearch:Password"] = "password123",
				["ElasticSearch:ApiKey"] = "apikey-123"
			}, "ElasticSearch");

		// Assert
		_ = settings.ShouldNotBeNull();
		settings!.Url.ShouldBe("http://localhost:9200");
		settings.CertificateFingerprint.ShouldBe("ABCDEF123456");
		settings.Username.ShouldBe("elastic");
		settings.Password.ShouldBe("password123");
		settings.ApiKey.ShouldBe("apikey-123");
	}

	[Fact]
	public void ThrowIfUrlIsNotProvided()
	{
		// Arrange & Act
		var settings = ConfigurationTestHelper.BindSettings<ElasticsearchConfigurationSettings>(
			new Dictionary<string, string?> { ["ElasticSearch:CertificateFingerprint"] = "ABCDEF123456" }, "ElasticSearch");

		// Assert Url is required (init-only non-nullable), so will throw on access if not bound
		_ = Should.Throw<NullReferenceException>(() =>
		{
			_ = settings!.Url.Length;
		});
	}

	[Fact]
	public void HaveNullForOptionalPropertiesIfNotConfigured()
	{
		// Arrange & Act
		var settings = ConfigurationTestHelper.BindSettings<ElasticsearchConfigurationSettings>(
			new Dictionary<string, string?> { ["ElasticSearch:Url"] = "http://localhost:9200" }, "ElasticSearch");

		// Assert
		_ = settings.ShouldNotBeNull();
		settings!.CertificateFingerprint.ShouldBeNull();
		settings.ApiKey.ShouldBeNull();
		settings.Username.ShouldBeNull();
		settings.Password.ShouldBeNull();
	}
}
