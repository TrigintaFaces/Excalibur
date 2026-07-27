// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers.Text;
using System.Reflection;
using System.Text;

using Excalibur.Data.CosmosDb.Projections;

namespace Excalibur.Data.CosmosDb.Tests.Projections;

/// <summary>
/// Equivalence/correctness lock for <see cref="CosmosDbProjectionStore{TProjection}"/>'s private
/// <c>CreateDocumentId</c> helper, which encodes the projection id's UTF-8 bytes into a Cosmos-safe
/// document id via the BCL primitive <see cref="Base64Url.EncodeToString(System.ReadOnlySpan{byte})"/>.
/// <para>
/// The impl encodes <c>Encoding.UTF8.GetBytes(projectionId)</c> (raw id bytes — NOT a hash) with
/// unpadded base64url. This lock pins: (1) URL-safe alphabet with no padding, (2) determinism +
/// collision-resistance, (3) a lossless round-trip back to the source bytes, and (4) byte-identity
/// with the former hand-rolled <c>Convert.ToBase64String(...).Replace('+','-').Replace('/','_')
/// .TrimEnd('=')</c> so existing stored document ids do not churn.
/// </para>
/// <para>
/// Non-vacuity: a mutant that used plain <c>Convert.ToBase64String</c> (or omitted the char
/// substitution / padding trim) would emit <c>'+'</c>, <c>'/'</c>, or <c>'='</c> for the multi-byte
/// inputs below — the URL-safe, no-pad, byte-identity, and Cosmos-validity assertions all go RED.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class CosmosDbCreateDocumentIdShould
{
	private sealed class TestProjection
	{
		public string Name { get; set; } = string.Empty;
	}

	// CreateDocumentId is private static — reach it via reflection on the constructed generic type.
	private static readonly MethodInfo CreateDocumentIdMethod =
		typeof(CosmosDbProjectionStore<TestProjection>)
			.GetMethod("CreateDocumentId", BindingFlags.NonPublic | BindingFlags.Static)
			?? throw new InvalidOperationException(
				"CreateDocumentId(string) not found — the impl seam this lock binds to has moved.");

	private static string CreateDocumentId(string projectionId) =>
		(string)CreateDocumentIdMethod.Invoke(null, [projectionId])!;

	// The exact prior hand-roll the AC requires byte-identity with (no doc-id churn on migration).
	private static string PriorHandRoll(string projectionId) =>
		Convert.ToBase64String(Encoding.UTF8.GetBytes(projectionId))
			.Replace('+', '-')
			.Replace('/', '_')
			.TrimEnd('=');

	// Representative id set. The multi-byte / high-codepoint inputs force '+' and '/' in *standard*
	// base64, so they are the load-bearing cases that separate base64url from base64.
	public static TheoryData<string> RepresentativeIds() =>
	[
		"order-123",
		"projection:with:colons",
		"a/b/c-with-slashes",
		"café-Ñ-ÿ",
		"emoji-🎉-and-中文",
		"ûÿþ", // bytes chosen to land on '+' and '/' in standard base64
		"x",
		"",
	];

	[Theory]
	[MemberData(nameof(RepresentativeIds))]
	public void Produce_only_url_safe_base64url_characters_with_no_padding(string projectionId)
	{
		var id = CreateDocumentId(projectionId);

		id.ShouldNotContain("+");
		id.ShouldNotContain("/");
		id.ShouldNotContain("=");
		id.ShouldAllBe(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_');
	}

	[Theory]
	[MemberData(nameof(RepresentativeIds))]
	public void Be_a_valid_cosmos_document_id(string projectionId)
	{
		var id = CreateDocumentId(projectionId);

		// Cosmos ids may not contain '/', '\\', '?', or '#'.
		id.ShouldNotContain("/");
		id.ShouldNotContain("\\");
		id.ShouldNotContain("?");
		id.ShouldNotContain("#");
	}

	[Theory]
	[MemberData(nameof(RepresentativeIds))]
	public void Be_deterministic_for_the_same_input(string projectionId)
	{
		CreateDocumentId(projectionId).ShouldBe(CreateDocumentId(projectionId));
	}

	[Fact]
	public void Produce_distinct_ids_for_distinct_inputs()
	{
		string[] inputs = ["order-1", "order-2", "café", "cafe", "a/b", "a-b", "", "x"];

		var ids = inputs.Select(CreateDocumentId).ToArray();

		ids.Distinct(StringComparer.Ordinal).Count().ShouldBe(inputs.Length);
	}

	[Theory]
	[MemberData(nameof(RepresentativeIds))]
	public void Round_trip_losslessly_back_to_the_source_bytes(string projectionId)
	{
		var id = CreateDocumentId(projectionId);

		var decoded = Base64Url.DecodeFromChars(id);

		decoded.ShouldBe(Encoding.UTF8.GetBytes(projectionId));
		Encoding.UTF8.GetString(decoded).ShouldBe(projectionId);
	}

	[Theory]
	[MemberData(nameof(RepresentativeIds))]
	public void Be_byte_identical_to_the_prior_hand_rolled_base64url(string projectionId)
	{
		// Equivalence guarantee: switching to the BCL primitive must NOT change any produced id,
		// or every previously stored projection document would become unreachable.
		CreateDocumentId(projectionId).ShouldBe(PriorHandRoll(projectionId));
	}
}
