namespace Tests.Shared.Categories;

/// <summary>
/// Trait key name constants to avoid magic strings in [Trait] attributes.
/// Usage: [Trait(TraitNames.Category, TestCategories.Unit)]
/// </summary>
public static class TraitNames
{
	public const string Category = "Category";
	public const string Component = "Component";
	public const string Feature = "Feature";
	public const string Pattern = "Pattern";
	public const string Infrastructure = "Infrastructure";
}

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

	/// <summary>
	/// Conformance tests validating provider implementations against shared contracts.
	/// For transport-specific conformance, combine with [Trait("Component", TestComponents.Transport)].
	/// </summary>
	public const string Conformance = "Conformance";

	/// <summary>
	/// Contract tests verifying API compatibility and public surface stability.
	/// </summary>
	public const string Contract = "Contract";

	/// <summary>
	/// Smoke tests verifying basic package loading and DI registration.
	/// </summary>
	public const string Smoke = "Smoke";

	/// <summary>
	/// End-to-end tests spanning multiple system boundaries.
	/// </summary>
	public const string EndToEnd = "EndToEnd";
}

/// <summary>
/// Component trait constants for CI shard targeting.
/// Maps to package/module boundaries, not infrastructure providers.
/// Usage: [Trait("Component", TestComponents.Core)]
/// Run with: dotnet test --filter "Component=Core"
/// </summary>
public static class TestComponents
{
	// === Dispatch Framework ===
	public const string Core = "Core";
	public const string Abstractions = "Abstractions";
	public const string Dispatcher = "Dispatcher";
	public const string Pipeline = "Pipeline";
	public const string Handlers = "Handlers";
	public const string Delivery = "Delivery";

	// === Messaging ===
	public const string Messaging = "Messaging";
	public const string BatchProcessing = "BatchProcessing";
	public const string Streaming = "Streaming";
	public const string CloudEvents = "CloudEvents";

	// === Middleware ===
	public const string Middleware = "Middleware";
	public const string Deduplication = "Deduplication";
	public const string Validation = "Validation";

	// === Transport ===
	public const string Transport = "Transport";
	public const string TransportAbstractions = "Transport.Abstractions";
	public const string RabbitMQ = "Transport.RabbitMQ";
	public const string Kafka = "Transport.Kafka";
	public const string AzureServiceBus = "Transport.AzureServiceBus";
	public const string AwsSqs = "Transport.AwsSqs";
	public const string GooglePubSub = "Transport.GooglePubSub";
	public const string Grpc = "Transport.Grpc";

	// === Cross-Cutting ===
	public const string Observability = "Observability";
	public const string Security = "Security";
	public const string Resilience = "Resilience";
	public const string Caching = "Caching";
	public const string Serialization = "Serialization";
	public const string AuditLogging = "AuditLogging";
	public const string Compliance = "Compliance";
	public const string Configuration = "Configuration";
	public const string Options = "Options";
	public const string DependencyInjection = "DependencyInjection";
	public const string Diagnostics = "Diagnostics";
	public const string Hosting = "Hosting";
	public const string SourceGenerators = "SourceGenerators";

	// === Patterns ===
	public const string Outbox = "Outbox";
	public const string Inbox = "Inbox";
	public const string Saga = "Saga";
	public const string ClaimCheck = "ClaimCheck";
	public const string Patterns = "Patterns";

	// === Excalibur Framework ===
	public const string Domain = "Domain";
	public const string Data = "Data";
	public const string EventSourcing = "EventSourcing";
	public const string Jobs = "Jobs";
	public const string LeaderElection = "LeaderElection";
	public const string Projections = "Projections";
	public const string Testing = "Testing";

	// === CDC ===
	public const string CDC = "CDC";

	// === Architecture / Platform ===
	public const string Architecture = "Architecture";
	public const string Platform = "Platform";
}

/// <summary>
/// Feature trait constants for fine-grained test categorization within a component.
/// Usage: [Trait("Feature", TestFeatures.Authorization)]
/// Run with: dotnet test --filter "Feature=Authorization"
/// </summary>
public static class TestFeatures
{
	public const string Configuration = "Configuration";
	public const string Context = "Context";
	public const string DependencyInjection = "DependencyInjection";
	public const string Authorization = "Authorization";
	public const string Encryption = "Encryption";
	public const string Sanitization = "Sanitization";
	public const string Monitoring = "Monitoring";
	public const string Metrics = "Metrics";
	public const string HealthChecks = "HealthChecks";
	public const string Sampling = "Sampling";
	public const string Abstractions = "Abstractions";
	public const string Stores = "Stores";
	public const string Projections = "Projections";
	public const string SchemaRegistry = "SchemaRegistry";
	public const string IndexManagement = "IndexManagement";
	public const string LeaderElection = "LeaderElection";
	public const string Inbox = "Inbox";
	public const string Outbox = "Outbox";
	public const string AOT = "AOT";
	public const string CDC = "CDC";
	public const string Resilience = "Resilience";
	public const string ColdStart = "ColdStart";
	public const string Concurrency = "Concurrency";
	public const string Masking = "Masking";
	public const string Sharding = "Sharding";
}

/// <summary>
/// Pattern trait constants for conformance test classification.
/// Usage: [Trait("Pattern", TestPatterns.Transport)]
/// Run with: dotnet test --filter "Pattern=Transport"
/// </summary>
public static class TestPatterns
{
	public const string Transport = "Transport";
	public const string Store = "Store";
	public const string Service = "Service";
	public const string Provider = "Provider";
	public const string Cache = "Cache";
	public const string Validator = "Validator";
	public const string Registry = "Registry";
	public const string Generator = "Generator";
	public const string Scheduler = "Scheduler";
	public const string AlertHandler = "AlertHandler";
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
