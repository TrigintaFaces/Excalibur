// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json.Serialization;

namespace Excalibur.Outbox.DynamoDb;

/// <summary>
/// Source-generated serialization metadata for the values this package stores in DynamoDB items.
/// </summary>
/// <remarks>
/// <para>
/// The outbox persists a message's headers as a JSON string attribute. That used the reflection-based
/// <c>JsonSerializer</c> overloads, which the trim and ahead-of-time analyzers cannot prove safe, so the
/// calls were wrapped in suppressions that asserted safety without demonstrating it. Under a trimmed or
/// NativeAOT publish the metadata those calls rely on can be removed, and the failure arrives at runtime
/// in a consumer's application after a build that reported success.
/// </para>
/// <para>
/// Generating the metadata at compile time removes the problem instead of describing it: the analyzers can
/// see the type, the trimmer keeps it, and the suppressions are gone because there is nothing left to
/// suppress.
/// </para>
/// <para>
/// <b>This context deliberately covers the header dictionary only.</b> The store also serializes the
/// paging key returned by the AWS SDK, whose element type carries a stream and self-referential
/// collections; declaring that graph here is a separate decision with a different risk profile, and it
/// keeps its suppression until that decision is taken. Adding it to this context to make one more warning
/// disappear would be the wrong reason to widen generated metadata.
/// </para>
/// <para>
/// <b>The stored format is unchanged, which matters because these items are persisted.</b> The previous
/// options set <c>PropertyNamingPolicy = CamelCase</c>; that policy governs property names and does not
/// apply to dictionary keys, which are controlled by <c>DictionaryKeyPolicy</c> and were never set. The
/// only type declared here is a dictionary, so no key was ever transformed and none is now.
/// </para>
/// </remarks>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = false)]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class DynamoDbOutboxSerializerContext : JsonSerializerContext;
