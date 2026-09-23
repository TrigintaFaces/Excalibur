// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

using Excalibur.Outbox.DynamoDb;

namespace Excalibur.Data.DynamoDb.Tests.Outbox;

/// <summary>
/// Binds the stored shape of the outbox message headers to the format earlier versions wrote.
/// </summary>
/// <remarks>
/// <para>
/// The headers were serialized with the reflection-based <see cref="JsonSerializer"/> overloads, which the
/// trim and ahead-of-time analyzers cannot prove safe; the calls carried suppressions that asserted safety
/// without demonstrating it. They now go through a source-generated context, which makes the call provably
/// safe and removes the suppression entirely.
/// </para>
/// <para>
/// <b>These items are persisted, so that change is only safe if the bytes are identical.</b> The previous
/// options set <c>PropertyNamingPolicy = CamelCase</c>, which governs PROPERTY names and does not apply to
/// dictionary KEYS — those are controlled by <c>DictionaryKeyPolicy</c>, which was never set. This suite
/// exists because that is an argument, and an argument is not evidence when being wrong means every stored
/// message's headers stop round-tripping.
/// </para>
/// <para>
/// <b>Scope.</b> The store also serializes the AWS paging key, which is a different type and keeps its
/// suppression pending a separate decision. Nothing here covers it, and this suite must not be read as
/// clearing it.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class DynamoDbOutboxHeaderSerializationShould
{
    /// <summary>The options the store and the subscription used before the generated context replaced them.</summary>
    private static readonly JsonSerializerOptions LegacyOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Header keys chosen to be hostile to the naming policy: were <c>CamelCase</c> ever applied to
    /// dictionary keys, every one of these would visibly change.
    /// </summary>
    private static Dictionary<string, string> SampleHeaders() => new(StringComparer.Ordinal)
    {
        ["Content-Type"] = "application/json",
        ["TraceParent"] = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
        ["X-Correlation-ID"] = "corr-42",
        ["UPPERCASE"] = "shouted",
        ["already_snake"] = "unchanged",
        ["A"] = "single-letter key",
        [""] = "empty key is legal in a dictionary",
    };

    /// <summary>
    /// SAFETY. The source-generated context writes byte-for-byte what the reflection-based options wrote.
    /// </summary>
    /// <remarks>
    /// RED by construction against a context that sets <c>DictionaryKeyPolicy</c> or changes the naming
    /// policy: the keys above would be rewritten and the two strings would diverge.
    /// </remarks>
    [Fact]
    public void WriteTheSameBytesAsTheReflectionBasedOptions()
    {
        var headers = SampleHeaders();

        var legacy = JsonSerializer.Serialize(headers, LegacyOptions);
        var generated = JsonSerializer.Serialize(
            headers,
            DynamoDbOutboxSerializerContext.Default.DictionaryStringString);

        generated.ShouldBe(
            legacy,
            "the source-generated context changed the stored header format, so headers written by an "
            + "earlier version would no longer round-trip and every persisted outbox item would lose them");
    }

    /// <summary>
    /// LIVENESS. An item written by the previous serializer still reads back through the new one.
    /// </summary>
    /// <remarks>
    /// The arm above compares two writers and would pass if BOTH were broken the same way. This one starts
    /// from the legacy bytes and requires the new reader to recover the original dictionary, so a format
    /// that is self-consistent but wrong still fails. It covers the store and the streams subscription
    /// together: both read headers through the same context.
    /// </remarks>
    [Fact]
    public void ReadBackAnItemWrittenByTheReflectionBasedSerializer()
    {
        var headers = SampleHeaders();
        var storedByPreviousVersion = JsonSerializer.Serialize(headers, LegacyOptions);

        var recovered = JsonSerializer.Deserialize(
            storedByPreviousVersion,
            DynamoDbOutboxSerializerContext.Default.DictionaryStringString);

        _ = recovered.ShouldNotBeNull();
        recovered.Count.ShouldBe(headers.Count);
        foreach (var (key, value) in headers)
        {
            recovered.ShouldContainKeyAndValue(key, value);
        }
    }

    /// <summary>
    /// PRECISION. The context resolves generated metadata for the dictionary, with no reflection fallback.
    /// </summary>
    /// <remarks>
    /// This is what makes the suppression removable rather than merely hidden. Were the type not declared
    /// on the context, the generated resolver would return no type info and the call would fall back to
    /// reflection — the state this change exists to leave.
    /// </remarks>
    [Fact]
    public void ResolveGeneratedMetadataForTheHeaderDictionary()
    {
        var typeInfo = DynamoDbOutboxSerializerContext.Default
            .GetTypeInfo(typeof(Dictionary<string, string>));

        _ = typeInfo.ShouldNotBeNull(
            "the header dictionary is not declared on the source-generated context, so serialization falls "
            + "back to reflection and the trim/AOT suppression would still be load-bearing");
    }
}
