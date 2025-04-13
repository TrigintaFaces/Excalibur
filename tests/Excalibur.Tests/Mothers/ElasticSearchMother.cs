using System.Globalization;

using Excalibur.DataAccess.SqlServer;
using Excalibur.DataAccess.SqlServer.Cdc;
using Excalibur.Tests.Shared;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Tests.Mothers;

public static class ElasticSearchMother
{
	public static TestElasticDocument CreateTestDocument(string? id = null, string? name = null) =>
		new() { Id = id ?? Guid.NewGuid().ToString("N"), Name = name ?? "DefaultName" };

	public static IEnumerable<TestElasticDocument> CreateManyTestDocuments(int count)
	{
		for (var i = 0; i < count; i++)
		{
			yield return CreateTestDocument(id: i.ToString(CultureInfo.CurrentCulture), name: $"Name {i}");
		}
	}
}

public class TestElasticDocument
{
	public string Id { get; set; } = null!;
	public string Name { get; set; } = null!;
}

public static class CdcProcessorMother
{
	public static CdcProcessor Create(
		IDatabaseConfig config,
		SqlConnection cdcConnection,
		SqlConnection stateConnection,
		ILogger<CdcProcessor> logger)
	{
		using var appLifetime = new TestAppLifetime();
		var policyFactory = new SqlDataAccessPolicyFactory(new NullLogger<SqlDataAccessPolicyFactory>());

		return new CdcProcessor(
			appLifetime,
			config,
			cdcConnection,
			stateConnection,
			policyFactory,
			logger);
	}
}
