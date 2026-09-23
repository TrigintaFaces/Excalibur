// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.CryptoShredding;
using Excalibur.Compliance.Encryption;
using Excalibur.Dispatch;
using Excalibur.EventSourcing.Encryption.Decorators;

using FakeItEasy;

using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Encryption;

/// <summary>
/// Encrypting an annotated event is idempotent, so a retry after a failed append cannot wrap the value twice.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DEFECT.</b> The encrypt path mutates the CALLER'S event in place and is followed by an append that
/// can fail. When it does, the caller still holds the event with its fields already replaced by envelopes,
/// and the natural response to a transient fault is to retry the same instance. The write path read the
/// property with a raw <c>GetValue</c> and applied no marker test, so it could not tell an envelope from
/// plaintext and encrypted it a second time. The stored value then decrypts in ONE pass to an envelope
/// string rather than to the subject's data — corruption that presents as a successful decryption, which is
/// the worst shape it could take.
/// </para>
/// <para>
/// <b>WHY THE STORE MUST GENUINELY THROW, and why it is in the arm's name.</b> A double that succeeds
/// produces no retry, so the second encrypt never happens and the arm passes against the broken code. The
/// throw is not scenery — it is the only thing that creates the state under test. The name carries it so
/// that a later simplification to a happy-path double is visibly wrong rather than quietly vacuous.
/// </para>
/// <para>
/// <b>THE CONTROLS ARE NOT OPTIONAL HERE.</b> A guard that skipped everything, or that threw whenever the
/// marker was absent, would satisfy the safety arm and destroy the framework: an unmarked value is the
/// LEGACY form written before the marker existed, and several read paths depend on unmarked meaning
/// plaintext. The two control arms below pin that an ordinary first encrypt still encrypts and that an
/// unmarked value is still encrypted rather than skipped.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptIsIdempotentAcrossAFailedAppendShould
{
    private const string OriginalPlaintext = "Ada Lovelace";

    /// <summary>
    /// Fails the first append and succeeds afterwards — the transient store fault the retry responds to.
    /// </summary>
    private sealed class ThrowsOnFirstAppendEventStore : IEventStore
    {
        public int AppendAttempts { get; private set; }

        public ValueTask<AppendResult> AppendAsync(
            string aggregateId,
            string aggregateType,
            IEnumerable<IDomainEvent> events,
            long expectedVersion,
            CancellationToken cancellationToken)
        {
            AppendAttempts++;

            return AppendAttempts == 1
                ? throw new InvalidOperationException("transient store fault on the first append")
                : ValueTask.FromResult(AppendResult.CreateSuccess(expectedVersion + 1, firstEventPosition: null));
        }

        public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(string aggregateId, string aggregateType, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<StoredEvent>>([]);

        public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(string aggregateId, string aggregateType, long fromVersion, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<StoredEvent>>([]);
    }

    /// <summary>
    /// Carries the plaintext through as the ciphertext, so the arms observe the BINDING and the marker rather
    /// than a cipher. A real cipher would prove the same thing and hide which step failed.
    /// </summary>
    private static IFieldEncryptor RoundTrippingEncryptor()
    {
        var encryptor = A.Fake<IFieldEncryptor>();

#pragma warning disable CA2012 // FakeItEasy stores the ValueTask rather than awaiting it here
        A.CallTo(() => encryptor.EncryptAsync(A<string>._, A<ReadOnlyMemory<byte>>._, A<CancellationToken>._))
            .ReturnsLazily((string _, ReadOnlyMemory<byte> plaintext, CancellationToken _) =>
                new EncryptedData
                {
                    Ciphertext = plaintext.ToArray(),
                    KeyId = "test-key",
                    KeyVersion = 1,
                    Algorithm = EncryptionAlgorithm.Aes256Gcm,
                    Iv = new byte[12],
                });

        A.CallTo(() => encryptor.DecryptAsync(A<EncryptedData>._, A<CancellationToken>._))
            .ReturnsLazily((EncryptedData envelope, CancellationToken _) => envelope.Ciphertext);
#pragma warning restore CA2012

        return encryptor;
    }

    private static EncryptingEventStoreDecorator CreateDecorator(SubjectFieldCryptor cryptor, IEventStore inner) =>
        new(
            inner,
            A.Fake<IEncryptionProviderRegistry>(),
            cryptor,
            A.Fake<IEventSerializer>(),
            Options.Create(new EncryptionOptions
            {
                Mode = EncryptionMode.EncryptAndDecrypt,
                DefaultPurpose = "test",
            }), global::Excalibur.Dispatch.UntenantedContext.Instance);

    private static PersonalOrderPlaced NewEvent() =>
        new() { SubjectId = "subject-1", CustomerName = OriginalPlaintext };

    /// <summary>
    /// SAFETY — the arm the defect is about. RED against the pre-fix write path, which wraps twice.
    /// </summary>
    [Fact]
    public async Task DecryptToTheOriginalPlaintextInOnePass_AfterTheFirstAppendThrowsAndTheCallerRetriesTheSameEvent()
    {
        var cryptor = new SubjectFieldCryptor(RoundTrippingEncryptor());
        var store = new ThrowsOnFirstAppendEventStore();
        var decorator = CreateDecorator(cryptor, store);
        var evt = NewEvent();

        // The append fails AFTER the encrypt has already mutated the caller's event.
        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await decorator.AppendAsync("agg-1", "Order", [evt], expectedVersion: 0, CancellationToken.None))
            .ConfigureAwait(false);

        // The caller retries the SAME instance, which is what a repository does with a transient fault.
        _ = await decorator.AppendAsync("agg-1", "Order", [evt], expectedVersion: 0, CancellationToken.None)
            .ConfigureAwait(false);

        store.AppendAttempts.ShouldBe(2, "the retry must actually have happened, or this arm proves nothing");

        await cryptor.DecryptFieldsAsync(evt, CancellationToken.None).ConfigureAwait(false);

        evt.CustomerName.ShouldBe(
            OriginalPlaintext,
            "one decrypt pass returned something other than the subject's data, so the value was wrapped "
            + "twice: the retry re-encrypted an envelope it could not distinguish from plaintext. A caller "
            + "reading this field receives an envelope string and has no signal that anything went wrong — "
            + "the decrypt reported success");
    }

    /// <summary>
    /// LIVENESS. Without this, the safety arm is satisfied by a guard that skips every property and encrypts
    /// nothing at all — which would persist personal data in the clear.
    /// </summary>
    [Fact]
    public async Task StillEncryptAnOrdinaryFirstWrite()
    {
        var cryptor = new SubjectFieldCryptor(RoundTrippingEncryptor());
        var evt = NewEvent();

        await cryptor.EncryptFieldsAsync(evt, CancellationToken.None).ConfigureAwait(false);

        evt.CustomerName.ShouldNotBe(OriginalPlaintext, "a first encrypt must still encrypt");
        evt.CustomerName!.StartsWith(EncryptedFieldBinding.StringEnvelopePrefix, StringComparison.Ordinal).ShouldBeTrue(
            "and it must carry the marker, which is what makes the second write recognisable as a re-encrypt");
    }

    /// <summary>
    /// LIVENESS. The guard keys on the MARKER, so it must not skip a value that lacks one. An unmarked value
    /// is the legacy form, and treating it as already-encrypted would leave personal data in the clear —
    /// turning a corruption fix into a disclosure.
    /// </summary>
    [Fact]
    public async Task StillEncryptAnUnmarkedLegacyPlaintext()
    {
        var cryptor = new SubjectFieldCryptor(RoundTrippingEncryptor());
        var evt = NewEvent();

        // A value that has never been through this framework: no marker, ordinary text.
        evt.CustomerName = "unmarked legacy value";

        await cryptor.EncryptFieldsAsync(evt, CancellationToken.None).ConfigureAwait(false);

        evt.CustomerName.ShouldNotBe(
            "unmarked legacy value",
            "an unmarked value is plaintext, not an envelope. Skipping it would persist personal data in the "
            + "clear for exactly the records written before the marker existed");
    }

    /// <summary>
    /// SAFETY. The idempotence stated directly, without the store: encrypting twice equals encrypting once.
    /// </summary>
    [Fact]
    public async Task ProduceTheSameValueWhenEncryptedTwice()
    {
        var cryptor = new SubjectFieldCryptor(RoundTrippingEncryptor());
        var evt = NewEvent();

        await cryptor.EncryptFieldsAsync(evt, CancellationToken.None).ConfigureAwait(false);
        var afterFirst = evt.CustomerName;

        await cryptor.EncryptFieldsAsync(evt, CancellationToken.None).ConfigureAwait(false);

        evt.CustomerName.ShouldBe(
            afterFirst,
            "the second encrypt changed the stored value, so re-encryption is possible and the no-double-wrap "
            + "property rests on nobody ever calling it twice");
    }

    /// <remarks>
    /// The name is declared because an assembly-wide startup validator enumerates every <see cref="IDomainEvent"/>
    /// in the test assembly and fails the container when one does not declare a name. A fixture is not exempt:
    /// adding this type is what the validator reacts to, and nothing in the change it proves would reveal that.
    /// </remarks>
    [MessageName("Test.EncryptIdempotenceAcrossFailedAppend.PersonalOrderPlaced")]
    private sealed class PersonalOrderPlaced : IDomainEvent
    {
        [DataSubjectId]
        public string SubjectId { get; set; } = string.Empty;

        [PersonalData]
        public string? CustomerName { get; set; }

        public string EventId { get; set; } = "evt-1";

        public DateTimeOffset OccurredAt { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public IDictionary<string, object>? Metadata { get; set; }
    }
}
