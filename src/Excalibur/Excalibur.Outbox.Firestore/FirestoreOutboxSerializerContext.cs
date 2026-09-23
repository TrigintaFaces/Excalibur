// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json.Serialization;

namespace Excalibur.Outbox.Firestore;

/// <summary>
/// Source-generated serialization metadata for the values this package stores in Firestore documents.
/// </summary>
/// <remarks>
/// <para>
/// The outbox persists a message's headers as a JSON string inside the document. That was done with the
/// reflection-based <c>JsonSerializer</c> overloads, which the trim and ahead-of-time analyzers cannot
/// prove safe — so the calls were wrapped in suppressions that asserted safety without demonstrating it.
/// Under a trimmed or NativeAOT publish the metadata those calls depend on can be removed, and the
/// failure arrives at runtime, in a consumer's application, after a build that reported success.
/// </para>
/// <para>
/// Generating the metadata at compile time removes the problem rather than describing it: the analyzers
/// can see the types, the trimmer keeps them, and the suppressions are gone because there is nothing left
/// to suppress. This is the same shape the CDC provider in this repository already uses.
/// </para>
/// <para>
/// <b>The wire format is unchanged, which matters because these documents are persisted.</b> The previous
/// options set <c>PropertyNamingPolicy = CamelCase</c>, and that policy governs property names — it does
/// not apply to dictionary keys, which are controlled by <c>DictionaryKeyPolicy</c> and were never set.
/// The only serialized type here is a dictionary, so no key was ever transformed and none is now: a
/// document written by an earlier version round-trips through this context byte for byte.
/// </para>
/// </remarks>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = false)]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class FirestoreOutboxSerializerContext : JsonSerializerContext;
