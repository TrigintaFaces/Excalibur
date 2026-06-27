using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace Excalibur.Dispatch.Tests.EventSourcing;

/// <summary>
/// Unit tests for JsonEventSerializer.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class JsonEventSerializerShould : UnitTestBase
{
	[RequiresDynamicCode("Test requires dynamic code")]
	private static JsonEventSerializer CreateSerializer(JsonSerializerOptions? options = null)
		=> new(options);

	// wpynky: the reflection assembly scan is OFF by default (secure-by-default). Tests that exercise
	// the scan-resolution mechanics (full name, simple-name fallback, caching) opt into it explicitly —
	// the secure default (unregistered => UnknownEventTypeException) is locked separately in
	// EventSerializerUnregisteredTypeRejectionShould.
	[RequiresDynamicCode("Test requires dynamic code")]
	private static JsonEventSerializer CreateScanningSerializer(JsonSerializerOptions? options = null)
		=> new(options, allowAssemblyScan: true);

	[Fact]
	[RequiresDynamicCode("Test requires dynamic code")]
	public void Constructor_WithNoOptions_CreatesSerializer()
	{
		// Act
		var serializer = CreateSerializer();

		// Assert
		serializer.ShouldNotBeNull();
	}

	[Fact]
	[RequiresDynamicCode("Test requires dynamic code")]
	public void Constructor_WithCustomOptions_CreatesSerializer()
	{
		// Arrange
		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

		// Act
		var serializer = CreateSerializer(options);

		// Assert
		serializer.ShouldNotBeNull();
	}

	[Fact]
	[RequiresDynamicCode("Test requires dynamic code")]
	[RequiresUnreferencedCode("Test requires unreferenced code")]
	public void SerializeEvent_WithValidEvent_ReturnsBytes()
	{
		// Arrange
		var serializer = CreateSerializer();
		var domainEvent = new TestDomainEvent { Name = "test" };

		// Act
		var result = serializer.SerializeEvent(domainEvent);

		// Assert
		result.ShouldNotBeNull();
		result.Length.ShouldBeGreaterThan(0);
		var json = Encoding.UTF8.GetString(result);
		json.ShouldContain("test");
	}

	[Fact]
	[RequiresDynamicCode("Test requires dynamic code")]
	[RequiresUnreferencedCode("Test requires unreferenced code")]
	public void SerializeEvent_WithNullEvent_ThrowsArgumentNullException()
	{
		// Arrange
		var serializer = CreateSerializer();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => serializer.SerializeEvent(null!));
	}

	[Fact]
	[RequiresDynamicCode("Test requires dynamic code")]
	[RequiresUnreferencedCode("Test requires unreferenced code")]
	public void DeserializeEvent_WithValidData_ReturnsEvent()
	{
		// Arrange
		var serializer = CreateSerializer();
		var originalEvent = new TestDomainEvent { Name = "roundtrip" };
		var data = serializer.SerializeEvent(originalEvent);

		// Act
		var result = serializer.DeserializeEvent(data, typeof(TestDomainEvent));

		// Assert
		result.ShouldNotBeNull();
		result.ShouldBeOfType<TestDomainEvent>();
		((TestDomainEvent)result).Name.ShouldBe("roundtrip");
	}

	[Fact]
	[RequiresDynamicCode("Test requires dynamic code")]
	[RequiresUnreferencedCode("Test requires unreferenced code")]
	public void DeserializeEvent_WithNullData_ThrowsArgumentNullException()
	{
		// Arrange
		var serializer = CreateSerializer();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => serializer.DeserializeEvent(null!, typeof(TestDomainEvent)));
	}

	[Fact]
	[RequiresDynamicCode("Test requires dynamic code")]
	[RequiresUnreferencedCode("Test requires unreferenced code")]
	public void DeserializeEvent_WithNullType_ThrowsArgumentNullException()
	{
		// Arrange
		var serializer = CreateSerializer();
		var data = Encoding.UTF8.GetBytes("{}");

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => serializer.DeserializeEvent(data, null!));
	}

	[Fact]
	[RequiresDynamicCode("Test requires dynamic code")]
	[RequiresUnreferencedCode("Test requires unreferenced code")]
	public void DeserializeEvent_WithWrongType_ThrowsInvalidOperationException()
	{
		// Arrange
		var serializer = CreateSerializer();
		var data = Encoding.UTF8.GetBytes("{\"value\": 42}");

		// Act & Assert - NonEventClass is not an IDomainEvent
		Should.Throw<InvalidOperationException>(() => serializer.DeserializeEvent(data, typeof(NonEventClass)));
	}

	[Fact]
	[RequiresDynamicCode("Test requires dynamic code")]
	public void GetTypeName_ReturnsExpectedName()
	{
		// Arrange
		var serializer = CreateSerializer();

		// Act
		var name = serializer.GetTypeName(typeof(TestDomainEvent));

		// Assert
		name.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	[RequiresDynamicCode("Test requires dynamic code")]
	public void ResolveType_WithNullTypeName_ThrowsArgumentException()
	{
		// Arrange
		var serializer = CreateSerializer();

		// Act & Assert
		Should.Throw<ArgumentException>(() => serializer.ResolveType(null!));
	}

	[Fact]
	[RequiresDynamicCode("Test requires dynamic code")]
	public void ResolveType_WithEmptyTypeName_ThrowsArgumentException()
	{
		// Arrange
		var serializer = CreateSerializer();

		// Act & Assert
		Should.Throw<ArgumentException>(() => serializer.ResolveType(string.Empty));
	}

	[Fact]
	[RequiresDynamicCode("Test requires dynamic code")]
	public void ResolveType_WithValidTypeName_ReturnsType()
	{
		// Arrange — scan opt-in: this exercises the reflection assembly-scan resolution path.
		var serializer = CreateScanningSerializer();
		var fullName = typeof(string).AssemblyQualifiedName;

		// Act
		var result = serializer.ResolveType(fullName!);

		// Assert
		result.ShouldBe(typeof(string));
	}

	[Fact]
	[RequiresDynamicCode("Test requires dynamic code")]
	public void ResolveType_WithFullNameOnly_ReturnsType()
	{
		// Arrange — scan opt-in: this exercises the reflection assembly-scan resolution path.
		var serializer = CreateScanningSerializer();
		var fullNameOnly = typeof(TestDomainEvent).FullName!;

		// Act
		var result = serializer.ResolveType(fullNameOnly);

		// Assert
		result.ShouldBe(typeof(TestDomainEvent));
	}

	[Fact]
	[RequiresDynamicCode("Test requires dynamic code")]
	public void ResolveType_WithInvalidAssemblySuffix_FallsBackToSimpleTypeName()
	{
		// Arrange — scan opt-in: this exercises the reflection assembly-scan resolution path.
		var serializer = CreateScanningSerializer();
		var typeName = $"{typeof(TestDomainEvent).FullName}, Missing.Assembly";

		// Act
		var result = serializer.ResolveType(typeName);

		// Assert
		result.ShouldBe(typeof(TestDomainEvent));
	}

	[Fact]
	[RequiresDynamicCode("Test requires dynamic code")]
	public void ResolveType_CachesResolvedTypes()
	{
		// Arrange — scan opt-in: caching only occurs on the assembly-scan resolution path.
		var serializer = CreateScanningSerializer();
		var fullName = typeof(int).AssemblyQualifiedName;

		// Act - resolve twice
		var result1 = serializer.ResolveType(fullName!);
		var result2 = serializer.ResolveType(fullName!);

		// Assert
		result1.ShouldBe(result2);
	}

	[Fact]
	[RequiresDynamicCode("Test requires dynamic code")]
	public void ResolveType_WithUnregisteredTypeName_AndScanDisabled_ThrowsUnknownEventTypeException()
	{
		// Arrange — default serializer: scan disabled, so an unregistered name is rejected.
		var serializer = CreateSerializer();

		// Act & Assert — strengthened from InvalidOperationException to the specific subtype (wpynky).
		Should.Throw<UnknownEventTypeException>(
			() => serializer.ResolveType("NonExistent.Type.That.Does.Not.Exist"));
	}

	[Fact]
	[RequiresDynamicCode("Test requires dynamic code")]
	public void ResolveType_WithUnresolvableTypeName_AndScanEnabled_ThrowsUnknownEventTypeException()
	{
		// Arrange — scan enabled, but no loaded assembly exposes the name => still rejected.
		var serializer = CreateScanningSerializer();

		// Act & Assert
		Should.Throw<UnknownEventTypeException>(
			() => serializer.ResolveType("NonExistent.Type.That.Does.Not.Exist"));
	}

	// Test helper types - internal to avoid CA1034
	internal sealed class TestDomainEvent : IDomainEvent
	{
		public string Name { get; set; } = string.Empty;
		public string EventId { get; set; } = Guid.NewGuid().ToString();
		public string AggregateId { get; set; } = string.Empty;
		public long Version { get; set; }
		public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
		public string EventType { get; set; } = nameof(TestDomainEvent);
		public IDictionary<string, object>? Metadata { get; set; }
	}

	internal sealed class NonEventClass
	{
		public int Value { get; set; }
	}
}
