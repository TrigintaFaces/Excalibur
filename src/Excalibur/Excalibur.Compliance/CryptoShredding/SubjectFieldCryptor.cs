// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;

using Excalibur.Compliance.Encryption;

namespace Excalibur.Compliance.CryptoShredding;

/// <summary>
/// Encrypts and decrypts the <see cref="PersonalDataAttribute"/>-annotated fields of a record under the key
/// of the data subject named by its <see cref="DataSubjectIdAttribute"/>-annotated member, so destroying the
/// subject's key crypto-shreds only that subject's personal fields while the record's non-personal structure
/// stays plaintext (and therefore still loads after erasure).
/// </summary>
/// <remarks>
/// <para>
/// A record with no <see cref="DataSubjectIdAttribute"/>-annotated member is left untouched (the caller's
/// existing purpose-key behavior is unchanged) — per-subject protection is additive. A record that
/// <em>declares</em> a data subject but whose identifier is absent, blank, or cannot be formatted to a stable
/// string is rejected rather than skipped: it carries <see cref="PersonalDataAttribute"/> fields and no key
/// exists to protect them, so proceeding would persist plaintext personal data.
/// </para>
/// <para>
/// Scope: one <see cref="DataSubjectIdAttribute"/> per record; <see cref="string"/> and
/// <see cref="byte"/><c>[]</c> personal-data properties. The data subject's identifier may be a
/// <see cref="string"/>, a <see cref="Guid"/>, or any integral type — it is formatted invariantly, never
/// cast. Reflection over arbitrary record types is
/// trim/AOT-hostile (consistent with the existing <c>PersonalDataAnnotationSource</c>); a source-generated
/// field map is a tracked hardening follow-up.
/// </para>
/// </remarks>
public sealed class SubjectFieldCryptor
{
    private static readonly ConcurrentDictionary<Type, TypeFieldPlan> Plans = new();

    private readonly IFieldEncryptor _fieldEncryptor;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubjectFieldCryptor"/> class.
    /// </summary>
    /// <param name="fieldEncryptor">The per-subject field encryptor used to protect personal-data fields.</param>
    public SubjectFieldCryptor(IFieldEncryptor fieldEncryptor)
    {
        _fieldEncryptor = fieldEncryptor ?? throw new ArgumentNullException(nameof(fieldEncryptor));
    }

    /// <summary>
    /// Encrypts each personal-data field of <paramref name="record"/> in place under its data subject's key.
    /// </summary>
    /// <remarks>
    /// A record whose type declares no <c>[DataSubjectId]</c> property is a no-op: per-subject protection is
    /// additive over whatever at-rest encryption already applies. The two other cases FAIL CLOSED rather than
    /// passing through. A type that declares a data subject but carries no <c>[PersonalData]</c> field, and a
    /// record whose declared data-subject identifier is null or blank, both throw
    /// <see cref="EncryptionException"/> -- the second because the record has personal data and no key under
    /// which to protect it, so proceeding would persist it in plaintext.
    /// </remarks>
    /// <param name="record">The record whose personal-data fields are encrypted in place.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public ValueTask EncryptFieldsAsync(object record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        return EncryptFieldsAsync(record, GetPlanForInstance(record), cancellationToken);
    }

    /// <summary>
    /// Encrypts the fields a supplied plan names, instead of a plan derived by reflecting over the record's
    /// annotations.
    /// </summary>
    /// <remarks>
    /// Exists so the encrypt path can be exercised without compiling a <see cref="PersonalDataAttribute"/>
    /// type into an assembly the erasure coverage scan reaches: such a type is, correctly, reported as
    /// annotated personal data that no discovered location covers. A plan must still come from
    /// <see cref="TypeFieldPlan.Describe"/>, which applies the same encryptability rule as reflection.
    /// </remarks>
    internal async ValueTask EncryptFieldsAsync(object record, TypeFieldPlan plan, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(plan);

        // Not a data-subject entity (no [DataSubjectId]) — nothing to protect, legitimate no-op.
        if (plan.SubjectIdProperty is null)
        {
            return;
        }

        // Fail-closed registry-mismatch guard: a [DataSubjectId]-bearing type declares it carries personal
        // data, so a plan that resolves ZERO [PersonalData] fields means the annotations were lost (e.g.
        // trimmed away) — encrypting nothing would silently persist plaintext PII (a GDPR breach). Refuse
        // rather than proceed. (The DAM rooting on GetPlan keeps the annotations under trimming; this is the
        // defense-in-depth backstop.)
        if (plan.PersonalDataProperties.Length == 0)
        {
            throw new EncryptionException(
                $"Type '{record.GetType().FullName}' declares a data subject ([DataSubjectId]) but resolved no "
                + "[PersonalData] fields to encrypt. This indicates the classification annotations were lost "
                + "(e.g. trimmed) — refusing to persist unencrypted personal data.");
        }

        // Fail-closed missing-subject guard, symmetric with the annotation guard above. Reaching here means
        // the type declares a data subject AND resolved [PersonalData] fields to protect; an absent or blank
        // identifier therefore names no key under which to encrypt them. Returning would persist those fields
        // as plaintext for precisely the records that declared they carry personal data. There is no key to
        // derive and no safe way to proceed, so refuse.
        var subjectId = ResolveSubjectId(plan.SubjectIdProperty, record);
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            throw new EncryptionException(
                $"Type '{record.GetType().FullName}' declares a data subject "
                + $"([DataSubjectId] on '{plan.SubjectIdProperty.Name}') and carries [PersonalData] fields, but the "
                + "identifier is null or blank. No subject key can be derived — refusing to persist unencrypted "
                + "personal data.");
        }

        foreach (var property in plan.PersonalDataProperties)
        {
            // NO DOUBLE-WRAP, and it is enforced here because here is the only place it can be.
            //
            // This method MUTATES THE CALLER'S OBJECT IN PLACE, and the operation that follows it -- the
            // append, the save, the send -- can fail. When it does, the caller still holds the record with
            // its fields already replaced by envelopes, and the natural response to a transient fault is to
            // retry the same instance. Without this test the retry reads an envelope, cannot tell it from
            // plaintext (the read below is a raw GetValue with no marker test), and encrypts it AGAIN. What
            // is then stored decrypts in one pass to an envelope string rather than to the subject's data --
            // corruption that presents as a successful decryption, which is the worst shape it could take.
            //
            // The marker is what makes the two cases distinguishable, and consulting it makes re-encryption
            // IMPOSSIBLE rather than merely unlikely: WriteEnvelope marks every value this type writes, so a
            // marked value is necessarily one we produced and necessarily must not be wrapped again.
            //
            // It must remain a SKIP and never a throw. An unmarked value is the legacy form -- written before
            // the marker existed -- and several read paths depend on unmarked meaning plaintext, so refusing
            // the unmarked case would reject exactly the records this framework wrote first.
            if (EncryptedFieldBinding.IsMarkedEncrypted(property, record))
            {
                continue;
            }

            var plaintext = ReadFieldBytes(property, record);
            if (plaintext is null)
            {
                continue;
            }

            var envelope = await _fieldEncryptor.EncryptAsync(subjectId, plaintext, cancellationToken)
                .ConfigureAwait(false);
            WriteEnvelope(property, record, envelope);
        }
    }

    /// <summary>
    /// Gets a value indicating whether <paramref name="recordType"/> declares any encryptable
    /// <see cref="PersonalDataAttribute"/> field.
    /// </summary>
    /// <remarks>
    /// Lets a caller decide whether a record needs the encrypt/decrypt round-trip at all WITHOUT materializing
    /// it. The answer is a property of the TYPE, and the plan behind it is cached, so asking is a dictionary
    /// lookup rather than a per-record reflection walk.
    /// </remarks>
    /// <param name="recordType">The record type to inspect.</param>
    /// <returns>
    /// <see langword="true"/> when the type declares at least one encryptable personal-data field; otherwise
    /// <see langword="false"/>, meaning <see cref="EncryptFieldsAsync(object, CancellationToken)"/> and <see cref="DecryptFieldsAsync(object, CancellationToken)"/>
    /// would both leave a record of this type untouched.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="recordType"/> is null.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2067:UnrecognizedReflectionPattern",
        Justification = "A runtime-resolved record type, matching IEventSerializer.DeserializeEvent(byte[], Type) "
            + "which this answer gates; GetPlan is DAM-rooted and the instance path carries the same suppression "
            + "for the same reason. Annotating the parameter instead would only move the unprovable step to "
            + "every caller.")]
    public static bool HasPersonalDataFields(Type recordType)
    {
        ArgumentNullException.ThrowIfNull(recordType);

        return GetPlan(recordType).PersonalDataProperties.Length != 0;
    }

    /// <summary>
    /// Decrypts each personal-data field of <paramref name="record"/> in place. A field whose subject key has
    /// been destroyed decrypts to <see langword="null"/> (a tombstone), leaving the rest of the record intact.
    /// </summary>
    /// <param name="record">The record whose personal-data fields are decrypted in place.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public ValueTask DecryptFieldsAsync(object record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        return DecryptFieldsAsync(record, GetPlanForInstance(record), cancellationToken);
    }

    /// <summary>Decrypts the fields a supplied plan names. See the plan-taking encrypt overload.</summary>
    internal async ValueTask DecryptFieldsAsync(object record, TypeFieldPlan plan, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.PersonalDataProperties.Length == 0)
        {
            return;
        }

        foreach (var property in plan.PersonalDataProperties)
        {
            var enveloped = ReadEnvelopeBytes(property, record);
            if (enveloped is null || !EncryptedData.IsFieldEncrypted(enveloped))
            {
                continue;
            }

            var envelope = DeserializeEnvelope(enveloped);
            var plaintext = await _fieldEncryptor.DecryptAsync(envelope, cancellationToken).ConfigureAwait(false);
            WriteFieldBytes(property, record, plaintext);
        }
    }

    private static byte[]? ReadFieldBytes(PropertyInfo property, object record)
    {
        var value = property.GetValue(record);
        return value switch
        {
            null => null,
            string s => Encoding.UTF8.GetBytes(s),
            byte[] b => b,
            _ => null,
        };
    }

    private static void WriteFieldBytes(PropertyInfo property, object record, byte[]? plaintext)
    {
        if (property.PropertyType == typeof(string))
        {
            property.SetValue(record, plaintext is null ? null : Encoding.UTF8.GetString(plaintext));
        }
        else
        {
            property.SetValue(record, plaintext);
        }
    }

    private static void WriteEnvelope(PropertyInfo property, object record, EncryptedData envelope)
    {
        // Delegates to the single owner of the marker rather than re-deriving the encoding here. A string
        // property receives EncryptedFieldBinding.StringEnvelopePrefix followed by Base64; a byte[] property
        // receives the frame verbatim. Marking the write is what lets a reader tell a stored ciphertext from
        // a value that was never encrypted, which bare Base64 cannot express - see ReadEnvelopeBytes.
        EncryptedFieldBinding.WriteEnvelope(property, record, SerializeEnvelope(envelope));
    }

    /// <summary>
    /// Reads a field's stored value as envelope bytes, accepting both the marked form written today and the
    /// unmarked form written before the marker existed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two branches are not interchangeable and the order matters. A value carrying
    /// <see cref="EncryptedFieldBinding.StringEnvelopePrefix"/> is unambiguously ours, because the prefix ends
    /// in a character outside the Base64 alphabet and so cannot be produced by encoding. A value without it
    /// may still be a ciphertext this framework wrote before the marker was introduced, so it is decoded and
    /// admitted when the decoded bytes carry <see cref="EncryptedData.MagicBytes"/>.
    /// </para>
    /// <para>
    /// That second branch is the decode-first test the marker exists to avoid, and it keeps that test's known
    /// weakness: a plaintext whose Base64 decodes to magic-prefixed bytes is admitted as an envelope. It is
    /// retained deliberately and only for legacy values, because the alternative is worse in exactly the
    /// direction that matters here — refusing them would treat every personal-data field encrypted before this
    /// change as plaintext and hand its ciphertext back to a caller as though it had been decrypted. A false
    /// positive fails loudly at decryption; a false negative fails silently, on erasure-critical data.
    /// </para>
    /// <para>
    /// The ambiguity is confined to unmarked values, and it does NOT decay on its own everywhere. A store
    /// that rewrites a record re-marks it; an append-only store never rewrites what it already holds, so its
    /// existing entries keep the unmarked form for as long as they are retained. The legacy branch is
    /// therefore permanent, not transitional, and removing it would strand that data.
    /// </para>
    /// </remarks>
    private static byte[]? ReadEnvelopeBytes(PropertyInfo property, object record)
    {
        // TryReadEnvelope answers the marked question itself: it returns false for an unmarked value and
        // THROWS for one that is marked but unreadable, so there is no third outcome to fold into a null.
        // The earlier form guarded with IsMarkedEncrypted and then mapped a false result to null, which
        // read as a silent skip of a value already known to be marked — a branch that cannot be reached
        // but that states the opposite of the contract to anyone reading it.
        if (EncryptedFieldBinding.TryReadEnvelope(property, record, out var marked))
        {
            return marked;
        }

        return property.GetValue(record) switch
        {
            null => null,
            byte[] b => b,
            string s => TryFromBase64(s),
            _ => null,
        };
    }

    private static byte[]? TryFromBase64(string value)
    {
        var buffer = new byte[value.Length];
        return Convert.TryFromBase64String(value, buffer, out var written) ? buffer[..written] : null;
    }

    private static byte[] SerializeEnvelope(EncryptedData envelope)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(envelope, EncryptionJsonContext.Default.EncryptedData);
        var framed = new byte[EncryptedData.MagicBytes.Length + json.Length];
        EncryptedData.MagicBytes.CopyTo(framed.AsSpan());
        json.CopyTo(framed, EncryptedData.MagicBytes.Length);
        return framed;
    }

    private static EncryptedData DeserializeEnvelope(byte[] framed)
    {
        var json = framed.AsSpan(EncryptedData.MagicBytes.Length);
        return JsonSerializer.Deserialize(json, EncryptionJsonContext.Default.EncryptedData)
            ?? throw new EncryptionException(Resources.Encryption_EncryptedDataEnvelopeDeserializeFailed);
    }

    // The one unavoidable trim-unsafe hop: an arbitrary record arrives as `object`, so its runtime type from
    // `object.GetType()` carries no DAM guarantee. Narrowly suppressed here (not blanket over GetPlan) — GetPlan
    // itself is DAM-rooted (PublicProperties preserved), and the caller fails closed when the plan is empty for
    // a data-subject type, so a trimmed-away annotation cannot silently persist plaintext. Source-generated
    // field map remains the tracked AOT-hardening follow-up.
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2072:UnrecognizedReflectionPattern",
        Justification = "object.GetType() over a consumer record; GetPlan is DAM-rooted and the caller throws "
            + "rather than persist plaintext when a [DataSubjectId] type resolves no [PersonalData] fields.")]
    private static TypeFieldPlan GetPlanForInstance(object record) => GetPlan(record.GetType());

    private static TypeFieldPlan GetPlan(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
    {
        if (Plans.TryGetValue(type, out var cached))
        {
            return cached;
        }

        // The plan is built HERE rather than inside a GetOrAdd value factory: the annotation on `type`
        // does not flow into a static lambda's own parameter, so a factory hides this property walk from
        // the trimmer and the preservation it needs becomes unprovable. Two threads racing a cold type
        // both build the same plan and one wins the add, which is the semantics GetOrAdd already had.
        PropertyInfo? subjectIdProperty = null;
        var personalData = new List<PropertyInfo>();

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetCustomAttribute<DataSubjectIdAttribute>() is not null)
            {
                subjectIdProperty ??= property;
            }

            if (TypeFieldPlan.IsEncryptable(property)
                && property.GetCustomAttribute<PersonalDataAttribute>() is not null)
            {
                personalData.Add(property);
            }
        }

        return Plans.GetOrAdd(type, new TypeFieldPlan(subjectIdProperty, [.. personalData]));
    }

    /// <summary>
    /// Resolves the data subject's identifier to the stable string under which its key is derived.
    /// </summary>
    /// <remarks>
    /// A <see cref="DataSubjectIdAttribute"/>-annotated member is commonly a <see cref="Guid"/> or an integral
    /// identifier, not only a <see cref="string"/>. Casting the value with <c>as string</c> yields
    /// <see langword="null"/> for every one of those, which reads as "no resolvable data subject" and skips
    /// encryption entirely — persisting plaintext personal data for exactly the records that declared they
    /// carry it. The value is therefore formatted, not cast.
    /// <para>
    /// Formatting is culture-invariant so the derived key is stable across hosts and locales. A type that
    /// cannot be formatted invariantly is rejected rather than passed through <see cref="object.ToString"/>:
    /// a type without a meaningful override returns its type name, which would silently derive one shared key
    /// for every data subject of that type. Refusing is the only safe outcome; a fabricated key is worse than
    /// no key.
    /// </para>
    /// </remarks>
    private static string? ResolveSubjectId(PropertyInfo subjectIdProperty, object record)
    {
        var value = subjectIdProperty.GetValue(record);

        return value switch
        {
            null => null,
            string s => s,
            // Guid, int, long, and the other primitive identifier types are all IFormattable.
            IFormattable formattable => formattable.ToString(format: null, CultureInfo.InvariantCulture),
            _ => throw new EncryptionException(
                $"The [DataSubjectId] member '{subjectIdProperty.DeclaringType?.FullName}.{subjectIdProperty.Name}' "
                + $"has type '{value.GetType().FullName}', which cannot be formatted to a stable subject "
                + "identifier. Use a string, Guid, or integral identifier — refusing to derive a subject key "
                + "that would be shared across data subjects."),
        };
    }

    /// <summary>The data-subject identifier and the personal-data fields of one record type.</summary>
    internal sealed record TypeFieldPlan(PropertyInfo? SubjectIdProperty, PropertyInfo[] PersonalDataProperties)
    {
        /// <summary>
        /// The single rule for which properties can carry an encrypted envelope. Reflection and a supplied plan
        /// both pass through it, so a supplied plan cannot name a field the encrypt path would mishandle.
        /// </summary>
        internal static bool IsEncryptable(PropertyInfo property) =>
            property.CanRead
            && property.CanWrite
            && (property.PropertyType == typeof(string) || property.PropertyType == typeof(byte[]));

        /// <summary>Describes a plan explicitly, validated by the same rule reflection applies.</summary>
        internal static TypeFieldPlan Describe(PropertyInfo? subjectIdProperty, params PropertyInfo[] personalDataProperties)
        {
            ArgumentNullException.ThrowIfNull(personalDataProperties);

            foreach (var property in personalDataProperties)
            {
                ArgumentNullException.ThrowIfNull(property);
                if (!IsEncryptable(property))
                {
                    throw new ArgumentException(
                        $"Property '{property.DeclaringType?.Name}.{property.Name}' cannot carry an encrypted field: "
                        + "it must be a readable and writable string or byte[].",
                        nameof(personalDataProperties));
                }
            }

            return new TypeFieldPlan(subjectIdProperty, personalDataProperties);
        }
    }
}
