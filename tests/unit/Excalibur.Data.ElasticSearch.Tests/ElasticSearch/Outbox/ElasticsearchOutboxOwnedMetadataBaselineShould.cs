using System.Text;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport.Extensions;
using Excalibur.Outbox;
using Microsoft.Extensions.DependencyInjection;
using FakeItEasy;
using Shouldly;
using Xunit;

namespace Excalibur.Data.Tests.ElasticSearch.Outbox;

[Trait("Category", "Unit")]
[Trait("Component", "ElasticSearch")]
[Trait("Pattern", "Outbox")]
public sealed class ElasticsearchOutboxOwnedMetadataBaselineShould
{
    [Fact]
    public void RefuseUnregisteredSourceTypesOnFrameworkOwnedClient()
    {
        var services = new ServiceCollection();
        var builder = A.Fake<IOutboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);
        builder.UseElasticSearch(es => es.NodeUri(new Uri("http://localhost:9200")));
        using var provider = services.BuildServiceProvider();
        var serializer = provider.GetRequiredService<ElasticsearchClient>().SourceSerializer;
        Should.Throw<NotSupportedException>(() => serializer.SerializeToString(new UnregisteredSource { Value = "not generated" }));
    }

    [Fact]
    public void ParseActualCloudIdRatherThanTreatingItAsNodeUri()
    {
        var cloudId = "example:" + Convert.ToBase64String(Encoding.UTF8.GetBytes("example.test$elastic-id$kibana-id"));
        var services = new ServiceCollection();
        var builder = A.Fake<IOutboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);
        builder.UseElasticSearch(es => es.CloudId(cloudId));
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ElasticsearchClient>().ElasticsearchClientSettings.NodePool.Nodes.Single().Uri
            .ShouldBe(new Uri("https://elastic-id.example.test"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KeepBorrowedClientIdentity(bool factory)
    {
        var client = new ElasticsearchClient();
        var services = new ServiceCollection();
        var builder = A.Fake<IOutboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);
        builder.UseElasticSearch(es =>
        {
            if (factory) { es.ClientFactory(_ => client); }
            else { es.Client(client); }
        });
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ElasticsearchClient>().ShouldBeSameAs(client);
    }

    private sealed class UnregisteredSource { public string Value { get; init; } = string.Empty; }
}
