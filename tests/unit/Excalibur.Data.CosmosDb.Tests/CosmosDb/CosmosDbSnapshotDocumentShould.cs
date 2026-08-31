// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch;
using System.Text.Json.Serialization;

using Excalibur.Data.CosmosDb.Snapshots;

namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Unit tests for the <see cref="CosmosDbSnapshotDocument"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.3): CosmosDB unit tests.
/// Tests verify document properties and the CreateId factory method.
/// Note: CosmosDbSnapshotDocument is internal, so we use reflection to test it.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "CosmosDb")]
[Trait("Feature", "Snapshots")]
public sealed class CosmosDbSnapshotDocumentShould
{
	private readonly Type _documentType;

	public CosmosDbSnapshotDocumentShould()
	{
		// Get the internal type via reflection
		var assembly = typeof(CosmosDbSnapshotStoreOptions).Assembly;
		_documentType = assembly.GetType("Excalibur.Data.CosmosDb.Snapshots.CosmosDbSnapshotDocument")!;
	}

	#region CreateId Tests

	// CreateId takes the tenant partition as a REQUIRED argument. The tenant-less single-argument
	// overload is gone: the store always resolves the partition from the total tenant term, so it never
	// produced the tenant-less shape, and a factory that could still emit one only offered an id no read
	// path composes.
	private string CreateId(string aggregateId, string tenantPartition)
	{
		var method = _documentType.GetMethod(
			"CreateId", BindingFlags.Public | BindingFlags.Static, binder: null,
			types: [typeof(string), typeof(string)], modifiers: null);

		method.ShouldNotBeNull("CreateId(aggregateId, tenantPartition) must exist.");
		return (string)method.Invoke(null, [aggregateId, tenantPartition])!;
	}

	[Fact]
	public void CreateId_HasNoTenantLessOverload()
	{
		// The invariant this file exists to hold: one id shape per document. A second, tenant-omitting form
		// is what lets a read and a write disagree about which of two shapes to address.
		_documentType.GetMethod(
				"CreateId", BindingFlags.Public | BindingFlags.Static, binder: null,
				types: [typeof(string)], modifiers: null)
			.ShouldBeNull("A tenant-less CreateId would emit a document id no read path composes.");

		// Positive control: the reflection query itself discriminates, so the null above is a real absence
		// rather than a lookup that could never have matched.
		_documentType.GetMethod(
				"CreateId", BindingFlags.Public | BindingFlags.Static, binder: null,
				types: [typeof(string), typeof(string)], modifiers: null)
			.ShouldNotBeNull();
	}

	[Fact]
	public void CreateId_ReturnsUrlSafeBase64()
	{
		var result = CreateId("test-aggregate-123", "tenant-a");

		result.ShouldNotContain("+");
		result.ShouldNotContain("/");
		result.ShouldNotContain("=");
	}

	[Fact]
	public void CreateId_HandlesSpecialCharacters()
	{
		// Characters that need escaping in a Cosmos id: / \ ? #
		var result = CreateId("order/123\test?query#fragment", "tenant-a");

		result.ShouldNotContain("+");
		result.ShouldNotContain("/");
		result.ShouldNotContain("=");
	}

	[Fact]
	public void CreateId_ProducesConsistentResults()
	{
		CreateId("my-aggregate-id", "tenant-a").ShouldBe(CreateId("my-aggregate-id", "tenant-a"));
	}

	[Fact]
	public void CreateId_CarriesTheTenantSegment_ForEveryPartition()
	{
		// LIVENESS: a real tenant and the untenanted partition both yield a well-formed, URL-safe id.
		// Without this arm a builder returning the empty string for everything satisfies the safety arm.
		foreach (var partition in new[] { "tenant-a", TenantScope.UntenantedSentinel })
		{
			var id = CreateId("shared-aggregate", partition);

			id.ShouldNotBeNullOrEmpty();
			id.ShouldNotContain("+");
			id.ShouldNotContain("/");
			id.ShouldNotContain("=");
		}
	}

	[Fact]
	public void CreateId_SeparatesTenants_AndSeparatesUntenantedFromTenanted()
	{
		// SAFETY: no two partitions share an id for the same aggregate. The untenanted partition is a value
		// like any other, so it is separated too - it is not an absence.
		const string AggregateId = "shared-aggregate";

		var tenantA = CreateId(AggregateId, "tenant-a");
		var tenantB = CreateId(AggregateId, "tenant-b");
		var untenanted = CreateId(AggregateId, TenantScope.UntenantedSentinel);

		tenantA.ShouldNotBe(tenantB);
		tenantA.ShouldNotBe(untenanted);
		tenantB.ShouldNotBe(untenanted);
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void Id_DefaultsToEmptyString()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var idProperty = _documentType.GetProperty("Id");

		// Assert
		idProperty.GetValue(document).ShouldBe(string.Empty);
	}

	[Fact]
	public void AggregateId_DefaultsToEmptyString()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("AggregateId");

		// Assert
		property.GetValue(document).ShouldBe(string.Empty);
	}

	[Fact]
	public void AggregateType_DefaultsToEmptyString()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("AggregateType");

		// Assert
		property.GetValue(document).ShouldBe(string.Empty);
	}

	[Fact]
	public void SnapshotId_DefaultsToEmptyString()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("SnapshotId");

		// Assert
		property.GetValue(document).ShouldBe(string.Empty);
	}

	[Fact]
	public void Version_DefaultsToZero()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("Version");

		// Assert
		property.GetValue(document).ShouldBe(0L);
	}

	[Fact]
	public void Data_DefaultsToEmptyString()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("Data");

		// Assert
		property.GetValue(document).ShouldBe(string.Empty);
	}

	[Fact]
	public void Metadata_DefaultsToNull()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("Metadata");

		// Assert
		property.GetValue(document).ShouldBeNull();
	}

	[Fact]
	public void CreatedAt_DefaultsToEmptyString()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("CreatedAt");

		// Assert
		property.GetValue(document).ShouldBe(string.Empty);
	}

	[Fact]
	public void Ttl_DefaultsToNull()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("Ttl");

		// Assert
		property.GetValue(document).ShouldBeNull();
	}

	#endregion

	#region JsonPropertyName Attribute Tests

	[Fact]
	public void Id_HasCorrectJsonPropertyName()
	{
		// Arrange
		var property = _documentType.GetProperty("Id");
		var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("id");
	}

	[Fact]
	public void AggregateId_HasCorrectJsonPropertyName()
	{
		// Arrange
		var property = _documentType.GetProperty("AggregateId");
		var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("aggregateId");
	}

	[Fact]
	public void AggregateType_HasCorrectJsonPropertyName()
	{
		// Arrange
		var property = _documentType.GetProperty("AggregateType");
		var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("aggregateType");
	}

	[Fact]
	public void Version_HasCorrectJsonPropertyName()
	{
		// Arrange
		var property = _documentType.GetProperty("Version");
		var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("version");
	}

	[Fact]
	public void Data_HasCorrectJsonPropertyName()
	{
		// Arrange
		var property = _documentType.GetProperty("Data");
		var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("data");
	}

	[Fact]
	public void Ttl_HasCorrectJsonPropertyName()
	{
		// Arrange
		var property = _documentType.GetProperty("Ttl");
		var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("ttl");
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsSealed()
	{
		// Assert
		_documentType.IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsInternal()
	{
		// Assert
		_documentType.IsNotPublic.ShouldBeTrue();
	}

	#endregion
}
