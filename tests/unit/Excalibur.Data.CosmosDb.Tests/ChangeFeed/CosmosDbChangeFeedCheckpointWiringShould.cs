// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IO;
using System.Text.RegularExpressions;

namespace Excalibur.Data.CosmosDb.Tests.ChangeFeed;

/// <summary>
/// Persistent structural regression guard for bead <c>ydln24</c> (sprint 855 REVIEW_CODE BLOCKING):
/// the egwtku durable Cosmos change-feed continuation MUST be actually <b>wired</b>, not advertised-but-inert.
/// The shipped egwtku impl had the <c>LoadAsync</c>/<c>SaveAsync</c> calls in
/// <c>CosmosDbChangeFeedSubscription</c> but constructed all 3 subscriptions via the <b>3-arg ctor</b> →
/// <c>IChangeFeedCheckpointStore</c> never injected → <c>_checkpointStore</c> always null → the calls were
/// dead code (durable continuation INERT at runtime — the sprint's own advertised-but-unwired anti-pattern).
/// </summary>
/// <remarks>
/// <para>
/// This is the <b>correct-by-construction</b> half (source-scan, assembly-independent across the 3 Cosmos
/// packages, zero real-infra): it RED-proves the <i>inert</i> failure mode (no injection / missing helper /
/// wrong serialization). The runtime <i>behavioral</i> half — resume-after-processed at-least-once across a
/// real restart — stays the deferred real-Cosmos lock (<c>jattxa</c>, emulator expired).
/// </para>
/// <para>
/// <b>Non-vacuous:</b> fails if it cannot locate <c>src/</c> or any of the 3 construction-site files; RED if
/// a site reverts to the 3-arg ctor (drops <c>_checkpointStore</c>), the public activation helper is removed,
/// or the <c>[JsonPropertyName("id")]</c> is dropped (Cosmos requires lowercase <c>id</c>).
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Cdc")]
public sealed class CosmosDbChangeFeedCheckpointWiringShould
{
	// Each Cosmos store/provider that constructs a change-feed subscription and MUST inject the checkpoint store.
	private static readonly (string File, string SubscriptionType)[] Sites =
	[
		("CosmosDbPersistenceProvider.cs", "CosmosDbChangeFeedSubscription"),
		("CosmosDbOutboxStore.cs", "CosmosDbOutboxChangeFeedSubscription"),
		("CosmosDbEventStore.cs", "CosmosDbEventStoreChangeFeedSubscription"),
	];

	// The subscription implementations that own the read loop and persist the durable checkpoint. The
	// SaveAsync MUST be on the CONSUMER side, AFTER the page/batch has been yielded to (and pulled by) the
	// consumer — never before — so a crash resumes from BEFORE the unprocessed page (at-least-once), never
	// past it (the ydln24 data-loss fix #2). Each file must also LoadAsync on resume (not replay-from-start).
	private static readonly string[] SubscriptionImplFiles =
	[
		"CosmosDbChangeFeedSubscription.cs",
		"CosmosDbOutboxChangeFeedSubscription.cs",
		"CosmosDbEventStoreChangeFeedSubscription.cs",
	];

	[Fact]
	public void InjectTheCheckpointStoreAtAllThreeSubscriptionConstructionSites()
	{
		var srcRoot = LocateSrcRoot();
		_ = srcRoot.ShouldNotBeNull(
			"Could not locate the repository 'src/' directory; the egwtku wiring guard cannot run (refusing to pass vacuously).");

		var violations = new List<string>();
		foreach (var (file, subscriptionType) in Sites)
		{
			var path = Directory
				.EnumerateFiles(srcRoot, file, SearchOption.AllDirectories)
				.FirstOrDefault(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
								  && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
			_ = path.ShouldNotBeNull($"Construction-site file '{file}' not located under '{srcRoot}' (scan mis-located — refusing to pass vacuously).");

			var text = StripComments(File.ReadAllText(path));
			var construction = ExtractConstruction(text, subscriptionType);
			if (construction is null)
			{
				violations.Add($"{file}: no `new {subscriptionType}(...)` construction found.");
				continue;
			}

			// The fix injects the DI-supplied IChangeFeedCheckpointStore (field `_checkpointStore`) into the
			// subscription ctor. The pre-fix 3-arg ctor omitted it → durable continuation inert.
			if (!construction.Contains("_checkpointStore", StringComparison.Ordinal))
			{
				violations.Add(
					$"{file}: `new {subscriptionType}(...)` does NOT inject `_checkpointStore` (3-arg ctor) — "
					+ "durable change-feed continuation would be INERT (ydln24).");
			}
		}

		violations.ShouldBeEmpty(
			"ydln24 — every Cosmos change-feed subscription must be constructed WITH the injected checkpoint store:\n"
			+ string.Join("\n", violations));
	}

	[Fact]
	public void PersistTheDurableCheckpointOnlyAfterTheConsumerHasProcessedThePage()
	{
		var srcRoot = LocateSrcRoot();
		_ = srcRoot.ShouldNotBeNull(
			"Could not locate the repository 'src/' directory; the egwtku ordering guard cannot run (refusing to pass vacuously).");

		var violations = new List<string>();
		foreach (var file in SubscriptionImplFiles)
		{
			var path = Directory
				.EnumerateFiles(srcRoot, file, SearchOption.AllDirectories)
				.FirstOrDefault(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
								  && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
			_ = path.ShouldNotBeNull($"Subscription impl '{file}' not located under '{srcRoot}' (scan mis-located — refusing to pass vacuously).");

			var text = StripComments(File.ReadAllText(path));

			// (a) The durable checkpoint MUST be loaded on resume — otherwise the feed always replays from the
			//     configured start position (the original egwtku "continuation lost on restart" bug).
			if (!text.Contains("_checkpointStore.LoadAsync", StringComparison.Ordinal))
			{
				violations.Add($"{file}: never calls `_checkpointStore.LoadAsync(...)` — durable continuation would not resume (egwtku).");
			}

			// (b) The durable persist MUST come AFTER the consumer has pulled the page. We prove this
			//     structurally: the `_checkpointStore.SaveAsync(...)` call must appear textually AFTER the
			//     FIRST `yield return` in the file. The pre-fix bug persisted BEFORE the foreach-yield loop
			//     (at-most-once / silent skip on crash). ydln24 fix #2 / SA seam 17195.
			var firstYield = text.IndexOf("yield return", StringComparison.Ordinal);
			var saveIdx = text.IndexOf("_checkpointStore.SaveAsync", StringComparison.Ordinal);
			if (saveIdx < 0)
			{
				violations.Add($"{file}: never calls `_checkpointStore.SaveAsync(...)` — durable continuation never advances (egwtku).");
			}
			else if (firstYield < 0)
			{
				violations.Add($"{file}: no `yield return` found — cannot prove the persist-after-process ordering (scan mis-located).");
			}
			else if (saveIdx < firstYield)
			{
				violations.Add(
					$"{file}: `_checkpointStore.SaveAsync(...)` appears BEFORE the consumer `yield return` — "
					+ "the checkpoint would advance before any document is processed (at-most-once / silent skip on crash). ydln24 fix #2.");
			}
		}

		violations.ShouldBeEmpty(
			"ydln24 — the durable checkpoint must be loaded on resume and persisted only AFTER the consumer processes the page:\n"
			+ string.Join("\n", violations));
	}

	[Fact]
	public void ExposeAPublicDurableCheckpointStoreActivationHelper()
	{
		var ext = ReadSrcFile("CosmosDbServiceCollectionExtensions.cs");

		// Consumers must be able to turn durability ON (the store is internal; without a public helper it is
		// unreachable — advertised-but-unactivatable).
		ext.ShouldContain("AddCosmosDbChangeFeedCheckpointStore", Case.Sensitive,
			"ydln24 — a public AddCosmosDbChangeFeedCheckpointStore activation helper must exist.");
		ext.ShouldContain("new CosmosDbChangeFeedCheckpointStore", Case.Sensitive,
			"ydln24 — the activation helper must register the durable CosmosDbChangeFeedCheckpointStore (not only the InMemory default).");
	}

	[Fact]
	public void SerializeCheckpointDocumentIdAndPartitionKeyLowercaseUnderAnySerializer()
	{
		var store = StripComments(ReadSrcFile("CosmosDbChangeFeedCheckpointStore.cs"));

		// The store takes a CONSUMER-supplied Container, so the framework does not control the serializer. The
		// Cosmos SDK v3 DEFAULT serializer is Newtonsoft (STJ is opt-in), and Newtonsoft ignores STJ
		// [JsonPropertyName] → emits PascalCase. So BOTH the system 'id' AND the partition-key field
		// 'subscriptionId' (path /subscriptionId) must carry BOTH the STJ [JsonPropertyName] and the Newtonsoft
		// [JsonProperty] attribute to be lowercase under ANY serializer. STJ-only = silently inert on the default
		// client (i2eabb Facet-1 + Platform Facet-2). Behavioral proof: ChangeFeedCheckpointDocumentSerializationShould.
		var violations = new List<string>();

		// `JsonProperty\(` does NOT match `JsonPropertyName\(` (the latter is followed by `Name(`), so these
		// distinguish the Newtonsoft attribute from the STJ one.
		if (!Regex.IsMatch(store, @"JsonPropertyName\(""id""\)", RegexOptions.Compiled))
		{
			violations.Add("CheckpointDocument.Id missing STJ [JsonPropertyName(\"id\")].");
		}

		if (!Regex.IsMatch(store, @"JsonProperty\(""id""\)", RegexOptions.Compiled))
		{
			violations.Add("CheckpointDocument.Id missing Newtonsoft [JsonProperty(\"id\")] — PascalCase 'Id' under the default Newtonsoft serializer → point-read NotFound → durable continuation inert (i2eabb).");
		}

		if (!Regex.IsMatch(store, @"JsonPropertyName\(""subscriptionId""\)", RegexOptions.Compiled))
		{
			violations.Add("CheckpointDocument.SubscriptionId missing STJ [JsonPropertyName(\"subscriptionId\")].");
		}

		if (!Regex.IsMatch(store, @"JsonProperty\(""subscriptionId""\)", RegexOptions.Compiled))
		{
			violations.Add("CheckpointDocument.SubscriptionId (partition-key path /subscriptionId) missing Newtonsoft [JsonProperty(\"subscriptionId\")] — PascalCase 'SubscriptionId' under the default serializer → write/read PK mismatch (i2eabb Facet-2).");
		}

		violations.ShouldBeEmpty(
			"i2eabb — CheckpointDocument must serialize 'id' and 'subscriptionId' lowercase under BOTH the default Newtonsoft serializer and STJ:\n"
			+ string.Join("\n", violations));
	}

	private static string ReadSrcFile(string fileName)
	{
		var srcRoot = LocateSrcRoot();
		_ = srcRoot.ShouldNotBeNull("Could not locate the repository 'src/' directory (refusing to pass vacuously).");
		var path = Directory
			.EnumerateFiles(srcRoot, fileName, SearchOption.AllDirectories)
			.FirstOrDefault(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
							  && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
		_ = path.ShouldNotBeNull($"Source file '{fileName}' not located under '{srcRoot}' (refusing to pass vacuously).");
		return File.ReadAllText(path);
	}

	// Extracts the argument text of the first `new Foo(` / `new Foo<...>(` … `)` call via paren matching
	// (comment-stripped input). Tolerates an optional generic clause between the type name and the '('
	// (e.g. `new CosmosDbChangeFeedSubscription<TDocument>(`), which the literal `new Foo(` marker missed.
	private static string? ExtractConstruction(string text, string typeName)
	{
		var marker = $"new {typeName}";
		var searchFrom = 0;
		int start;
		int open = -1;
		while ((start = text.IndexOf(marker, searchFrom, StringComparison.Ordinal)) >= 0)
		{
			// The char after the type name must begin a ctor call: either '(' directly or a '<...>' generic
			// clause that resolves to '(' (no parens occur inside a generic argument list).
			var i = start + marker.Length;
			if (i < text.Length && text[i] == '<')
			{
				var genDepth = 0;
				for (; i < text.Length; i++)
				{
					if (text[i] == '<')
					{
						genDepth++;
					}
					else if (text[i] == '>')
					{
						genDepth--;
						if (genDepth == 0)
						{
							i++;
							break;
						}
					}
				}
			}

			if (i < text.Length && text[i] == '(')
			{
				open = i;
				break;
			}

			searchFrom = start + marker.Length;
		}

		if (open < 0)
		{
			return null;
		}

		var depth = 0;
		for (var i = open; i < text.Length; i++)
		{
			if (text[i] == '(')
			{
				depth++;
			}
			else if (text[i] == ')')
			{
				depth--;
				if (depth == 0)
				{
					return text[open..(i + 1)];
				}
			}
		}

		return null;
	}

	private static string StripComments(string text)
	{
		text = Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
		text = Regex.Replace(text, @"//[^\n]*", string.Empty);
		return text;
	}

	private static string? LocateSrcRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null)
		{
			var src = Path.Combine(dir.FullName, "src");
			var tests = Path.Combine(dir.FullName, "tests");
			if (Directory.Exists(src) && Directory.Exists(tests))
			{
				return src;
			}

			dir = dir.Parent;
		}

		return null;
	}
}
