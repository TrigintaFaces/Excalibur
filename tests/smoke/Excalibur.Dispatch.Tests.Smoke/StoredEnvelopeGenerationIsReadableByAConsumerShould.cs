// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

#nullable enable

using System;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Excalibur.Compliance;
using Excalibur.Compliance.CryptoShredding;

namespace Excalibur.Dispatch.Tests.Smoke;

/// <summary>
/// Binds the one question the crypto-shredding guarantee is stated in terms of -- which key generation does
/// this stored field name? -- to the surface a consumer actually has.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why this arm lives in this assembly and not beside the cryptor's own tests.</strong> The property
/// under test is not "the parse works" but "a consumer can perform the parse", and that is a statement about
/// VISIBILITY. Excalibur.Compliance grants <c>InternalsVisibleTo</c> to its own unit-test assembly, so an arm
/// written there could reach an internal serializer context or a private deserializer without a single line
/// looking unusual, and would pass while the consumer-facing gap it claims to close remained open. This
/// assembly holds a reference to every shipping package and is granted internals by none of them, so the
/// restriction is enforced by the compiler rather than by the author's care.
/// </para>
/// <para>
/// <strong>Reflection is used, and it is not a way around the restriction.</strong>
/// <see cref="EncryptedFieldBinding.TryReadEnvelope"/> is public and takes a
/// <see cref="PropertyInfo"/> by design -- reading a stored field is how a consumer is meant to reach the
/// envelope bytes. Nothing here reflects over a non-public member; every call below resolves at compile time
/// against public API, which is why this file compiles at all from outside the assembly.
/// </para>
/// <para>
/// <strong>What makes the positive arm non-vacuous.</strong> The stored bytes are serialized by the writer's
/// own serializer metadata inside Excalibur.Compliance and deserialized by a separate set in
/// Excalibur.Compliance.Abstractions. They agree today only because both use the declared property names. Give
/// either one a naming policy, drop <see cref="EncryptedData.KeyGeneration"/> from the written envelope, or
/// reframe the payload, and the generation read back stops matching the generation bound -- which is exactly
/// the failure a consumer would otherwise discover as a silently wrong erasure report.
/// </para>
/// </remarks>
[Trait("Category", "Smoke")]
[Trait("Component", "Compliance")]
public sealed class StoredEnvelopeGenerationIsReadableByAConsumerShould
{
	private const string Plaintext = "ada@example.com";
	private const string BoundGeneration = "gen-01HQ8Y7V4M3K2P";

	/// <summary>
	/// The question an auditor asks: given a stored field, which key generation holds it? Answered from the
	/// stored bytes alone -- no key, no tenant, no decrypt attempt.
	/// </summary>
	[Fact]
	public async Task Read_the_key_generation_a_stored_field_names_using_only_public_api()
	{
		var stored = await StoreAnEncryptedCustomerAsync().ConfigureAwait(false);

		EncryptedFieldBinding.TryReadEnvelope(EmailProperty, stored, out var framed).ShouldBeTrue(
			"premise of this arm: the framework must have written a readable envelope into the field. If this "
			+ "fails, the parse below is parsing nothing and proves nothing.");

		EncryptedData.TryParse(framed, out var envelope).ShouldBeTrue(
			"a stored envelope the framework wrote must parse through the public surface. Without this a "
			+ "consumer cannot ask which generation holds a field, and the crypto-shredding guarantee -- "
			+ "stated in terms of that generation -- is unverifiable from outside the framework.");

		envelope.KeyGeneration.ShouldBe(
			BoundGeneration,
			"the generation read back must be the generation the write path bound. This is the value a "
			+ "destruction ledger is keyed by, so a consumer that reads a different one reports the wrong "
			+ "subject as erased, or a destroyed subject as live -- and either answer looks authoritative.");
	}

	/// <summary>
	/// A value that is not an envelope at all. The common case: a column holding plaintext, or holding a
	/// value written by something else entirely.
	/// </summary>
	[Fact]
	public void Refuse_bytes_that_are_not_an_envelope_without_throwing()
	{
		EncryptedData.TryParse(Encoding.UTF8.GetBytes(Plaintext), out var envelope).ShouldBeFalse(
			"plaintext is not an envelope. Answering true here would hand a caller a fabricated generation "
			+ "for a field nobody encrypted.");

		envelope.ShouldBeNull("a failed parse must not yield a value.");
	}

	/// <summary>
	/// A truncated envelope -- what a column too narrow for the ciphertext, a partial write, or a bad
	/// migration leaves behind.
	/// </summary>
	[Fact]
	public async Task Refuse_a_truncated_envelope_without_throwing()
	{
		var stored = await StoreAnEncryptedCustomerAsync().ConfigureAwait(false);
		EncryptedFieldBinding.TryReadEnvelope(EmailProperty, stored, out var framed).ShouldBeTrue(
			"premise: the envelope must be whole before truncating it means anything.");

		// Damage a GENUINE write rather than hand-building bytes: a hand-built fixture encodes this file's
		// belief about the envelope format and would keep passing after the format changed underneath it.
		var truncated = framed[..(framed.Length / 2)];

		EncryptedData.TryParse(truncated, out var envelope).ShouldBeFalse(
			"a truncated envelope must be reported, not thrown and not parsed. A caller auditing a column it "
			+ "does not control meets damaged rows as an ordinary condition.");

		envelope.ShouldBeNull("a failed parse must not yield a value.");
	}

	/// <summary>
	/// Malformed bodies behind a correct frame, and frames shorter than the marker itself. Each is a distinct
	/// way for the magic-byte check and the body to disagree.
	/// </summary>
	[Theory]
	[InlineData("magic bytes followed by text that is not JSON")]
	[InlineData("magic bytes followed by a JSON object missing every required member")]
	[InlineData("magic bytes followed by the JSON literal null")]
	[InlineData("fewer bytes than the marker occupies")]
	[InlineData("no bytes at all")]
	public void Refuse_a_malformed_envelope_without_throwing(string shape)
	{
		var framed = shape switch
		{
			"magic bytes followed by text that is not JSON" => Frame("}{ not json"),
			"magic bytes followed by a JSON object missing every required member" => Frame("{}"),
			"magic bytes followed by the JSON literal null" => Frame("null"),
			"fewer bytes than the marker occupies" => EncryptedData.MagicBytes[..2].ToArray(),
			_ => [],
		};

		EncryptedData.TryParse(framed, out var envelope).ShouldBeFalse(
			$"{shape} is not a readable envelope and must return false rather than throw.");

		envelope.ShouldBeNull("a failed parse must not yield a value.");
	}

	private static byte[] Frame(string body)
	{
		var magic = EncryptedData.MagicBytes;
		var bodyBytes = Encoding.UTF8.GetBytes(body);
		var framed = new byte[magic.Length + bodyBytes.Length];
		magic.CopyTo(framed);
		bodyBytes.CopyTo(framed, magic.Length);
		return framed;
	}

	private static PropertyInfo EmailProperty =>
		typeof(Customer).GetProperty(nameof(Customer.Email))!;

	/// <summary>
	/// Runs the framework's own write path over an annotated record, leaving the field holding exactly what a
	/// consumer's database would hold.
	/// </summary>
	private static async Task<Customer> StoreAnEncryptedCustomerAsync()
	{
		var record = new Customer { SubjectId = "subject-1", Email = Plaintext };
		var cryptor = new SubjectFieldCryptor(new GenerationBindingEncryptor());

		await cryptor.EncryptFieldsAsync(record, aggregateType: null, CancellationToken.None)
			.ConfigureAwait(false);

		return record;
	}

	/// <summary>
	/// Binds a known key generation and carries the plaintext through as the ciphertext, so the arms observe
	/// what the framework STORED and what a consumer can READ rather than a cipher. Real cryptography is
	/// covered where it belongs and here would only obscure which layer failed.
	/// </summary>
	private sealed class GenerationBindingEncryptor : IFieldEncryptor
	{
		public ValueTask<EncryptedData> EncryptAsync(
			string subjectId,
			RetentionScope retentionScope,
			ReadOnlyMemory<byte> plaintext,
			CancellationToken cancellationToken) =>
			ValueTask.FromResult(new EncryptedData
			{
				Ciphertext = plaintext.ToArray(),
				KeyId = "key-for-" + subjectId,
				KeyVersion = 1,
				KeyGeneration = BoundGeneration,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				Iv = [],
			});

		public ValueTask<byte[]?> DecryptAsync(EncryptedData envelope, CancellationToken cancellationToken) =>
			ValueTask.FromResult<byte[]?>(envelope.Ciphertext);
	}

	private sealed class Customer
	{
		[DataSubjectId]
		public string SubjectId { get; set; } = string.Empty;

		[PersonalData]
		public string? Email { get; set; }
	}
}
