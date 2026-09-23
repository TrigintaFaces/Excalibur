// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json.Serialization;

namespace Excalibur.Outbox.CosmosDb;

/// <summary>
/// Source-generated serialization metadata for the values this package stores in Cosmos documents.
/// </summary>
/// <remarks>
/// <para>
/// The outbox persists a message's headers as a JSON string on the document. That used the
/// reflection-based <c>JsonSerializer</c> overloads, which the trim and ahead-of-time analyzers cannot
/// prove safe, so the calls were wrapped in suppressions that asserted safety without demonstrating it.
/// Under a trimmed or NativeAOT publish the metadata those calls rely on can be removed, and the failure
/// arrives at runtime in a consumer's application after a build that reported success.
/// </para>
/// <para>
/// Generating the metadata at compile time removes the problem instead of describing it: the analyzers
/// can see the type, the trimmer keeps it, and the suppressions are gone because there is nothing left to
/// suppress. The same shape is used by the Firestore outbox and by the CDC providers in this repository.
/// </para>
/// <para>
/// <b>The stored format is unchanged, which matters because these documents are persisted.</b> The
/// previous options set <c>PropertyNamingPolicy = CamelCase</c>; that policy governs property names and
/// does not apply to dictionary keys, which are controlled by <c>DictionaryKeyPolicy</c> and were never
/// set. The only serialized type here is a dictionary, so no key was ever transformed and none is now — a
/// document written by an earlier version round-trips through this context unchanged.
/// </para>
/// </remarks>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = false)]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class CosmosDbOutboxSerializerContext : JsonSerializerContext;
