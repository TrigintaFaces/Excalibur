// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Excalibur.Data.CosmosDb.Tests.ChangeFeed;

/// <summary>
/// Deterministic (no real-infra) regression guard for bead <c>i2eabb</c> (sprint 855 REVIEW_ARCH BLOCKING):
/// the durable <c>CosmosDbChangeFeedCheckpointStore.CheckpointDocument</c> MUST serialize to Cosmos's
/// required lowercase keys (<c>id</c>, and the partition-key field <c>subscriptionId</c>) under the
/// <b>Cosmos SDK v3 DEFAULT serializer (Newtonsoft.Json)</b> — not only under an opt-in System.Text.Json client.
/// </summary>
/// <remarks>
/// <para>
/// <b>The bug this catches:</b> the store takes a <i>consumer-supplied</i> <c>Container</c>, so the framework
/// does not control the serializer. The SDK-v3 default is Newtonsoft (STJ is opt-in via
/// <c>CosmosClientOptions.UseSystemTextJsonSerializer…</c>). Newtonsoft ignores STJ
/// <c>[JsonPropertyName]</c> attributes, so a document carrying <b>only</b> the STJ attribute serializes
/// PascalCase (<c>"Id"</c>/<c>"SubscriptionId"</c>): the Cosmos-required lowercase <c>id</c> is absent →
/// the point-read by <c>id</c> returns NotFound → <c>LoadAsync</c> returns null → resume-from-Beginning every
/// restart → durable continuation <b>silently inert</b> on the most common client config. The PK field
/// mismatch (<c>"SubscriptionId"</c> vs the documented <c>/subscriptionId</c> path) is the same root cause
/// (Platform Facet-2, msg 17277).
/// </para>
/// <para>
/// <b>Why Newtonsoft here:</b> <see cref="JsonConvert"/> with default settings reproduces exactly what the
/// default <c>CosmosClient</c> emits, so this asserts the real production serialization without a live emulator.
/// The fix is the standard dual-attribute Cosmos pattern (STJ <c>[JsonPropertyName]</c> + Newtonsoft
/// <c>[JsonProperty]</c>); this lock is <b>RED</b> on the STJ-only/no-attr surface and GREEN once both attrs
/// are present. The <i>real-Cosmos</i> half stays the deferred <c>ajt1iy</c> lock (now using a default-serializer
/// client).
/// </para>
/// <para>
/// <b>Internal-first:</b> <c>CheckpointDocument</c> is a private nested type; this reaches it via reflection
/// rather than widening production visibility for a test (per the internal-first rule).
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Cdc")]
public sealed class ChangeFeedCheckpointDocumentSerializationShould
{
	[Fact]
	public void SerializeIdToCosmosRequiredLowercaseIdUnderTheDefaultNewtonsoftSerializer()
	{
		var json = SerializeCheckpointDocumentWithNewtonsoft("sub-i2eabb-id", "continuation-token-1");
		var keys = JObject.Parse(json).Properties().Select(p => p.Name).ToList();

		keys.ShouldContain(
			"id",
			$"i2eabb — CheckpointDocument.Id must serialize to Cosmos-required lowercase 'id' under the DEFAULT (Newtonsoft) serializer. Emitted JSON: {json}");
		keys.ShouldNotContain(
			"Id",
			"i2eabb — a PascalCase 'Id' under the default serializer means Cosmos's point-read by 'id' returns NotFound → LoadAsync null → durable continuation silently inert.");
	}

	[Fact]
	public void SerializeSubscriptionIdMatchingThePartitionKeyPathUnderTheDefaultNewtonsoftSerializer()
	{
		var json = SerializeCheckpointDocumentWithNewtonsoft("sub-i2eabb-pk", "continuation-token-2");
		var keys = JObject.Parse(json).Properties().Select(p => p.Name).ToList();

		// The store's partition-key path is /subscriptionId and SaveAsync/LoadAsync pass new PartitionKey(subscriptionId);
		// the document's PK field MUST serialize to lowercase 'subscriptionId' or the write/read partition key won't
		// match the document under a default serializer (Platform Facet-2).
		keys.ShouldContain(
			"subscriptionId",
			$"i2eabb Facet-2 — CheckpointDocument.SubscriptionId must serialize to lowercase 'subscriptionId' (partition-key path /subscriptionId) under the DEFAULT (Newtonsoft) serializer. Emitted JSON: {json}");
		keys.ShouldNotContain(
			"SubscriptionId",
			"i2eabb Facet-2 — a PascalCase 'SubscriptionId' won't match the /subscriptionId partition-key path under a default serializer → write/read PK mismatch.");
	}

	// Builds the private CheckpointDocument and serializes it exactly as the SDK-v3 default (Newtonsoft) client would.
	private static string SerializeCheckpointDocumentWithNewtonsoft(string subscriptionId, string continuationToken)
	{
		// Anchor on a PUBLIC type to resolve the production assembly without depending on InternalsVisibleTo.
		var assembly = typeof(CosmosDbServiceCollectionExtensions).Assembly;
		var storeType = assembly.GetType("Excalibur.Data.CosmosDb.CosmosDbChangeFeedCheckpointStore", throwOnError: true)!;
		var docType = storeType.GetNestedType("CheckpointDocument", BindingFlags.NonPublic)
			?? throw new InvalidOperationException(
				"i2eabb — CheckpointDocument nested type not found under CosmosDbChangeFeedCheckpointStore (scan mis-located — refusing to pass vacuously).");

		var doc = Activator.CreateInstance(docType, nonPublic: true)
			?? throw new InvalidOperationException("i2eabb — could not construct CheckpointDocument.");

		SetProperty(docType, doc, "Id", subscriptionId);
		SetProperty(docType, doc, "SubscriptionId", subscriptionId);
		SetProperty(docType, doc, "ContinuationToken", continuationToken);
		SetProperty(docType, doc, "UpdatedAt", DateTimeOffset.UtcNow);

		return JsonConvert.SerializeObject(doc);
	}

	private static void SetProperty(Type type, object instance, string name, object value)
	{
		var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
			?? throw new InvalidOperationException(
				$"i2eabb — CheckpointDocument.{name} not found (the document shape changed — update the serialization guard).");
		prop.SetValue(instance, value);
	}
}
