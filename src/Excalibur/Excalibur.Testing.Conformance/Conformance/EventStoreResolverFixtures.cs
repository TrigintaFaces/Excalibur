// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

using Excalibur.Dispatch;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// An event type the conformance resolver declares, used as the arm's fixture discriminator.
/// </summary>
internal sealed class DeclaredConformanceEvent : IDomainEvent
{
	public string EventId { get; set; } = Guid.NewGuid().ToString();

	public string AggregateId { get; set; } = string.Empty;

	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

	public string EventType { get; set; } = nameof(DeclaredConformanceEvent);

	public string Name { get; set; } = string.Empty;

	public IDictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// An event type the conformance resolver deliberately does not declare.
/// </summary>
/// <remarks>
/// Nothing about this type is special; what matters is its absence from
/// <see cref="ConformanceResolverContext"/>. A store that consults the host's resolver cannot serialize
/// it. A store that quietly built its own reflection options serializes it happily, which is the defect
/// the refusal arm exists to catch — and which no build warning reports either way.
/// </remarks>
internal sealed class UndeclaredConformanceEvent : IDomainEvent
{
	public string EventId { get; set; } = Guid.NewGuid().ToString();

	public string AggregateId { get; set; } = string.Empty;

	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

	public string EventType { get; set; } = nameof(UndeclaredConformanceEvent);

	public IDictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// A consumer-shaped source-generated context declaring exactly one event type.
/// </summary>
/// <remarks>
/// This stands in for the closed set of event types a host states its process will ever write. It carries
/// no <c>JsonSourceGenerationOptions</c> annotation on purpose: a store attaches the resolver to its own
/// canonical options rather than adopting the context's, so a bare context is the stricter fixture.
/// </remarks>
[JsonSerializable(typeof(DeclaredConformanceEvent))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(bool))]
internal sealed partial class ConformanceResolverContext : JsonSerializerContext;
