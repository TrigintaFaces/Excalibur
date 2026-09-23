// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;
using System.Text.Json.Nodes;

using Excalibur.Outbox.CosmosDb;

namespace Excalibur.Data.CosmosDb.Tests.Outbox;

/// <summary>
/// Binds the outbox delivery guarantee's duplicate-suppression half at the wire: the stored document must
/// emit the exact property names the server-side claim predicate reads, under EITHER serializer.
/// <para>
/// <b>Why the emitted name and not the attribute.</b> Asserting that a mapping attribute is present tests
/// that we wrote an attribute; it does not test that the serializer actually in use honors it. The client
/// this store builds configures System.Text.Json, but <c>ICosmosDbOutboxBuilder.Client(...)</c> and
/// <c>ClientFactory(...)</c> let a consumer supply their own <c>CosmosClient</c>, which bypasses that
/// configuration and uses the SDK default — Newtonsoft. So the framework does not own the serializer, and
/// only serializing through both and reading the emitted keys can tell the two apart.
/// </para>
/// <para>
/// <b>Why it is a safety defect rather than a misconfiguration.</b> The claim predicate is
/// <c>NOT IS_DEFINED(c.leasedAt) OR IS_NULL(c.leasedAt) OR c.leasedAt &lt; @leaseCutoff</c>. If the document
/// reaches the wire as <c>LeasedAt</c>, then <c>c.leasedAt</c> is undefined on every row, the first clause
/// is TRUE for every row, and every message reads as unclaimed no matter who holds the lease. The atomic
/// claim is then inert and two instances publish the same message. The predicate fails OPEN.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox.CosmosDb")]
public sealed class CosmosDbOutboxDocumentWireNamesShould
{
	/// <summary>
	/// Every name the queries in <c>CosmosDbOutboxStore</c> read off a document, plus the system properties
	/// Cosmos itself requires. Written out rather than derived from the type on purpose: deriving them from
	/// the same attributes under test would make this assert that the type agrees with itself.
	/// </summary>
	private static readonly string[] RequiredWireNames =
	[
		"id",            // Cosmos system property AND c.id
		"partitionKey",  // c.partitionKey
		"createdAt",     // c.createdAt -- the pending-order and age predicates
		"isPublished",   // c.isPublished -- the pending filter
		"publishedAt",   // c.publishedAt -- the purge predicate
		"leasedAt",      // c.leasedAt -- THE CLAIM PREDICATE
		"leasedBy",      // the claimant, read back to prove ownership
		"_etag",         // Cosmos system property, the conditional-write token
	];

	[Fact]
	public void EmitEveryRequiredNameUnderSystemTextJsonWithNoNamingPolicyConfigured()
	{
		// A consumer-supplied client that happens to use STJ still gets the SDK's own options, NOT ours --
		// so no camelCase policy is configured here. That is the whole point of the arm.
		var json = JsonSerializer.Serialize(NewDocument(), new JsonSerializerOptions());

		AssertNames(JsonNode.Parse(json)!.AsObject().Select(p => p.Key).ToArray(), "System.Text.Json");
	}

	[Fact]
	public void EmitEveryRequiredNameUnderTheSdkDefaultNewtonsoftSerializer()
	{
		// This is the shape a consumer-supplied CosmosClient actually produces today.
		var json = Newtonsoft.Json.JsonConvert.SerializeObject(NewDocument());

		AssertNames(JsonNode.Parse(json)!.AsObject().Select(p => p.Key).ToArray(), "Newtonsoft");
	}

	// LIVENESS CONTROL. Without it, a document type that emitted NOTHING -- or that emitted every property
	// under every possible name -- would satisfy both arms above. This pins that the PascalCase shape the
	// defect produced is genuinely absent, so the arms are measuring the emitted name and not merely the
	// presence of some key.
	[Theory]
	[InlineData("LeasedAt")]
	[InlineData("LeasedBy")]
	[InlineData("IsPublished")]
	public void NotEmitThePascalCaseShapeTheClaimPredicateCannotRead(string pascalName)
	{
		var stj = JsonNode.Parse(JsonSerializer.Serialize(NewDocument(), new JsonSerializerOptions()))!.AsObject();
		var newtonsoft = JsonNode.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(NewDocument()))!.AsObject();

		stj.ContainsKey(pascalName).ShouldBeFalse($"System.Text.Json emitted '{pascalName}'");
		newtonsoft.ContainsKey(pascalName).ShouldBeFalse($"Newtonsoft emitted '{pascalName}'");
	}

	// The per-document TTL is validated by Cosmos whenever it is PRESENT and rejects the whole write with
	// 400 BadRequest for an explicit null. Both serializers must omit it, not just the configured one.
	[Fact]
	public void OmitTheTimeToLivePropertyEntirelyWhenItHasNoValue()
	{
		var document = NewDocument();
		document.Ttl = null;

		var stj = JsonNode.Parse(JsonSerializer.Serialize(document, new JsonSerializerOptions()))!.AsObject();
		var newtonsoft = JsonNode.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(document))!.AsObject();

		stj.ContainsKey("ttl").ShouldBeFalse("Cosmos rejects the write outright for an explicit null ttl");
		newtonsoft.ContainsKey("ttl").ShouldBeFalse("Cosmos rejects the write outright for an explicit null ttl");
	}

	private static void AssertNames(string[] emitted, string serializer)
	{
		foreach (var required in RequiredWireNames)
		{
			emitted.ShouldContain(
				required,
				$"{serializer} did not emit '{required}'. A query reads that name off the document, so under "
				+ "this serializer it is undefined on every row.");
		}
	}

	private static CosmosDbOutboxDocument NewDocument() =>
		new()
		{
			Id = "message-1",
			PartitionKey = "tenant-a",
			MessageType = "Test.Message",
			Payload = "cGF5bG9hZA==",
			CreatedAt = DateTimeOffset.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
			LeasedAt = DateTimeOffset.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
			LeasedBy = "instance-1",
			ETag = "\"0000-etag\"",
			Ttl = 60,
		};
}
