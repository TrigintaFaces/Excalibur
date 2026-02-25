namespace Tests.Shared.Categories;

/// <summary>
/// Test category constants for filtering tests by type.
/// Usage: [Trait("Category", TestCategories.Unit)]
/// Run with: dotnet test --filter "Category=Unit"
/// </summary>
public static class TestCategories
{
	/// <summary>
	/// Fast, isolated tests with no external dependencies.
	/// Target: complete in &lt;30 seconds total.
	/// </summary>
	public const string Unit = "Unit";

	/// <summary>
	/// Tests that require external infrastructure (databases, message brokers).
	/// Uses TestContainers. Target: complete in &lt;2 minutes total.
	/// </summary>
	public const string Integration = "Integration";

	/// <summary>
	/// End-to-end workflow tests covering full business scenarios.
	/// </summary>
	public const string Functional = "Functional";

	/// <summary>
	/// Architecture enforcement tests using NetArchTest.
	/// </summary>
	public const string Architecture = "Architecture";

	/// <summary>
	/// Performance and load tests. Not run in CI by default.
	/// </summary>
	public const string Performance = "Performance";

	/// <summary>
	/// Tests that require specific cloud provider credentials.
	/// </summary>
	public const string RequiresCredentials = "RequiresCredentials";

	/// <summary>
	/// Long-running tests that may timeout in CI.
	/// </summary>
	public const string LongRunning = "LongRunning";
}

/// <summary>
/// Component trait constants for CI shard targeting.
/// Usage: [Trait("Component", TestComponents.Core)]
/// Run with: dotnet test --filter "Component=Core"
/// </summary>
public static class TestComponents
{
	public const string Core = "Core";
	public const string Messaging = "Messaging";
	public const string Transport = "Transport";
	public const string Middleware = "Middleware";
	public const string Observability = "Observability";
	public const string Security = "Security";
	public const string Resilience = "Resilience";
	public const string Caching = "Caching";
	public const string Serialization = "Serialization";
	public const string AuditLogging = "AuditLogging";
	public const string Compliance = "Compliance";
	public const string EventSourcing = "EventSourcing";
	public const string Data = "Data";
	public const string Domain = "Domain";
	public const string Hosting = "Hosting";
	public const string Outbox = "Outbox";
	public const string Saga = "Saga";
	public const string Jobs = "Jobs";
	public const string LeaderElection = "LeaderElection";
	public const string Patterns = "Patterns";
	public const string Configuration = "Configuration";
	public const string DependencyInjection = "DependencyInjection";
}

/// <summary>
/// Infrastructure requirement traits for integration test sharding.
/// Usage: [Trait("Infrastructure", TestInfrastructure.SqlServer)]
/// Run with: dotnet test --filter "Infrastructure=SqlServer"
/// </summary>
public static class TestInfrastructure
{
	public const string None = "None";
	public const string SqlServer = "SqlServer";
	public const string Postgres = "Postgres";
	public const string Redis = "Redis";
	public const string MongoDB = "MongoDB";
	public const string Kafka = "Kafka";
	public const string RabbitMQ = "RabbitMQ";
	public const string Elasticsearch = "Elasticsearch";
	public const string CosmosDb = "CosmosDb";
	public const string DynamoDb = "DynamoDb";
	public const string Firestore = "Firestore";
}

// Note: Use xUnit's built-in [assembly: Xunit.AssemblyTrait("Component", TestComponents.Core)]
// for assembly-level traits. No custom attribute needed.

/// <summary>
/// Attribute-based test categorization for cleaner syntax.
/// Usage: [UnitTest] instead of [Trait("Category", "Unit")]
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class UnitTestAttribute : Attribute, Xunit.Sdk.ITraitAttribute
{
	public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits()
		=> [new KeyValuePair<string, string>("Category", TestCategories.Unit)];
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class IntegrationTestAttribute : Attribute, Xunit.Sdk.ITraitAttribute
{
	public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits()
		=> [new KeyValuePair<string, string>("Category", TestCategories.Integration)];
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class FunctionalTestAttribute : Attribute, Xunit.Sdk.ITraitAttribute
{
	public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits()
		=> [new KeyValuePair<string, string>("Category", TestCategories.Functional)];
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class ArchitectureTestAttribute : Attribute, Xunit.Sdk.ITraitAttribute
{
	public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits()
		=> [new KeyValuePair<string, string>("Category", TestCategories.Architecture)];
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class PerformanceTestAttribute : Attribute, Xunit.Sdk.ITraitAttribute
{
	public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits()
		=> [new KeyValuePair<string, string>("Category", TestCategories.Performance)];
}
