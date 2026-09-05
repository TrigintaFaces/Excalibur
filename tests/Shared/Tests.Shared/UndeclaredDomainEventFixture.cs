// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Tests.Shared;

/// <summary>
/// A domain event that deliberately declares no message name, for the startup guard that reports such
/// types.
/// </summary>
/// <remarks>
/// It lives here rather than beside its test because any assembly holding an undeclared event cannot
/// also be the target of an assembly-scanning registration -- that scan registers every concrete
/// domain event it finds and throws on the first undeclared one. Two suites in the abstractions test
/// assembly do exactly that, so a fixture placed there fails them. Nothing scans this assembly.
/// </remarks>
public sealed record UndeclaredDomainEventFixture(string Id) : IDomainEvent
{
	public string EventId => Id;

	public DateTimeOffset OccurredAt => DateTimeOffset.UnixEpoch;

	public IDictionary<string, object>? Metadata => null;
}

/// <summary>A declared sibling in the same assembly, so the guard has something to pass over.</summary>
[MessageName("Test.Shared.DeclaredDomainEventFixture")]
public sealed record DeclaredDomainEventFixture(string Id) : IDomainEvent
{
	public string EventId => Id;

	public DateTimeOffset OccurredAt => DateTimeOffset.UnixEpoch;

	public IDictionary<string, object>? Metadata => null;
}

/// <summary>
/// An undeclared GENERIC sibling. Its definition is the only place a name can be declared, and until a
/// closed generic could compose one from it, requiring the declaration here would have made every
/// construction claim the same name -- so the guard skipped generics entirely.
/// </summary>
public sealed record UndeclaredGenericDomainEventFixture<T>(string Id) : IDomainEvent
{
	public string EventId => Id;

	public DateTimeOffset OccurredAt => DateTimeOffset.UnixEpoch;

	public IDictionary<string, object>? Metadata => null;
}
