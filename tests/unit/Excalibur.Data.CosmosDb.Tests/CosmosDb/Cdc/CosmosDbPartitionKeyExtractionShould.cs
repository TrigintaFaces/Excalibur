// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;
using System.Text.Json;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging.Abstractions;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Data.Tests.CosmosDb.Cdc;

/// <summary>
/// Holds both Cosmos change-feed processors to the partition-key path semantics the option publishes.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect these arms catch.</b> Both processors resolved a configured partition-key path by
/// stripping the leading slash, doing a single top-level property lookup, and calling
/// <c>GetString()</c> on whatever came back. That implements "a flat, top-level, string-valued property",
/// while the option documents a Cosmos DB <em>partition-key path</em> — a term Cosmos defines, and which
/// admits nested paths and string, numeric and boolean values. Two consequences, of which the quiet one
/// is worse:
/// </para>
/// <list type="bullet">
/// <item><c>/tenant</c> over <c>tenant = 42</c> threw <see cref="InvalidOperationException"/> out of
/// <c>GetString()</c>, before the change ever reached the handler — loud, and it stops the feed.</item>
/// <item><c>/tenant/id</c> over <c>tenant.id = "t1"</c> looked up a literal property named
/// <c>tenant/id</c>, found nothing, and reported a <see langword="null"/> partition key on every single
/// change — silent, and the consumer discovers it downstream or never.</item>
/// </list>
/// <para>
/// <b>Why the arms run against the processors and not only the resolver.</b> A resolver that behaves
/// correctly while a processor still carries its own copy of the old lookup fixes nothing, and that is
/// exactly the shape the defect had — one extraction, written out twice. The arms below drive the real
/// event-construction method of each processor, so a call site that stops delegating goes red here.
/// </para>
/// <para>
/// <b>The control is load-bearing.</b> Every "now it works" arm here would also pass against a resolver
/// that returned the raw JSON text of everything, which would silently change the flat-string case from
/// <c>t1</c> to <c>"t1"</c> for every consumer that already relies on it. The flat-string arm is the case
/// that was measured as working before the fix, and it must still produce the same value after it.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.CDC)]
public sealed class CosmosDbPartitionKeyExtractionShould : UnitTestBase
{
	// Never contacted. No arm here reads a change feed; the SDK's parser only has to accept the shape.
	private const string ConnectionString =
		"AccountEndpoint=https://cdc-partition-key.documents.azure.com:443/;AccountKey=dGVzdA==;";

	// ---------------------------------------------------------------------------------------------
	// The shared resolver both processors delegate to.
	// ---------------------------------------------------------------------------------------------

	[Fact]
	public void ResolveANumericPartitionKeyInsteadOfThrowing()
	{
		// SAFETY. GetString() on a JSON number throws InvalidOperationException, which killed the change
		// before the handler saw it. Microsoft documents numeric partition-key values as valid.
		using var document = JsonDocument.Parse("""{"id":"d1","tenant":42}""");

		var value = Extract(document, "/tenant", out var kind);

		value.ShouldBe("42", "a numeric partition key is valid in Cosmos and must resolve, not throw.");
		kind.ShouldBe(CosmosDbPartitionKeyKind.Number);
	}

	[Fact]
	public void ResolveANestedPartitionKeyPathInsteadOfReportingNothing()
	{
		// SAFETY, and the dangerous half: the old lookup searched for a property literally named
		// "tenant/id", found none, and reported null on every change without any error at all.
		using var document = JsonDocument.Parse("""{"id":"d1","tenant":{"id":"t1"}}""");

		var value = Extract(document, "/tenant/id", out var kind);

		value.ShouldBe("t1", "a partition-key path addresses nested properties; each segment is walked.");
		kind.ShouldBe(CosmosDbPartitionKeyKind.String);
	}

	[Fact]
	public void ResolveAFlatStringPartitionKeyExactlyAsItAlwaysDid()
	{
		// CONTROL — non-vacuous. This is the case that was measured as working before the fix. If the
		// resolver had been written to return raw JSON text uniformly, every arm above would still pass
		// and this one would report "t1" with quotes, silently breaking every existing consumer.
		using var document = JsonDocument.Parse("""{"id":"d1","tenant":"t1"}""");

		var value = Extract(document, "/tenant", out var kind);

		value.ShouldBe("t1", "the string case must be unchanged: the value itself, not its JSON text.");
		kind.ShouldBe(CosmosDbPartitionKeyKind.String);
	}

	[Fact]
	public void ResolveABooleanPartitionKey()
	{
		// Cosmos documents string, number and bool as the partition-key value types.
		using var document = JsonDocument.Parse("""{"id":"d1","archived":true}""");

		var value = Extract(document, "/archived", out var kind);

		value.ShouldBe("true");
		kind.ShouldBe(CosmosDbPartitionKeyKind.Boolean);
	}

	[Fact]
	public void KeepANumericKeyDistinguishableFromAStringKeyOfTheSameText()
	{
		// The number 42 and the string "42" are DIFFERENT Cosmos partitions. Carrying both as the text
		// "42" and nothing else would merge them for any consumer routing on the extracted value.
		using var numeric = JsonDocument.Parse("""{"id":"d1","tenant":42}""");
		using var textual = JsonDocument.Parse("""{"id":"d2","tenant":"42"}""");

		var numericValue = Extract(numeric, "/tenant", out var numericKind);
		var textualValue = Extract(textual, "/tenant", out var textualKind);

		numericValue.ShouldBe(textualValue, "both partitions render to the same text, by construction.");
		numericKind.ShouldBe(CosmosDbPartitionKeyKind.Number);
		textualKind.ShouldBe(CosmosDbPartitionKeyKind.String);
		numericKind.ShouldNotBe(
			textualKind,
			"the two address different partitions, so the event must let them be told apart.");
	}

	[Fact]
	public void ReportAnExplicitJsonNullSeparatelyFromAnAbsentValue()
	{
		// Cosmos distinguishes PartitionKey.Null from PartitionKey.None; collapsing them loses which
		// partition a change came from.
		using var explicitNull = JsonDocument.Parse("""{"id":"d1","tenant":null}""");
		using var absent = JsonDocument.Parse("""{"id":"d1"}""");

		_ = Extract(explicitNull, "/tenant", out var nullKind);
		_ = Extract(absent, "/tenant", out var absentKind);

		nullKind.ShouldBe(CosmosDbPartitionKeyKind.Null);
		absentKind.ShouldBe(CosmosDbPartitionKeyKind.None);
	}

	[Theory]
	[InlineData("/missing")]
	[InlineData("/tenant/missing")]
	[InlineData("/tenant/id/deeper")]
	[InlineData("/")]
	[InlineData("")]
	[InlineData(null)]
	public void ReportNothingWhenThePathDoesNotAddressAScalar(string? path)
	{
		// Every one of these is a path that resolves to no partition-key value — including a path that
		// runs off the end of a scalar, and a path with no segments at all, which must not resolve to the
		// document itself.
		using var document = JsonDocument.Parse("""{"id":"d1","tenant":{"id":"t1"}}""");

		var value = Extract(document, path, out var kind);

		value.ShouldBeNull();
		kind.ShouldBe(CosmosDbPartitionKeyKind.None);
	}

	[Fact]
	public void ReportNothingWhenThePathAddressesAnObject()
	{
		// LIVENESS for the guard above: a path stopping on an object must not stringify the object, which
		// would hand the consumer a partition key Cosmos cannot express.
		using var document = JsonDocument.Parse("""{"id":"d1","tenant":{"id":"t1"}}""");

		var value = Extract(document, "/tenant", out var kind);

		value.ShouldBeNull();
		kind.ShouldBe(CosmosDbPartitionKeyKind.None);
	}

	[Fact]
	public void WalkAPathOfMoreThanTwoSegments()
	{
		// LIVENESS: "walks the segments" must mean all of them, not the first two.
		using var document = JsonDocument.Parse("""{"id":"d1","a":{"b":{"c":"deep"}}}""");

		var value = Extract(document, "/a/b/c", out var kind);

		value.ShouldBe("deep");
		kind.ShouldBe(CosmosDbPartitionKeyKind.String);
	}

	// ---------------------------------------------------------------------------------------------
	// Through the real processors — the two call sites that each carried a copy of the defect.
	// ---------------------------------------------------------------------------------------------

	[Theory]
	[InlineData("/tenant", """{"id":"d1","tenant":42}""", "42", CosmosDbPartitionKeyKind.Number)]
	[InlineData("/tenant/id", """{"id":"d1","tenant":{"id":"t1"}}""", "t1", CosmosDbPartitionKeyKind.String)]
	[InlineData("/archived", """{"id":"d1","archived":false}""", "false", CosmosDbPartitionKeyKind.Boolean)]
	[InlineData("/tenant", """{"id":"d1","tenant":"t1"}""", "t1", CosmosDbPartitionKeyKind.String)]
	public void CarryThePartitionKeyOnEventsBuiltByTheIncrementalProcessor(
		string partitionKeyPath,
		string documentJson,
		string expectedKey,
		CosmosDbPartitionKeyKind expectedKind)
	{
		// The last row is the control: the flat-string case the incremental processor already handled.
		using var client = new CosmosClient(ConnectionString);
		var processor = new CosmosDbCdcProcessor(
			client,
			A.Fake<ICosmosDbCdcStateStore>(),
			MsOptions.Create(new CosmosDbCdcOptions
			{
				ConnectionString = ConnectionString,
				ProcessorName = "partition-key-probe",
				DatabaseId = "cdc",
				ContainerId = "cdc-source",
				PartitionKeyPath = partitionKeyPath,
			}),
			NullLogger<CosmosDbCdcProcessor>.Instance);

		using var document = JsonDocument.Parse(documentJson);

		var changeEvent = InvokeCreateChangeEvent(processor, document);

		changeEvent.PartitionKey.ShouldBe(expectedKey);
		changeEvent.PartitionKeyKind.ShouldBe(expectedKind);
	}

	[Theory]
	[InlineData("/tenant", """{"metadata":{"operationType":"create"},"current":{"id":"d1","tenant":42}}""", "42", CosmosDbPartitionKeyKind.Number)]
	[InlineData("/tenant/id", """{"metadata":{"operationType":"create"},"current":{"id":"d1","tenant":{"id":"t1"}}}""", "t1", CosmosDbPartitionKeyKind.String)]
	[InlineData("/archived", """{"metadata":{"operationType":"create"},"current":{"id":"d1","archived":false}}""", "false", CosmosDbPartitionKeyKind.Boolean)]
	[InlineData("/tenant", """{"metadata":{"operationType":"create"},"current":{"id":"d1","tenant":"t1"}}""", "t1", CosmosDbPartitionKeyKind.String)]
	public void CarryThePartitionKeyOnEventsBuiltByTheAllVersionsProcessor(
		string partitionKeyPath,
		string documentJson,
		string expectedKey,
		CosmosDbPartitionKeyKind expectedKind)
	{
		// The second call site. Fixing one processor and leaving this one is how the two copies came to
		// disagree in the first place.
		using var client = new CosmosClient(ConnectionString);
		var processor = new CosmosDbAllVersionsChangeFeedProcessor(
			client,
			MsOptions.Create(new CosmosDbCdcOptions
			{
				ConnectionString = ConnectionString,
				ProcessorName = "partition-key-probe",
				DatabaseId = "cdc",
				ContainerId = "cdc-source",
				PartitionKeyPath = partitionKeyPath,
			}),
			MsOptions.Create(new CosmosDbAllVersionsChangeFeedOptions
			{
				ProcessorName = "partition-key-probe",
				LeaseContainer = "leases",
			}),
			NullLogger<CosmosDbAllVersionsChangeFeedProcessor>.Instance);

		using var document = JsonDocument.Parse(documentJson);

		var changeEvent = InvokeParseAllVersionsChangeEvent(processor, document);

		changeEvent.PartitionKey.ShouldBe(expectedKey);
		changeEvent.PartitionKeyKind.ShouldBe(expectedKind);
	}

	private static string? Extract(JsonDocument document, string? path, out CosmosDbPartitionKeyKind kind) =>
		CosmosDbPartitionKeyExtractor.Extract(document.RootElement, path, out kind);

	private static CosmosDbDataChangeEvent InvokeCreateChangeEvent(
		CosmosDbCdcProcessor processor,
		JsonDocument document)
	{
		var method = typeof(CosmosDbCdcProcessor).GetMethod(
			"CreateChangeEvent",
			BindingFlags.Instance | BindingFlags.NonPublic);

		method.ShouldNotBeNull(
			"the arm drives the processor's real event construction; if it was renamed, point this at "
			+ "the new name rather than deleting the arm.");

		return Invoke(method, processor, [document, CosmosDbCdcPosition.Beginning()]);
	}

	private static CosmosDbDataChangeEvent InvokeParseAllVersionsChangeEvent(
		CosmosDbAllVersionsChangeFeedProcessor processor,
		JsonDocument document)
	{
		var method = typeof(CosmosDbAllVersionsChangeFeedProcessor).GetMethod(
			"ParseAllVersionsChangeEvent",
			BindingFlags.Instance | BindingFlags.NonPublic);

		method.ShouldNotBeNull(
			"the arm drives the processor's real event construction; if it was renamed, point this at "
			+ "the new name rather than deleting the arm.");

		return Invoke(method, processor, [document]);
	}

	private static CosmosDbDataChangeEvent Invoke(MethodInfo method, object target, object?[] arguments)
	{
		try
		{
			return (CosmosDbDataChangeEvent)method.Invoke(target, arguments)!;
		}
		catch (TargetInvocationException ex) when (ex.InnerException is not null)
		{
			// Surface the real failure — the original defect was an InvalidOperationException from
			// GetString(), and a TargetInvocationException wrapper would hide which arm actually broke.
			throw ex.InnerException;
		}
	}
}
