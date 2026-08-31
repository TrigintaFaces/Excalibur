// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Text.Json.Serialization;

using Excalibur.Inbox.CosmosDb;

namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Unit tests for the <see cref="CosmosDbInboxDocument"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.3): CosmosDB unit tests.
/// Tests verify document properties and the CreateId factory method.
/// Note: CosmosDbInboxDocument is internal, so we use reflection to test it.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "CosmosDb")]
[Trait(TraitNames.Feature, TestFeatures.Inbox)]
public sealed class CosmosDbInboxDocumentShould
{
	private readonly Type _documentType;

	public CosmosDbInboxDocumentShould()
	{
		// Get the internal type via reflection
		var assembly = typeof(CosmosDbInboxOptions).Assembly;
		_documentType = assembly.GetType("Excalibur.Inbox.CosmosDb.CosmosDbInboxDocument")!;
	}

	#region CreateId Tests

	[Fact]
	public void CreateId_ReturnsCompositeId()
	{
		// Arrange
		var messageId = "msg-123";
		var handlerType = "OrderHandler";
		// CreateId's third parameter (tenantId) is OPTIONAL. Reflection does not apply default values, so
		// every Invoke below must pass all three arguments — a two-element array throws
		// TargetParameterCountException regardless of the default.
		var createIdMethod = _documentType.GetMethod("CreateId", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result = (string)createIdMethod!.Invoke(null, new object?[] { messageId, handlerType, null })!;

		// Assert
		result.ShouldBe("msg-123:OrderHandler");
	}

	[Fact]
	public void CreateId_ComposesTheTenantIntoTheDedupIdentity_WhenScoped()
	{
		// SAFETY: the tenant is a LEADING term of the dedup id, so two tenants carrying the same message id
		// and handler produce DIFFERENT documents and can never dedup against each other — one tenant's
		// delivery must not suppress another's. This is the isolation property; the arms above only cover
		// the tenant-less form, which cannot detect its loss.
		var createIdMethod = _documentType.GetMethod("CreateId", BindingFlags.Public | BindingFlags.Static);

		var tenantA = (string)createIdMethod!.Invoke(null, new object?[] { "msg-123", "OrderHandler", "tenant-a" })!;
		var tenantB = (string)createIdMethod!.Invoke(null, new object?[] { "msg-123", "OrderHandler", "tenant-b" })!;
		var untenanted = (string)createIdMethod!.Invoke(null, new object?[] { "msg-123", "OrderHandler", null })!;

		tenantA.ShouldBe("tenant-a:msg-123:OrderHandler");
		tenantB.ShouldNotBe(tenantA, "two tenants sharing a message id must not collide on one dedup document");
		untenanted.ShouldNotBe(tenantA, "the tenant-less form must not collide with a scoped tenant's id");
	}

	[Fact]
	public void CreateId_HandlesEmptyStrings()
	{
		// Arrange
		// CreateId's third parameter (tenantId) is OPTIONAL. Reflection does not apply default values, so
		// every Invoke below must pass all three arguments — a two-element array throws
		// TargetParameterCountException regardless of the default.
		var createIdMethod = _documentType.GetMethod("CreateId", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result = (string)createIdMethod!.Invoke(null, new object?[] { "", "", null })!;

		// Assert
		result.ShouldBe(":");
	}

	[Fact]
	public void CreateId_EscapesColonInValues()
	{
		// Arrange
		var messageId = "msg:123";
		var handlerType = "Order:Handler";
		// CreateId's third parameter (tenantId) is OPTIONAL. Reflection does not apply default values, so
		// every Invoke below must pass all three arguments — a two-element array throws
		// TargetParameterCountException regardless of the default.
		var createIdMethod = _documentType.GetMethod("CreateId", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result = (string)createIdMethod!.Invoke(null, new object?[] { messageId, handlerType, null })!;

		// Assert
		// The ':' joining the segments is escaped to %3A inside each term, so a term containing the
		// separator can no longer shift across the segment boundary. Previously this rendered
		// "msg:123:Order:Handler", which a different (messageId, handlerType) pair could also produce.
		result.ShouldBe("msg%3A123:Order%3AHandler");

		// Strengthened: the encoding is reversible, so two distinct inputs that collided under the
		// old bare join now differ. Without this the arm above would still pass for an encoding
		// that mapped every colon-bearing term onto one value.
		var shifted = (string)createIdMethod.Invoke(null, new object?[] { "msg", "123:Order:Handler", null })!;
		result.ShouldNotBe(shifted);
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void Id_DefaultsToEmptyString()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("Id");

		// Assert
		property.GetValue(document).ShouldBe(string.Empty);
	}

	[Fact]
	public void MessageId_DefaultsToEmptyString()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("MessageId");

		// Assert
		property.GetValue(document).ShouldBe(string.Empty);
	}

	[Fact]
	public void HandlerType_DefaultsToEmptyString()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("HandlerType");

		// Assert
		property.GetValue(document).ShouldBe(string.Empty);
	}

	[Fact]
	public void MessageType_DefaultsToEmptyString()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("MessageType");

		// Assert
		property.GetValue(document).ShouldBe(string.Empty);
	}

	[Fact]
	public void Payload_DefaultsToEmptyString()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("Payload");

		// Assert
		property.GetValue(document).ShouldBe(string.Empty);
	}

	[Fact]
	public void Metadata_DefaultsToEmptyDictionary()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType)!;
		var property = _documentType.GetProperty("Metadata");
		var metadata = (IDictionary<string, object>)property!.GetValue(document)!;

		// Assert
		metadata.ShouldNotBeNull();
		metadata.Count.ShouldBe(0);
	}

	[Fact]
	public void Status_DefaultsToZero()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("Status");

		// Assert
		property.GetValue(document).ShouldBe(0);
	}

	[Fact]
	public void RetryCount_DefaultsToZero()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("RetryCount");

		// Assert
		property.GetValue(document).ShouldBe(0);
	}

	[Fact]
	public void LastError_DefaultsToNull()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("LastError");

		// Assert
		property.GetValue(document).ShouldBeNull();
	}

	[Fact]
	public void ProcessedAt_DefaultsToNull()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("ProcessedAt");

		// Assert
		property.GetValue(document).ShouldBeNull();
	}

	[Fact]
	public void LastAttemptAt_DefaultsToNull()
	{
		// Arrange & Act
		var document = Activator.CreateInstance(_documentType);
		var property = _documentType.GetProperty("LastAttemptAt");

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
	public void MessageId_HasCorrectJsonPropertyName()
	{
		// Arrange
		var property = _documentType.GetProperty("MessageId");
		var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("message_id");
	}

	[Fact]
	public void HandlerType_HasCorrectJsonPropertyName()
	{
		// Arrange
		var property = _documentType.GetProperty("HandlerType");
		var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("handler_type");
	}

	[Fact]
	public void Status_HasCorrectJsonPropertyName()
	{
		// Arrange
		var property = _documentType.GetProperty("Status");
		var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("status");
	}

	[Fact]
	public void RetryCount_HasCorrectJsonPropertyName()
	{
		// Arrange
		var property = _documentType.GetProperty("RetryCount");
		var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();

		// Assert
		attribute.ShouldNotBeNull();
		attribute.Name.ShouldBe("retry_count");
	}

	#endregion

	#region Newtonsoft (Cosmos SDK-v3 default) emitted-key lock

	// CosmosDbInboxDocument is written through a consumer-supplied CosmosClient (the client is registered
	// TryAddSingleton, so a consumer's own registration wins) and the Cosmos SDK-v3 DEFAULT serializer is
	// Newtonsoft.Json -- System.Text.Json is opt-in, and Newtonsoft IGNORES [JsonPropertyName]. The
	// attribute-presence tests above are necessary but VACUOUS for a default client: they prove the STJ
	// attribute exists, not that Newtonsoft emits the keys the store's queries and point reads name. This
	// lock asserts the EMITTED JSON under the default serializer. It reds on an STJ-only document
	// (Newtonsoft emits PascalCase 'Id'/'TenantId') and greens with the dual STJ + Newtonsoft attributes.
	[Fact]
	public void SerializeRequiredLowercaseKeysUnderTheDefaultNewtonsoftSerializer()
	{
		var document = Activator.CreateInstance(_documentType)!;
		_documentType.GetProperty("Id")!.SetValue(document, "tenant-a:msg-1:HandlerX");
		_documentType.GetProperty("TenantId")!.SetValue(document, "tenant-a");

		// JsonConvert with default settings == exactly what the Cosmos SDK-v3 default client emits.
		var json = Newtonsoft.Json.JsonConvert.SerializeObject(document);

		using var parsed = System.Text.Json.JsonDocument.Parse(json);
		var root = parsed.RootElement;

		root.TryGetProperty("id", out _).ShouldBeTrue(
			$"CosmosDbInboxDocument.Id must serialize to lowercase 'id' under the DEFAULT (Newtonsoft) "
			+ $"serializer -- Cosmos point-reads the dedup record by 'id'. Emitted JSON: {json}");
		root.TryGetProperty("tenant_id", out _).ShouldBeTrue(
			$"CosmosDbInboxDocument.TenantId must serialize to 'tenant_id' under the DEFAULT (Newtonsoft) "
			+ $"serializer -- it is the tenant discriminator persisted alongside the dedup key. "
			+ $"Emitted JSON: {json}");

		// The PascalCase fallbacks (the STJ-only defect) must NOT appear.
		root.TryGetProperty("Id", out _).ShouldBeFalse(
			"A PascalCase 'Id' under the default Newtonsoft serializer means the dedup point read returns "
			+ "NotFound, so an already-processed message is processed again.");
		root.TryGetProperty("TenantId", out _).ShouldBeFalse(
			"A PascalCase 'TenantId' means the persisted tenant discriminator changes key between clients, "
			+ "so a document written by one serializer is unreadable as tenant-scoped by the other.");
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
