// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch.IndexManagement;

using Excalibur.Data.ElasticSearch.IndexManagement;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// ============================================================================
// ElasticSearch Index Management
// ============================================================================
//
// Demonstrates:
//   1. Index creation, existence checks, health monitoring, settings updates
//   2. Index templates for consistent index configuration
//   3. Index Lifecycle Management (ILM) policies for automated tiering
//   4. Alias management for zero-downtime index swaps
//
// Prerequisites:
//   - Elasticsearch running on http://localhost:9200
//   - docker run -d --name es -p 9200:9200 -e "discovery.type=single-node" \
//       -e "xpack.security.enabled=false" elasticsearch:8.15.0
//
// Note: Some features (ILM, templates) require specific Elasticsearch versions
//       or license levels. Operations that fail will be caught and reported.
//
// ============================================================================

var builder = Host.CreateApplicationBuilder(args);

// Register Elasticsearch client from configuration (reads ElasticSearch:Url from appsettings.json)
builder.Services.AddElasticsearchServices(builder.Configuration, registry: null);

// Register index management services (IIndexOperationsManager, IIndexTemplateManager,
// IIndexLifecycleManager, IIndexAliasManager)
builder.Services.AddElasticsearchIndexManagement(builder.Configuration);

var app = builder.Build();

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var ct = cts.Token;

Console.WriteLine("ElasticSearch Index Management");
Console.WriteLine("==============================");
Console.WriteLine();

// ---------------------------------------------------------------------------
// Section 1: Index Operations (IIndexOperationsManager)
// ---------------------------------------------------------------------------
await DemoIndexOperationsAsync(app.Services, ct).ConfigureAwait(false);

// ---------------------------------------------------------------------------
// Section 2: Index Templates (IIndexTemplateManager)
// ---------------------------------------------------------------------------
await DemoIndexTemplatesAsync(app.Services, ct).ConfigureAwait(false);

// ---------------------------------------------------------------------------
// Section 3: Index Lifecycle Management (IIndexLifecycleManager)
// ---------------------------------------------------------------------------
await DemoLifecyclePoliciesAsync(app.Services, ct).ConfigureAwait(false);

// ---------------------------------------------------------------------------
// Section 4: Alias Management (IIndexAliasManager)
// ---------------------------------------------------------------------------
await DemoAliasManagementAsync(app.Services, ct).ConfigureAwait(false);

Console.WriteLine();
Console.WriteLine("Done! All index management operations demonstrated.");

// ============================================================================
// Demo Methods
// ============================================================================

static async Task DemoIndexOperationsAsync(IServiceProvider services, CancellationToken ct)
{
    Console.WriteLine("--- 1. Index Operations (IIndexOperationsManager) ---");
    Console.WriteLine();

    var indexOps = services.GetRequiredService<IIndexOperationsManager>();
    const string indexName = "demo-index-v1";

    try
    {
        // 1a. Create an index with explicit settings for local development
        Console.WriteLine("  1a. Creating index with custom settings...");
        var config = new IndexConfiguration
        {
            Settings = new IndexSettings
            {
                NumberOfShards = 1,
                NumberOfReplicas = 0,
            },
        };

        var created = await indexOps.CreateIndexAsync(indexName, config, ct).ConfigureAwait(false);
        Console.WriteLine($"      Created '{indexName}': {created}");

        // 1b. Verify index exists
        Console.WriteLine("  1b. Checking index existence...");
        var exists = await indexOps.IndexExistsAsync(indexName, ct).ConfigureAwait(false);
        Console.WriteLine($"      '{indexName}' exists: {exists}");

        // 1c. Check index health (uses cluster health API filtered to pattern)
        Console.WriteLine("  1c. Checking index health...");
        var healthStatuses = await indexOps.GetIndexHealthAsync("demo-*", ct).ConfigureAwait(false);
        foreach (var health in healthStatuses)
        {
            Console.WriteLine(
                $"      Index: {health.IndexName}, Status: {health.Status}, " +
                $"Primary: {health.PrimaryShards}, Replicas: {health.ReplicaShards}");
        }

        // 1d. Update index settings dynamically (e.g., add replicas for production)
        Console.WriteLine("  1d. Updating index settings (adding 1 replica)...");
        var newSettings = new IndexSettings { NumberOfReplicas = 1 };
        var updated = await indexOps.UpdateIndexSettingsAsync(indexName, newSettings, ct).ConfigureAwait(false);
        Console.WriteLine($"      Settings updated: {updated}");

        // 1e. Clean up
        Console.WriteLine("  1e. Deleting index...");
        var deleted = await indexOps.DeleteIndexAsync(indexName, ct).ConfigureAwait(false);
        Console.WriteLine($"      Deleted '{indexName}': {deleted}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"      [Error] Index operations failed: {ex.Message}");
    }

    Console.WriteLine();
}

static async Task DemoIndexTemplatesAsync(IServiceProvider services, CancellationToken ct)
{
    Console.WriteLine("--- 2. Index Templates (IIndexTemplateManager) ---");
    Console.WriteLine();

    var templateMgr = services.GetRequiredService<IIndexTemplateManager>();
    const string templateName = "logs-template";

    try
    {
        // 2a. Define and validate a template before applying
        Console.WriteLine("  2a. Validating template configuration...");
        var template = new IndexTemplateConfiguration
        {
            IndexPatterns = ["logs-*"],
            Priority = 200,
            Version = 1,
            Template = new IndexSettings
            {
                NumberOfShards = 2,
                NumberOfReplicas = 1,
            },
            Metadata = new Dictionary<string, object?>
            {
                ["managed-by"] = "excalibur",
                ["purpose"] = "application-logs",
            },
        };

        var validation = await templateMgr.ValidateTemplateAsync(template, ct).ConfigureAwait(false);
        Console.WriteLine($"      Template valid: {validation.IsValid}");
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                Console.WriteLine($"      Validation error: {error}");
            }
        }

        // 2b. Create the template
        Console.WriteLine("  2b. Creating index template...");
        var created = await templateMgr.CreateOrUpdateTemplateAsync(templateName, template, ct).ConfigureAwait(false);
        Console.WriteLine($"      Created '{templateName}': {created}");

        // 2c. Verify template exists
        Console.WriteLine("  2c. Checking template existence...");
        var exists = await templateMgr.TemplateExistsAsync(templateName, ct).ConfigureAwait(false);
        Console.WriteLine($"      '{templateName}' exists: {exists}");

        // 2d. List templates matching pattern
        Console.WriteLine("  2d. Listing templates matching 'logs-*'...");
        var templates = await templateMgr.GetTemplatesAsync("logs-*", ct).ConfigureAwait(false);
        foreach (var t in templates)
        {
            Console.WriteLine($"      Template: {t.Name}");
        }

        // 2e. Clean up
        Console.WriteLine("  2e. Deleting template...");
        var deleted = await templateMgr.DeleteTemplateAsync(templateName, ct).ConfigureAwait(false);
        Console.WriteLine($"      Deleted '{templateName}': {deleted}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"      [Error] Template operations failed: {ex.Message}");
    }

    Console.WriteLine();
}

static async Task DemoLifecyclePoliciesAsync(IServiceProvider services, CancellationToken ct)
{
    Console.WriteLine("--- 3. Index Lifecycle Management (IIndexLifecycleManager) ---");
    Console.WriteLine();

    var lifecycleMgr = services.GetRequiredService<IIndexLifecycleManager>();
    const string policyName = "logs-policy";

    try
    {
        // 3a. Create an ILM policy with hot -> warm -> cold -> delete phases
        Console.WriteLine("  3a. Creating lifecycle policy...");
        var policy = new IndexLifecyclePolicy
        {
            Hot = new HotPhaseConfiguration
            {
                Priority = 100,
                Rollover = new Excalibur.Data.ElasticSearch.IndexManagement.RolloverConditions
                {
                    MaxAge = TimeSpan.FromDays(7),
                    MaxSize = "50GB",
                    MaxDocs = 100_000_000,
                },
            },
            Warm = new WarmPhaseConfiguration
            {
                MinAge = TimeSpan.FromDays(30),
                NumberOfReplicas = 1,
                ShrinkNumberOfShards = 1,
                Priority = 50,
            },
            Cold = new ColdPhaseConfiguration
            {
                MinAge = TimeSpan.FromDays(90),
                NumberOfReplicas = 0,
                Priority = 0,
            },
            Delete = new DeletePhaseConfiguration
            {
                MinAge = TimeSpan.FromDays(365),
            },
        };

        var created = await lifecycleMgr.CreateLifecyclePolicyAsync(policyName, policy, ct).ConfigureAwait(false);
        Console.WriteLine($"      Created '{policyName}': {created}");

        // 3b. Check lifecycle status for indices matching a pattern
        Console.WriteLine("  3b. Checking lifecycle status for 'logs-*'...");
        var statuses = await lifecycleMgr.GetIndexLifecycleStatusAsync("logs-*", ct).ConfigureAwait(false);
        if (!statuses.Any())
        {
            Console.WriteLine("      No indices found matching 'logs-*' (expected -- no indices created yet)");
        }
        else
        {
            foreach (var status in statuses)
            {
                Console.WriteLine(
                    $"      Index: {status.IndexName}, Phase: {status.Phase}, " +
                    $"Policy: {status.PolicyName}, Age: {status.Age}");
            }
        }

        // 3c. Clean up
        Console.WriteLine("  3c. Deleting lifecycle policy...");
        var deleted = await lifecycleMgr.DeleteLifecyclePolicyAsync(policyName, ct).ConfigureAwait(false);
        Console.WriteLine($"      Deleted '{policyName}': {deleted}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"      [Error] Lifecycle operations failed: {ex.Message}");
        Console.WriteLine("      (ILM requires Elasticsearch Basic license or higher)");
    }

    Console.WriteLine();
}

static async Task DemoAliasManagementAsync(IServiceProvider services, CancellationToken ct)
{
    Console.WriteLine("--- 4. Alias Management (IIndexAliasManager) ---");
    Console.WriteLine();

    var aliasMgr = services.GetRequiredService<IIndexAliasManager>();
    var indexOps = services.GetRequiredService<IIndexOperationsManager>();

    const string indexV1 = "logs-2024-01";
    const string indexV2 = "logs-2024-02";
    const string aliasName = "logs-current";

    try
    {
        // Set up: create two indices for alias demos
        Console.WriteLine("  Setup: Creating two indices for alias demonstration...");
        var minimalConfig = new IndexConfiguration
        {
            Settings = new IndexSettings { NumberOfShards = 1, NumberOfReplicas = 0 },
        };
        await indexOps.CreateIndexAsync(indexV1, minimalConfig, ct).ConfigureAwait(false);
        await indexOps.CreateIndexAsync(indexV2, minimalConfig, ct).ConfigureAwait(false);
        Console.WriteLine($"      Created '{indexV1}' and '{indexV2}'");

        // 4a. Create an alias pointing to the first index
        Console.WriteLine("  4a. Creating alias...");
        var created = await aliasMgr.CreateAliasAsync(aliasName, [indexV1], null, ct).ConfigureAwait(false);
        Console.WriteLine($"      Created alias '{aliasName}' -> [{indexV1}]: {created}");

        // 4b. Verify alias exists
        Console.WriteLine("  4b. Checking alias existence...");
        var exists = await aliasMgr.AliasExistsAsync(aliasName, ct).ConfigureAwait(false);
        Console.WriteLine($"      '{aliasName}' exists: {exists}");

        // 4c. List aliases matching a pattern
        Console.WriteLine("  4c. Listing aliases matching 'logs-*'...");
        var aliases = await aliasMgr.GetAliasesAsync("logs-*", ct).ConfigureAwait(false);
        foreach (var alias in aliases)
        {
            var indices = string.Join(", ", alias.Indices);
            Console.WriteLine($"      Alias: {alias.AliasName} -> [{indices}]");
        }

        // 4d. Atomic alias swap (zero-downtime switch from v1 to v2)
        Console.WriteLine("  4d. Performing atomic alias swap (v1 -> v2)...");
        var operations = new[]
        {
            new AliasOperation
            {
                OperationType = AliasOperationType.Remove,
                AliasName = aliasName,
                IndexName = indexV1,
            },
            new AliasOperation
            {
                OperationType = AliasOperationType.Add,
                AliasName = aliasName,
                IndexName = indexV2,
            },
        };

        var swapped = await aliasMgr.UpdateAliasesAsync(operations, ct).ConfigureAwait(false);
        Console.WriteLine($"      Atomic swap completed: {swapped}");

        // Verify the swap
        var swappedAliases = await aliasMgr.GetAliasesAsync(aliasName, ct).ConfigureAwait(false);
        foreach (var alias in swappedAliases)
        {
            var indices = string.Join(", ", alias.Indices);
            Console.WriteLine($"      After swap: {alias.AliasName} -> [{indices}]");
        }

        // 4e. Clean up alias
        Console.WriteLine("  4e. Deleting alias...");
        var aliasDeleted = await aliasMgr.DeleteAliasAsync(aliasName, null, ct).ConfigureAwait(false);
        Console.WriteLine($"      Deleted alias '{aliasName}': {aliasDeleted}");

        // Clean up indices
        Console.WriteLine("  Cleanup: Deleting demo indices...");
        await indexOps.DeleteIndexAsync(indexV1, ct).ConfigureAwait(false);
        await indexOps.DeleteIndexAsync(indexV2, ct).ConfigureAwait(false);
        Console.WriteLine($"      Deleted '{indexV1}' and '{indexV2}'");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"      [Error] Alias operations failed: {ex.Message}");

        // Best-effort cleanup
        try
        {
            await indexOps.DeleteIndexAsync(indexV1, ct).ConfigureAwait(false);
            await indexOps.DeleteIndexAsync(indexV2, ct).ConfigureAwait(false);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    Console.WriteLine();
}
