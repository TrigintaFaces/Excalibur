// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Text;

using Excalibur.Data.Persistence;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Deterministic (no real-infra) regression guard for bead <c>7tdbiu</c>: the framework-owned
/// <see cref="CosmosDbPersistenceProvider"/> MUST configure its Cosmos SDK client so the default
/// serializer emits Cosmos's required lowercase <c>id</c> (camelCase) for a generic <c>TDocument</c>
/// with a PascalCase <c>Id</c> property.
/// </summary>
/// <remarks>
/// <para>
/// <b>The bug this catches (store-owned axis):</b> the provider builds its OWN <see cref="CosmosClient"/>,
/// so the framework controls the serializer end-to-end. Pre-fix, <c>CreateClientOptions()</c> set no
/// <c>SerializerOptions</c>, so the SDK-v3 default (Newtonsoft, no naming policy) emitted PascalCase →
/// <c>TDocument.Id</c> serialized to <c>"Id"</c>, missing Cosmos's lowercase <c>"id"</c> → point-read-by-id
/// NotFound / auto-GUID id → the serialize→store→reload round-trip broke. The fix sets
/// <c>SerializerOptions.PropertyNamingPolicy = CamelCase</c>.
/// </para>
/// <para>
/// <b>Emitted-key (not attribute/config-presence), per the S855/i2eabb lesson:</b> this drives the SAME
/// serializer the SDK uses for a <c>CosmosPropertyNamingPolicy</c> — Newtonsoft with a
/// <see cref="CamelCasePropertyNamesContractResolver"/> for CamelCase, a default resolver otherwise — using
/// the policy the provider ACTUALLY configures (read from its own <c>CreateClientOptions()</c>). It then
/// asserts the <b>emitted JSON key</b>, exactly as the i2eabb checkpoint guard reproduces the default
/// client's wire shape without a live emulator (Cosmos emulator round-trip = the 63xsiv-deferred lock).
/// </para>
/// <para>
/// <b>Non-vacuity:</b> on the pre-fix impl <c>SerializerOptions</c> is <c>null</c> → policy <c>None</c> →
/// the default resolver emits PascalCase <c>"Id"</c> → RED. Post-fix the configured CamelCase policy emits
/// <c>"id"</c> → GREEN. The private factory is reached via reflection (internal-first; no production
/// visibility widened).
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
[Trait("Feature", "CosmosDb")]
public sealed class CosmosDbPersistenceProviderSerializerNamingShould : UnitTestBase
{
	private sealed class SampleDocument
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
	}

	private static IOptions<CosmosDbOptions> ValidOptions() => Options.Create(new CosmosDbOptions
	{
		Client = new()
		{
			AccountEndpoint = "https://localhost:8081",
			AccountKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("not-a-real-account-key-for-tests")),
		},
		DatabaseName = "TestDb",
		Name = "TestCosmosDb",
	});

	[Fact]
	public void EmitLowercaseIdUnderTheProviderConfiguredDefaultSerializer()
	{
		var logger = A.Fake<ILogger<CosmosDbPersistenceProvider>>();
		var provider = new CosmosDbPersistenceProvider(ValidOptions(), logger);

		// Read the naming policy the provider actually configures on its own SDK client.
		var policy = GetConfiguredNamingPolicy(provider);

		// Drive the SAME serializer the Cosmos SDK uses for that policy and capture the emitted wire shape.
		var json = SerializeAsCosmosWould(new SampleDocument { Id = "doc-7tdbiu", Name = "n" }, policy);
		var keys = JObject.Parse(json).Properties().Select(p => p.Name).ToList();

		keys.ShouldContain(
			"id",
			$"7tdbiu — a store-owned TDocument.Id must emit Cosmos-required lowercase 'id' under the provider-configured default serializer. Emitted JSON: {json}");
		keys.ShouldNotContain(
			"Id",
			"7tdbiu — a PascalCase 'Id' means Cosmos's point-read by 'id' returns NotFound → the serialize→store→reload round-trip is silently broken.");
	}

	private static CosmosPropertyNamingPolicy GetConfiguredNamingPolicy(CosmosDbPersistenceProvider provider)
	{
		var createClientOptions = typeof(CosmosDbPersistenceProvider).GetMethod(
			"CreateClientOptions",
			BindingFlags.NonPublic | BindingFlags.Instance)
			?? throw new InvalidOperationException(
				"7tdbiu — CreateClientOptions() not found (the client-options factory was renamed; update the guard).");

		var clientOptions = (CosmosClientOptions)createClientOptions.Invoke(provider, null)!;

		// Pre-fix the provider leaves SerializerOptions null (SDK default = PascalCase); model that as Default.
		return clientOptions.SerializerOptions?.PropertyNamingPolicy ?? CosmosPropertyNamingPolicy.Default;
	}

	// Reproduces the wire shape the Cosmos SDK v3 default (Newtonsoft) serializer emits for the given policy:
	// CamelCase -> CamelCasePropertyNamesContractResolver; Default -> default resolver (PascalCase).
	private static string SerializeAsCosmosWould(object document, CosmosPropertyNamingPolicy policy)
	{
		var settings = new JsonSerializerSettings
		{
			ContractResolver = policy == CosmosPropertyNamingPolicy.CamelCase
				? new CamelCasePropertyNamesContractResolver()
				: new DefaultContractResolver(),
		};
		return JsonConvert.SerializeObject(document, settings);
	}
}
