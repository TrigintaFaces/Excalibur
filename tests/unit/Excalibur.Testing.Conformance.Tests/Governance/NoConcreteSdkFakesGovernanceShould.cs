// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Excalibur.Tests.Governance;

/// <summary>
/// Governance boundary test enforcing the ADR-142 §D7 rule: tests must not
/// take <see cref="FakeItEasy.A.Fake{T}"/> dependencies on concrete class types
/// from third-party SDK namespaces. The seam-adapter pattern (e.g.,
/// <c>ISecretClient</c>, <c>IServiceBusClient</c>) is the only supported
/// way to substitute those SDKs in tests.
/// </summary>
/// <remarks>
/// <para>
/// Per OVERWATCH msg 1733 (S798 task-515), this test uses a declarative
/// namespace-prefix rubric rather than a blocklist of individual types: any
/// new SDK namespace appearing in the codebase is automatically covered if
/// its prefix is listed here. Interfaces under those namespaces are
/// FakeItEasy-safe and are not flagged (e.g., <c>IAmazonS3</c>,
/// <c>IConnectionMultiplexer</c>, <c>IMongoClient</c>).
/// </para>
/// <para>
/// The check is implemented as a source-text scan of <c>tests/**/*.cs</c>
/// because compile-time analysis across arbitrary test assemblies would
/// require IL introspection or a Roslyn analyzer. Source-text scanning
/// catches both fully-qualified (<c>A.Fake&lt;Azure.Foo.Bar&gt;()</c>) and
/// using-resolved (<c>using Azure.Foo; ... A.Fake&lt;Bar&gt;()</c>) forms.
/// </para>
/// <para>
/// Exceptions (e.g., if a seam adapter's own tests legitimately need to fake
/// an SDK concrete for a real-SDK passthrough conformance smoke) should be
/// added to <see cref="ExceptionTypeFullNames"/> with an ADR-142 §D7
/// justification in the PR description.
/// </para>
/// </remarks>
[Trait("Category", "Conformance")]
[Trait("Component", "Governance")]
public sealed class NoConcreteSdkFakesGovernanceShould
{
	/// <summary>
	/// Namespace prefixes that shelter third-party SDK concrete classes with
	/// the non-virtual-overload fragility pattern that motivated ADR-142 §D7.
	/// </summary>
	private static readonly ImmutableArray<string> ForbiddenNamespacePrefixes =
	[
		"Azure.",
		"Amazon.",
		"AWSSDK.",
		"Google.Cloud.",
		"Elastic.Clients.",
		"Confluent.Kafka.",
		"RabbitMQ.Client.",
		"MongoDB.Driver.",
		"StackExchange.Redis.",
	];

	/// <summary>
	/// Tracked <b>debt baseline</b> of pre-existing fake sites that predate
	/// the ADR-142 §D7 guardrail. Each entry is <b>not</b> §D7-exempt — it is
	/// scheduled for drain via incremental seam migration (S799+). Per COMPASS
	/// ruling msg 1743: "rename the list <c>_sdkFakeDebtBaseline</c> — must not
	/// read 'permitted'."
	/// </summary>
	/// <remarks>
	/// <para>
	/// Baseline seeded from COMPASS A1 inventory (S798 msg 1646) + A2 triage
	/// (msg 1705). Each entry pre-ratified under §D7 governance — no
	/// retroactive per-entry PR justification needed; CHRONICLE will lift the
	/// list into an ADR-142 §D7 amendment during DOCS phase.
	/// </para>
	/// <para>
	/// <b>Ratchet semantics</b>: the baseline must only shrink. Any <b>new</b>
	/// violation outside this set fails the guardrail. See
	/// <c>Debt_Baseline_Count_Only_Shrinks</c> test for the enforcement.
	/// </para>
	/// <para>S799 drain tracking: a Beads task tracks this list as a checklist
	/// to drain to empty.</para>
	/// </remarks>
	private static readonly ImmutableHashSet<string> SdkFakeDebtBaseline =
		ImmutableHashSet.Create(
			// === Azure ===
			// DI passthrough, never intercepted (A1 LOW-RISK).
			"Azure.Storage.Blobs.BlobServiceClient",
			// S799 defer — A2 msg 1705 ranked these below ServiceBusClient seam.
			// ServiceBusDLQ test currently keeps one ServiceBusClient fake for a
			// null-guard ctor test; CRUCIBLE will remove in a follow-up pass.
			"Azure.Messaging.ServiceBus.ServiceBusClient",
			"Azure.Messaging.ServiceBus.ServiceBusSender",
			"Azure.Messaging.ServiceBus.ServiceBusReceiver",
			"Azure.Messaging.ServiceBus.ServiceBusProcessor",
			// S799 defer — A2 rank #5 ArmClient (lower-traffic surface).
			"Azure.ResourceManager.ArmClient",

			// === Google Cloud ===
			// DI passthrough / builder registration tests.
			"Google.Cloud.Storage.V1.StorageClient",
			"Google.Cloud.Firestore.FirestoreClient",
			// S799 defer — A2 rank #6+#7 PubSub seam work.
			"Google.Cloud.PubSub.V1.SubscriberClient",
			"Google.Cloud.PubSub.V1.PublisherServiceApiClient",
			"Google.Cloud.PubSub.V1.SubscriberServiceApiClient");

			// === Elastic (fully drained in S799-A5, bd-iqlx2p) ===
			// Both prior entries (`ElasticsearchClient`, `BulkResponseItem`)
			// were removed when the fake sites were migrated:
			//  • 6× A.Fake<ElasticsearchClient>() → real unconnected
			//    ElasticsearchClient instances. The ResilientElasticsearchClient
			//    circuit-breaker test uses a real client pointed at
			//    http://127.0.0.1:1 so Elastic.Transport raises a true
			//    TransportException — exercises the real failure-propagation
			//    path rather than a faked one.
			//  • 2× A.Fake<BulkResponseItem>() — dead code (item-level setup
			//    was never attached to any BulkResponse fake in either
			//    ElasticsearchUnitTestBase) removed; reinstate with v8's
			//    BulkResponseItemBase only when a concrete test demands
			//    item-level assertions.
			// Data-shaped Elastic DTOs (Request/Response/Document/…) continue
			// to be allowed through the DataShapedTypeSuffixes rubric below.

	/// <summary>
	/// Immutable recorded count of the debt baseline — used by the ratchet
	/// assertion to detect unauthorized growth. Per COMPASS msg 1743 ruling:
	/// "guardrail test MUST fail if the baseline grows."
	/// </summary>
	private const int RecordedDebtBaselineCount = 11;

	/// <summary>
	/// Name suffixes that identify data-shaped DTO types in SDK namespaces.
	/// These types are property bags without non-virtual-overload fragility
	/// and are FakeItEasy-safe (same shape as <c>KeyVaultSecret</c> crossing
	/// the <c>ISecretClient</c> seam in the S797 template). Per COMPASS msg
	/// 1743 refined rubric — exclude from the guardrail flagging.
	/// </summary>
	private static readonly ImmutableArray<string> DataShapedTypeSuffixes =
	[
		"Response",
		"Request",
		"Result",
		"Options",
		"Settings",
		"Document",
		"Message",
		"Properties",
		"Info",
		"Data",
		"Record",
		"Event",
		"Args",
	];

	/// <summary>
	/// Matches <c>A.Fake&lt;TypeRef&gt;()</c> / <c>A.Fake&lt;TypeRef&gt;(</c>.
	/// Captures the type reference (may be simple or dotted, may carry a
	/// single level of generic args).
	/// </summary>
	private static readonly Regex FakeCallRegex = new(
		@"\bA\.Fake<\s*(?<type>[A-Za-z_][A-Za-z0-9_.]*(?:\s*<[^<>]*>)?)\s*>\s*\(",
		RegexOptions.Compiled);

	private static readonly Regex UsingRegex = new(
		@"^\s*using\s+(?<ns>[A-Za-z_][A-Za-z0-9_.]*)\s*;",
		RegexOptions.Compiled | RegexOptions.Multiline);

	[Fact]
	public void Tests_Do_Not_Fake_Concrete_Classes_From_Forbidden_SDK_Namespaces()
	{
		var repoRoot = FindRepoRoot();
		repoRoot.ShouldNotBeNull(
			"Could not locate repository root (walked up from test base directory looking for .git or .beads)");

		var testsRoot = Path.Combine(repoRoot, "tests");
		Directory.Exists(testsRoot).ShouldBeTrue(
			$"Expected tests/ directory at {testsRoot}");

		var violations = new List<Violation>();

		foreach (var csFile in EnumerateTestSourceFiles(testsRoot))
		{
			var text = File.ReadAllText(csFile);

			// Exclude this guardrail file itself — otherwise the regex samples
			// above would self-trigger.
			if (text.Contains("class NoConcreteSdkFakesGovernanceShould",
				StringComparison.Ordinal))
			{
				continue;
			}

			var fileUsings = UsingRegex
				.Matches(text)
				.Select(m => m.Groups["ns"].Value)
				.ToArray();

			foreach (Match fake in FakeCallRegex.Matches(text))
			{
				var typeRef = fake.Groups["type"].Value.Trim();
				var simpleName = GetSimpleName(typeRef);

				// Spec rule: T.IsClass && !T.IsInterface. Source-scanning can't
				// inspect IsInterface directly, but C# naming convention + common
				// practice both put an I-prefix on interface types. Skip those —
				// interfaces in forbidden namespaces (IAmazonS3, IMongoClient,
				// IConnectionMultiplexer, ILogger<T>) are FakeItEasy-safe per
				// the same spec.
				if (LooksLikeInterface(simpleName))
				{
					continue;
				}

				// Per COMPASS msg 1743 refined rubric: data-shaped DTOs
				// (Response/Request/Result/Options/Settings/Document/Message/
				// Properties/Info/Data/Record/Event/Args suffixes) are property
				// bags without non-virtual-overload fragility. Same shape as
				// KeyVaultSecret crossing the ISecretClient seam in the S797
				// template. Safe to fake.
				if (LooksLikeDataShapedDto(simpleName))
				{
					continue;
				}

				var resolvedNamespace = ResolveNamespace(typeRef, fileUsings);

				if (resolvedNamespace is null)
				{
					// Type reference couldn't be resolved against usings —
					// conservative: skip rather than false-flag.
					continue;
				}

				var forbiddenPrefixHit = ForbiddenNamespacePrefixes
					.FirstOrDefault(p => resolvedNamespace.StartsWith(p, StringComparison.Ordinal));
				if (forbiddenPrefixHit is null)
				{
					continue;
				}

				var fullyQualified = $"{resolvedNamespace}.{simpleName}";
				if (SdkFakeDebtBaseline.Contains(fullyQualified))
				{
					continue;
				}

				violations.Add(new Violation(
					File: Path.GetRelativePath(repoRoot, csFile),
					TypeRef: typeRef,
					ResolvedNamespace: resolvedNamespace,
					LineNumber: GetLineNumber(text, fake.Index)));
			}
		}

		if (violations.Count > 0)
		{
			var report = string.Join(
				Environment.NewLine,
				violations.Select(v => $"  {v.File}:{v.LineNumber}: A.Fake<{v.TypeRef}>  (resolves to {v.ResolvedNamespace})"));

			var message =
				$"Found {violations.Count} governance violations — tests must use internal seam adapters (e.g., ISecretClient, IServiceBusClient) instead of faking concrete SDK classes. See ADR-142 §D7." +
				Environment.NewLine +
				report;

			violations.ShouldBeEmpty(message);
		}
	}

	private static string? ResolveNamespace(string typeRef, IReadOnlyList<string> fileUsings)
	{
		// Fully-qualified form: return everything before the last '.'.
		var lastDot = typeRef.LastIndexOf('.');
		if (lastDot > 0)
		{
			return typeRef[..lastDot];
		}

		// Simple name: assume it could resolve to any of the file's usings.
		// Conservative policy: if any file using matches a forbidden prefix,
		// associate the simple name with that using. We accept false negatives
		// (simple name actually came from a non-forbidden using) but not false
		// positives (our algorithm never associates with a non-using namespace).
		foreach (var ns in fileUsings)
		{
			foreach (var prefix in ForbiddenNamespacePrefixes)
			{
				if (ns.StartsWith(prefix, StringComparison.Ordinal) || ns + "." == prefix)
				{
					return ns;
				}
			}
		}

		return null;
	}

	/// <summary>
	/// Heuristic: a simple name that matches the C# interface naming
	/// convention (<c>I</c> followed by an uppercase letter) is treated as
	/// an interface reference. Source-scanning cannot check
	/// <see cref="Type.IsInterface"/> directly; this heuristic catches the
	/// overwhelmingly dominant convention for BCL, SDK, and first-party
	/// interfaces (<c>IAmazonS3</c>, <c>IConnectionMultiplexer</c>,
	/// <c>IMongoClient</c>, <c>ILogger&lt;T&gt;</c>, <c>IDispatcher</c>, ...).
	/// </summary>
	/// <remarks>
	/// Per COMPASS msg 1743 constraint: the heuristic can produce false
	/// positives on classes with I-prefix names (<c>IntegrationClient</c>,
	/// <c>IdentityClient</c>, <c>IndexClient</c>). These hypothetical types
	/// are rare in the current SDK surface — the
	/// <see cref="Canary_Fixture_Classifies_Known_Samples_Correctly"/> test
	/// pins the current behavior so drift is detected.
	/// </remarks>
	private static bool LooksLikeInterface(string simpleName)
	{
		return simpleName.Length >= 2
			&& simpleName[0] == 'I'
			&& char.IsUpper(simpleName[1]);
	}

	/// <summary>
	/// Heuristic: a simple name ending with a data-shaped DTO suffix
	/// (<c>Response</c>, <c>Request</c>, <c>Result</c>, etc.) is a property
	/// bag and is FakeItEasy-safe regardless of namespace. Matches the
	/// <c>KeyVaultSecret</c> shape in the S797 <c>ISecretClient</c> template
	/// (per COMPASS msg 1743 refined rubric).
	/// </summary>
	private static bool LooksLikeDataShapedDto(string simpleName)
	{
		foreach (var suffix in DataShapedTypeSuffixes)
		{
			if (simpleName.EndsWith(suffix, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	private static string GetSimpleName(string typeRef)
	{
		var lastDot = typeRef.LastIndexOf('.');
		var baseName = lastDot > 0 ? typeRef[(lastDot + 1)..] : typeRef;

		// Strip generic args if present.
		var genericOpen = baseName.IndexOf('<', StringComparison.Ordinal);
		return genericOpen > 0 ? baseName[..genericOpen].Trim() : baseName.Trim();
	}

	private static int GetLineNumber(string text, int index)
	{
		var line = 1;
		for (var i = 0; i < index; i++)
		{
			if (text[i] == '\n')
			{
				line++;
			}
		}

		return line;
	}

	private static IEnumerable<string> EnumerateTestSourceFiles(string testsRoot)
	{
		foreach (var path in Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
		{
			var sep = Path.DirectorySeparatorChar;
			if (path.Contains($"{sep}bin{sep}", StringComparison.Ordinal) ||
				path.Contains($"{sep}obj{sep}", StringComparison.Ordinal))
			{
				continue;
			}

			yield return path;
		}
	}

	private static string? FindRepoRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null)
		{
			if (Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
				Directory.Exists(Path.Combine(dir.FullName, ".beads")))
			{
				return dir.FullName;
			}

			dir = dir.Parent;
		}

		return null;
	}

	/// <summary>
	/// Ratchet assertion per COMPASS msg 1743: the debt baseline must only
	/// shrink as S799 seam migrations land. A grown baseline is a regression.
	/// </summary>
	[Fact]
	public void Debt_Baseline_Count_Only_Shrinks()
	{
		SdkFakeDebtBaseline.Count.ShouldBeLessThanOrEqualTo(
			RecordedDebtBaselineCount,
			"S798 ratchet: SDK-fake debt baseline must only shrink as seam migrations land. " +
			"If you intentionally added a new entry, also update " +
			$"{nameof(RecordedDebtBaselineCount)} — but first file an ADR-142 §D7 justification. " +
			"Expected shrinkage path is S799 drain Beads tracking this list.");
	}

	/// <summary>
	/// Canary fixture per COMPASS msg 1743: pins the classification behavior
	/// of the <see cref="LooksLikeInterface"/> and
	/// <see cref="LooksLikeDataShapedDto"/> heuristics so drift is detected.
	/// If the rules evolve, update the fixture intentionally.
	/// </summary>
	[Theory]
	[InlineData("IAmazonS3", true, false, "interface — I-prefix + uppercase")]
	[InlineData("ILogger", true, false, "interface — BCL")]
	[InlineData("IConnectionMultiplexer", true, false, "interface — StackExchange.Redis")]
	[InlineData("IMongoClient", true, false, "interface — MongoDB.Driver")]
	[InlineData("ElasticsearchClient", false, false, "concrete SDK client — should be flagged")]
	[InlineData("ServiceBusClient", false, false, "concrete SDK client — should be flagged")]
	[InlineData("BlobServiceClient", false, false, "concrete SDK client — in debt baseline")]
	[InlineData("SearchResponse", false, true, "DTO — ends with Response")]
	[InlineData("BulkResponseItem", false, false, "composite name ending Item — NOT in suffix list, flagged")]
	[InlineData("IndexResponse", false, true, "DTO — ends with Response")]
	[InlineData("ReindexRequest", false, true, "DTO — ends with Request")]
	[InlineData("PutMappingRequest", false, true, "DTO — ends with Request")]
	[InlineData("KeyVaultSecret", false, false, "concrete data type — not in suffix list (S797 allows via seam)")]
	[InlineData("IndexLifecycleManager", false, false, "internal adapter-like name, not I-prefix + uppercase")]
	// Note: COMPASS msg 1743 flagged IntegrationClient/IdentityClient/IndexClient
	// as potential heuristic false positives. Analysis: these all have a
	// *lowercase* second char ('n', 'd') so the current heuristic (I + uppercase)
	// correctly returns false for them. The canary pins this.
	[InlineData("IntegrationClient", false, false, "lowercase 2nd char — heuristic correctly returns false")]
	[InlineData("IdentityClient", false, false, "lowercase 2nd char — heuristic correctly returns false")]
	[InlineData("IndexClient", false, false, "lowercase 2nd char — heuristic correctly returns false")]
	// A hypothetical class with I + uppercase 2nd char WOULD be a false
	// positive; no such SDK type is known in the current codebase.
	[InlineData("IBadlyNamedClass", true, false, "HYPOTHETICAL FALSE POSITIVE — I+uppercase pattern")]
	public void Canary_Fixture_Classifies_Known_Samples_Correctly(
		string simpleName,
		bool expectedInterface,
		bool expectedDto,
		string reason)
	{
		LooksLikeInterface(simpleName).ShouldBe(expectedInterface, reason);
		LooksLikeDataShapedDto(simpleName).ShouldBe(expectedDto, reason);
	}

	private sealed record Violation(
		string File,
		string TypeRef,
		string ResolvedNamespace,
		int LineNumber);
}
