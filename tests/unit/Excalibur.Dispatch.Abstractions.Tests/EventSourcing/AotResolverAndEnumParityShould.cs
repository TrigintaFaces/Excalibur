// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Dispatch;

namespace Excalibur.Dispatch.Tests.EventSourcing;

/// <summary>
/// Binds the two halves of the AOT event-serialization contract that a consumer can get wrong without any
/// signal: a store composed under Native AOT with no source-generated type-info resolver, and a
/// source-generated context that writes enums as numbers where the reflection path writes strings.
/// </summary>
/// <remarks>
/// Both faults are silent by construction — the first surfaces only when a running application serializes
/// its first event, the second never surfaces at all and simply stores payloads the other path mis-reads.
/// Each is therefore locked with a safety arm proving the fault is refused and a liveness arm proving the
/// working configurations still compose, so a blanket rejection cannot satisfy the suite.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
[Trait("Feature", "AOT")]
public sealed class AotResolverAndEnumParityShould
{
	[Fact]
	public void RejectAnAbsentTypeInfoResolverWhereReflectionIsUnavailable()
	{
		// SAFETY. The reflection path a null resolver falls back to cannot run under Native AOT, so a store
		// that accepts it constructs cleanly and then fails on the first event it is asked to write.
		var options = EventSerializationDefaults.CreateCanonicalOptions();

		var ex = Should.Throw<InvalidOperationException>(
			() => EventSerializationDefaults.TryApplyTypeInfoResolver(options, null, reflectionEnabled: false));

		ex.Message.ShouldContain("EventTypeInfoResolver");
	}

	[Fact]
	public void KeepTheReflectionPathWhereReflectionIsAvailable()
	{
		// LIVENESS. The overwhelmingly common host still has reflection, and an absent resolver there is a
		// supported configuration, not a fault — a blanket throw would break every non-AOT consumer.
		var options = EventSerializationDefaults.CreateCanonicalOptions();

		EventSerializationDefaults
			.TryApplyTypeInfoResolver(options, null, reflectionEnabled: true)
			.ShouldBeFalse();
	}

	[Fact]
	public void AttachASuppliedResolverEvenWhereReflectionIsUnavailable()
	{
		// LIVENESS. The AOT arm must reject only the ABSENCE of a resolver; supplying one is the remedy the
		// exception names, so it has to succeed on the same path.
		var options = EventSerializationDefaults.CreateCanonicalOptions();

		EventSerializationDefaults
			.TryApplyTypeInfoResolver(options, EnumParityJsonContext.Default, reflectionEnabled: false)
			.ShouldBeTrue();

		options.TypeInfoResolver.ShouldBe(EnumParityJsonContext.Default);
	}

	[Fact]
	public void RejectAContextThatWritesEnumsAsNumbers()
	{
		// SAFETY. This context is camelCase and null-omitting, so it clears both halves of the contract that
		// ARE readable off JsonSerializerOptions — it is refused only because of what it actually writes.
		var ex = Should.Throw<ArgumentException>(
			() => new AotJsonEventSerializer(NumericEnumJsonContext.Default, typeof(EnumParityEvent)));

		ex.Message.ShouldContain(nameof(EnumParityStatus));
		ex.Message.ShouldContain("UseStringEnumConverter");
	}

	[Fact]
	public void AcceptAContextThatWritesEnumsAsStrings()
	{
		// LIVENESS. The conforming context differs from the rejected one by exactly UseStringEnumConverter,
		// so a check that rejected everything, or that keyed off the readable options, would fail here.
		var serializer = new AotJsonEventSerializer(EnumParityJsonContext.Default, typeof(EnumParityEvent));

		var json = System.Text.Encoding.UTF8.GetString(
			serializer.SerializeEvent(new EnumParityEvent { Status = EnumParityStatus.Shipped }));

		json.ShouldContain("\"status\":\"Shipped\"");
	}

	internal enum EnumParityStatus
	{
		// Deliberately no zero member: a value that is not a declared member is written as a number by BOTH
		// converter modes, so a check probing the default value would report a false divergence here.
		Placed = 1,
		Shipped = 2,
	}

	[MessageName("Test.Aot.EnumParityEvent")]
	internal sealed class EnumParityEvent : IDomainEvent
	{
		public EnumParityStatus Status { get; set; }

		public string EventId { get; set; } = string.Empty;

		public DateTimeOffset OccurredAt { get; set; }

		public IDictionary<string, object>? Metadata { get; set; }
	}
}

/// <summary>The canonical context shape: camelCase, enums as strings, nulls omitted.</summary>
[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	UseStringEnumConverter = true,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AotResolverAndEnumParityShould.EnumParityEvent))]
[JsonSerializable(typeof(string))]
internal sealed partial class EnumParityJsonContext : JsonSerializerContext;

/// <summary>
/// Conforming on both readable halves of the contract and divergent only on the unreadable one — the
/// context a consumer produces by copying the attribute and dropping a single setting.
/// </summary>
[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AotResolverAndEnumParityShould.EnumParityEvent))]
[JsonSerializable(typeof(string))]
internal sealed partial class NumericEnumJsonContext : JsonSerializerContext;
