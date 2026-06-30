// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Text;

using Excalibur.Dispatch.Patterns.ClaimCheck;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

/// <summary>
/// Independent regression lock for <c>bd-unv8i3</c> (S856): a <see cref="JsonClaimCheckSerializer"/>
/// constructed with <b>null options</b> must default to the framework-wide JSON policy — <b>camelCase</b>
/// property names and <b>case-insensitive</b> reads — so its payloads interop with every other
/// <c>ISerializer</c> impl (<c>SystemTextJsonSerializer</c>/<c>DispatchJsonSerializer</c>/<c>PayloadSerializer</c>).
/// </summary>
/// <remarks>
/// <para>
/// Author≠implementer: BackendDeveloper owns the source fix; this is the independent lock.
/// </para>
/// <para>
/// <b>Pre-fix behaviour (RED):</b> the ctor stored <c>_options = options</c> directly, so null options fell
/// through to System.Text.Json's defaults — <b>PascalCase</b> names and <b>case-sensitive</b> reads — a
/// cross-serializer interop hazard. These tests assert the camelCase emission and case-insensitive binding
/// of a null-options serializer, RED against the pre-fix PascalCase/case-sensitive default.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Patterns)]
[Trait("Feature", "ClaimCheck")]
public sealed class JsonClaimCheckSerializerNullOptionsPolicyShould
{
    [Fact]
    public void EmitCamelCasePropertyNames_WithNullOptions()
    {
        var serializer = new JsonClaimCheckSerializer(); // null options → framework default policy

        var buffer = new ArrayBufferWriter<byte>();
        serializer.Serialize(new Payload { ClaimValue = 42 }, buffer);
        var json = Encoding.UTF8.GetString(buffer.WrittenSpan);

        // camelCase, not PascalCase (RED pre-fix: STJ default emits "ClaimValue").
        // Ordinal (case-sensitive) checks — Shouldly's ShouldContain/ShouldNotContain are case-INSENSITIVE,
        // which cannot distinguish camelCase from PascalCase, so assert on the literal byte content.
        json.Contains("\"claimValue\"", StringComparison.Ordinal)
            .ShouldBeTrue($"expected camelCase key; got: {json}");
        json.Contains("\"ClaimValue\"", StringComparison.Ordinal)
            .ShouldBeFalse($"PascalCase key must not be emitted; got: {json}");
    }

    [Fact]
    public void ReadCamelCasePayloadCaseInsensitively_IntoPascalCaseProperty_WithNullOptions()
    {
        var serializer = new JsonClaimCheckSerializer(); // null options → framework default policy

        // A camelCase payload bound into a PascalCase-property type. RED pre-fix: case-sensitive default
        // would leave ClaimValue at its default (0) because "claimValue" != "ClaimValue".
        var payload = "{\"claimValue\":42}"u8.ToArray();
        var result = serializer.Deserialize<Payload>(payload);

        _ = result.ShouldNotBeNull();
        result.ClaimValue.ShouldBe(42);
    }

    private sealed class Payload
    {
        public int ClaimValue { get; init; }
    }
}
