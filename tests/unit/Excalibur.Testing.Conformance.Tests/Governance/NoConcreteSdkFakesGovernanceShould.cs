// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Excalibur.Tests.Governance;

/// <summary>
/// Rejects unsafe SDK fakes while permitting certified substitution contracts in
/// SDK adapter boundary tests. See ADR-142 §D7.
/// </summary>
/// <remarks>
/// Namespace candidates come from each source project's restored compile metadata,
/// rather than the first SDK using directive. This source scan is not a C# semantic
/// model: unresolved names are reported separately, and ambiguous SDK candidates
/// require explicit qualification. Supported boundary contracts have companion
/// exact-member tests; neither their types nor paths grow the inherited debt list.
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
			// S821: ServiceBusClient/Sender/Receiver/Processor drained — seam
			// interfaces IServiceBusSenderSeam, IServiceBusReceiverSeam,
			// IServiceBusProcessorSeam created per ADR-142 §D7. DLQ DI test
			// uses real unconnected ServiceBusClient instance.
			// S821: ArmClient drained — IArmClientSeam created in
			// Excalibur.Jobs.Azure.Internal per ADR-142 §D7.

			// === Google Cloud ===
			// S821: StorageClient method-intercepting sites drained —
			// IStorageClientSeam created in ClaimCheck.GoogleCloudStorage.Internal.
			// Remaining sites are DI passthrough / builder / ctor null-guard tests.
			"Google.Cloud.Storage.V1.StorageClient",
			// Correct historical namespace attribution for the same six builder passthrough sites.
			"Google.Cloud.Firestore.V1.FirestoreClient",
			// S821: SubscriberClient + PublisherServiceApiClient drained —
			// ISubscriberClientSeam and IPublisherClientSeam created in
			// Transport.GooglePubSub.Internal per ADR-142 §D7.
			// SubscriberServiceApiClient remains — 7-method surface across
			// receiver + DLQ admin ops; seam deferred as lower-priority.
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
	private const int RecordedDebtBaselineCount = 4;

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
		@"\bA\.Fake<\s*(?<type>(?:global::)?[A-Za-z_][A-Za-z0-9_.]*(?:\s*<[^<>]*>)?)\s*>\s*\(",
		RegexOptions.Compiled);

	private static readonly Regex UsingRegex = new(
		@"^\s*(?:global\s+)?using\s+(?:(?<alias>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*)?(?<ns>(?:global::)?[A-Za-z_][A-Za-z0-9_.]*)\s*;",
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
		var metadata = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
		var projectTypes = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
		var unresolved = new HashSet<string>(StringComparer.Ordinal);

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
				.Select(m => m.Groups["alias"].Success
					? $"{m.Groups["alias"].Value}={m.Groups["ns"].Value}"
					: m.Groups["ns"].Value)
				.ToArray();

			foreach (Match fake in FakeCallRegex.Matches(text))
			{
				var typeRef = fake.Groups["type"].Value.Trim();
				var resolvedType = ResolveSdkType(typeRef, fileUsings, GetProjectSdkTypes(csFile, projectTypes, metadata));
				var resolvedNamespace = resolvedType is null ? null : resolvedType[..resolvedType.LastIndexOf('.')];

				if (resolvedNamespace is null)
				{
					// A non-SDK or unresolved reference is not an invented SDK type.
					unresolved.Add(typeRef);
					continue;
				}

				// Classify the actual metadata name, never an alias chosen to resemble an interface or DTO.
				var simpleName = GetSimpleName(resolvedType!);
				if (LooksLikeInterface(simpleName) || LooksLikeDataShapedDto(simpleName))
				{
					continue;
				}

				if (!IsSdkNamespace(resolvedNamespace))
				{
					continue;
				}

				var fullyQualified = resolvedType!;
				if (SdkFakeDebtBaseline.Contains(fullyQualified) ||
					IsSupportedAdapterContract(fullyQualified, Path.GetRelativePath(repoRoot, csFile)))
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

		Console.WriteLine($"SDK metadata scan: {unresolved.Count} non-SDK or unresolved type references; this is not whole-program semantic coverage.");

		if (violations.Count > 0)
		{
			var report = string.Join(
				Environment.NewLine,
				violations.Select(v => $"  {v.File}:{v.LineNumber}: A.Fake<{v.TypeRef}>  (resolves to {v.ResolvedNamespace})"));

			var message =
				$"Found {violations.Count} governance violations — use framework seams except certified SDK adapter substitution contracts. See ADR-142 §D7." +
				Environment.NewLine +
				report;

			violations.ShouldBeEmpty(message);
		}
	}

	private static bool IsSupportedAdapterContract(string typeName, string file)
	{
		file = file.Replace('\\', '/');
		return typeName switch
		{
			"Azure.Messaging.ServiceBus.ServiceBusClient" or "Azure.Messaging.ServiceBus.ServiceBusSender" =>
				file == "tests/unit/Excalibur.Dispatch.Transport.Tests/NamedRemoteMessageBusShould.cs",
			"Google.Cloud.PubSub.V1.PublisherClient" =>
				file is "tests/unit/Excalibur.Dispatch.Transport.Tests/GooglePubSub/GooglePubSubNamedMessageBusShould.cs" or
					"tests/unit/Excalibur.Dispatch.Transport.Tests/GooglePubSub/NamedPublisherCleanupShould.cs",
			"Azure.Security.KeyVault.Keys.KeyClient" or "Azure.Security.KeyVault.Keys.DeleteKeyOperation" =>
				file is "tests/unit/Excalibur.Compliance.Tests/Azure/AnAmbiguousAzureVersionNumberIsRefusedShould.cs" or
					"tests/unit/Excalibur.Compliance.Tests/Azure/AnUndatedAzureKeyReportsNoInstantShould.cs" or
					"tests/unit/Excalibur.Compliance.Tests/Azure/AzureKeyVaultErasureReachesTerminalShould.cs" or
					"tests/unit/Excalibur.Compliance.Tests/Azure/AzureKeyVaultProviderShould.cs",
			"Azure.Storage.Blobs.BlobContainerClient" or "Azure.Storage.Blobs.BlobClient" =>
				file is "tests/unit/Excalibur.Dispatch.Patterns.Tests/Azure/AzureBlobClaimCheckProviderDepthShould.cs" or
					"tests/unit/Excalibur.Dispatch.Patterns.Tests/Azure/AzureBlobClaimCheckProviderExpiryShould.cs",
			_ => false,
		};
	}

	private static string? ResolveNamespace(string typeRef, IReadOnlyList<string> fileUsings, HashSet<string> sdkTypes)
	{
		var type = ResolveSdkType(typeRef, fileUsings, sdkTypes);
		return type is null ? null : type[..type.LastIndexOf('.')];
	}

	private static string? ResolveSdkType(string typeRef, IReadOnlyList<string> fileUsings, HashSet<string> sdkTypes)
	{
		typeRef = typeRef.Replace("global::", string.Empty, StringComparison.Ordinal);
		foreach (var import in fileUsings)
		{
			var separator = import.IndexOf('=', StringComparison.Ordinal);
			if (separator < 0)
			{
				continue;
			}
			var alias = import[..separator];
			if (typeRef == alias || typeRef.StartsWith(alias + ".", StringComparison.Ordinal))
			{
				typeRef = import[(separator + 1)..].Replace("global::", string.Empty, StringComparison.Ordinal) + typeRef[alias.Length..];
				break;
			}
		}
		var genericOpen = typeRef.IndexOf('<', StringComparison.Ordinal);
		if (genericOpen >= 0)
		{
			var arguments = typeRef[(genericOpen + 1)..typeRef.LastIndexOf('>')];
			typeRef = typeRef[..genericOpen].Trim() + "`" + (arguments.Count(character => character == ',') + 1);
		}
		var candidates = new HashSet<string>(StringComparer.Ordinal);
		if (sdkTypes.Contains(typeRef))
		{
			candidates.Add(typeRef);
		}
		foreach (var import in fileUsings.Where(value => !value.Contains('=', StringComparison.Ordinal)))
		{
			var candidate = import.Replace("global::", string.Empty, StringComparison.Ordinal) + "." + typeRef;
			if (sdkTypes.Contains(candidate))
			{
				candidates.Add(candidate);
			}
		}
		if (candidates.Count > 1)
		{
			throw new InvalidOperationException($"Ambiguous SDK type {typeRef}: {string.Join(", ", candidates)}. Fully qualify the fake type.");
		}
		return candidates.SingleOrDefault();
	}

	private static HashSet<string> GetProjectSdkTypes(string source, Dictionary<string, HashSet<string>> projects,
		Dictionary<string, HashSet<string>> assemblies)
	{
		var directory = new DirectoryInfo(Path.GetDirectoryName(source)!);
		while (directory is not null && !directory.EnumerateFiles("*.csproj").Any())
		{
			directory = directory.Parent;
		}
		if (directory is null)
		{
			throw new InvalidOperationException($"No owning project for {source}.");
		}
		if (projects.TryGetValue(directory.FullName, out var cached))
		{
			return cached;
		}
		var assetsPath = Path.Combine(directory.FullName, "obj", "project.assets.json");
		if (!File.Exists(assetsPath))
		{
			throw new InvalidOperationException($"Restore {directory.FullName} before scanning SDK metadata: {assetsPath} is missing.");
		}
		using var assets = JsonDocument.Parse(File.ReadAllText(assetsPath));
		var root = assets.RootElement;
		var types = new HashSet<string>(StringComparer.Ordinal);
		foreach (var target in SelectCompileTarget(root))
		{
			foreach (var library in target.Value.EnumerateObject())
			{
				if (library.Value.GetProperty("type").GetString() != "package" ||
					!library.Value.TryGetProperty("compile", out var compile) ||
					!root.GetProperty("libraries").GetProperty(library.Name).TryGetProperty("path", out var package))
				{
					continue;
				}
				foreach (var reference in compile.EnumerateObject().Where(item => item.Name.EndsWith(".dll", StringComparison.Ordinal)))
				{
					var path = root.GetProperty("packageFolders").EnumerateObject()
						.Select(folder => Path.Combine(folder.Name, package.GetString()!, reference.Name))
						.FirstOrDefault(File.Exists) ?? throw new InvalidOperationException($"Missing restored compile asset {library.Name}/{reference.Name}.");
					if (!assemblies.TryGetValue(path, out var definitions))
					{
						definitions = new HashSet<string>(StringComparer.Ordinal);
						using var stream = File.OpenRead(path);
						using var pe = new PEReader(stream);
						var reader = pe.GetMetadataReader();
						foreach (var handle in reader.TypeDefinitions)
						{
							var definition = reader.GetTypeDefinition(handle);
							var ns = reader.GetString(definition.Namespace);
							if (IsSdkNamespace(ns))
							{
								definitions.Add(ns + "." + reader.GetString(definition.Name));
							}
						}
						assemblies.Add(path, definitions);
					}
					types.UnionWith(definitions);
				}
			}
		}
		projects.Add(directory.FullName, types);
		return types;
	}

	private static bool IsSdkNamespace(string value) => ForbiddenNamespacePrefixes.Any(prefix =>
		value.StartsWith(prefix, StringComparison.Ordinal) || value == prefix.TrimEnd('.'));

	private static IEnumerable<JsonProperty> SelectCompileTarget(JsonElement root)
	{
		var targets = root.GetProperty("targets").EnumerateObject().ToArray();
		var selected = targets.Where(target => target.Name == "net10.0").ToArray();
		if (selected.Length == 1)
		{
			return selected;
		}
		if (targets.Length != 1)
		{
			throw new InvalidOperationException("Select a compile target explicitly for this multitarget SDK metadata scan.");
		}
		return targets;
	}

	[Theory]
	[InlineData("IClient")]
	[InlineData("ClientResponse")]
	public void ClassifyTheActualSdkTypeInsteadOfItsDisguisedAlias(string alias)
	{
		var resolved = ResolveSdkType(alias, [$"{alias}=Azure.Messaging.ServiceBus.ServiceBusClient"],
			["Azure.Messaging.ServiceBus.ServiceBusClient"]).ShouldNotBeNull();
		var name = GetSimpleName(resolved);
		LooksLikeInterface(name).ShouldBeFalse();
		LooksLikeDataShapedDto(name).ShouldBeFalse();
		IsSupportedAdapterContract(resolved, "tests/unit/OtherShould.cs").ShouldBeFalse();
	}

	[Theory]
	[InlineData("ProducerBuilder<string, string>", "Confluent.Kafka.ProducerBuilder`2", false)]
	[InlineData("IProducer<string, string>", "Confluent.Kafka.IProducer`2", true)]
	public void ResolveGenericSdkArityWithoutLosingConcreteTypeEnforcement(string reference, string expected, bool isInterface)
	{
		var type = ResolveSdkType(reference, ["Confluent.Kafka"],
			["Confluent.Kafka.ProducerBuilder`2", "Confluent.Kafka.IProducer`2"]).ShouldNotBeNull();
		type.ShouldBe(expected);
		LooksLikeInterface(GetSimpleName(type)).ShouldBe(isInterface);
		IsSupportedAdapterContract(type, "tests/unit/OtherShould.cs").ShouldBeFalse();
	}

	[Theory]
	[InlineData("ServiceBusClient", "Azure.Messaging.ServiceBus")]
	[InlineData("Bus.ServiceBusClient", "Azure.Messaging.ServiceBus")]
	[InlineData("Alias", "Azure.Messaging.ServiceBus")]
	[InlineData("global::Azure.Messaging.ServiceBus.ServiceBusClient", "Azure.Messaging.ServiceBus")]
	public void ResolveActualSdkTypeInsteadOfTheFirstImportedNamespace(string reference, string expected)
	{
		ResolveNamespace(reference, ["Amazon.SQS", "Azure.Messaging.ServiceBus", "Bus=Azure.Messaging.ServiceBus", "Alias=global::Azure.Messaging.ServiceBus.ServiceBusClient"],
			["Azure.Messaging.ServiceBus.ServiceBusClient", "Amazon.SQS.AmazonSQSClient"]).ShouldBe(expected);
	}

	[Fact]
	public void RefuseAmbiguousSdkCandidatesRatherThanInventingTheirNamespace()
	{
		Should.Throw<InvalidOperationException>(() => ResolveNamespace("Client", ["Azure.One", "Azure.Two"],
			["Azure.One.Client", "Azure.Two.Client"]));
		ResolveNamespace("Unknown", ["Amazon.SQS"], ["Amazon.SQS.AmazonSQSClient"]).ShouldBeNull();
	}

	[Theory]
	[InlineData("Azure.Messaging.ServiceBus.ServiceBusClient", "tests/unit/Excalibur.Dispatch.Transport.Tests/NamedRemoteMessageBusShould.cs", true)]
	[InlineData("Google.Cloud.PubSub.V1.PublisherClient", "tests/unit/Excalibur.Dispatch.Transport.Tests/GooglePubSub/NamedPublisherCleanupShould.cs", true)]
	[InlineData("Google.Cloud.PubSub.V1.PublisherClient", "tests/unit/OtherShould.cs", false)]
	[InlineData("Azure.Security.KeyVault.Keys.KeyClient", "tests/unit/OtherShould.cs", false)]
	[InlineData("Azure.Storage.Blobs.BlobClient", "tests/unit/Excalibur.Dispatch.Transport.Tests/NamedRemoteMessageBusShould.cs", false)]
	[InlineData("Elastic.Clients.Elasticsearch.ElasticsearchClient", "tests/unit/Excalibur.Dispatch.Transport.Tests/NamedRemoteMessageBusShould.cs", false)]
	public void LimitSupportedContractsToTheirActualAdapterFixtures(string type, string file, bool expected)
	{
		IsSupportedAdapterContract(type, file).ShouldBe(expected);
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
		var metadataArity = baseName.IndexOf('`', StringComparison.Ordinal);
		return genericOpen > 0 ? baseName[..genericOpen].Trim() :
			metadataArity > 0 ? baseName[..metadataArity].Trim() : baseName.Trim();
	}

	[Fact]
	public void PreserveGenericDtoClassificationWithoutAdmittingGenericSdkClients()
	{
		LooksLikeDataShapedDto(GetSimpleName("Elastic.Clients.Elasticsearch.SearchResponse`1")).ShouldBeTrue();
		LooksLikeDataShapedDto(GetSimpleName("Confluent.Kafka.ProducerBuilder`2")).ShouldBeFalse();
	}

	[Fact]
	public void IndexRootNamespaceSdkTypesFromTheOwningProject()
	{
		var root = FindRepoRoot().ShouldNotBeNull();
		var source = Path.Combine(root, "tests/unit/Excalibur.Dispatch.Transport.Tests/NamedRemoteMessageBusShould.cs");
		var types = GetProjectSdkTypes(source, new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase),
			new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase));
		types.Contains("Confluent.Kafka.ProducerBuilder`2").ShouldBeTrue("The restored Kafka builder must be indexed at its namespace root");
		types.Contains("RabbitMQ.Client.ConnectionFactory").ShouldBeTrue("The restored RabbitMQ factory must be indexed at its namespace root");
		ResolveSdkType("ProducerBuilder<string, string>", ["Confluent.Kafka"], types)
			.ShouldBe("Confluent.Kafka.ProducerBuilder`2");
		ResolveSdkType("ConnectionFactory", ["RabbitMQ.Clientx"], types).ShouldBeNull();
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
	[InlineData("ElasticsearchClient", false, false, "class-shaped SDK client — admission depends on certified adapter contract")]
	[InlineData("ServiceBusClient", false, false, "class-shaped SDK client — admission depends on certified adapter contract")]
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
