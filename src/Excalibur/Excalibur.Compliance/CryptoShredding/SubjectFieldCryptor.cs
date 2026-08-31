// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
    /// No-op when the record has no personal-data fields or no resolvable data-subject id.
    /// </summary>
    /// <param name="record">The record whose personal-data fields are encrypted in place.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public async ValueTask EncryptFieldsAsync(object record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        var plan = GetPlanForInstance(record);

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
    /// Decrypts each personal-data field of <paramref name="record"/> in place. A field whose subject key has
    /// been destroyed decrypts to <see langword="null"/> (a tombstone), leaving the rest of the record intact.
    /// </summary>
    /// <param name="record">The record whose personal-data fields are decrypted in place.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public async ValueTask DecryptFieldsAsync(object record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        var plan = GetPlanForInstance(record);
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
        var framed = SerializeEnvelope(envelope);
        if (property.PropertyType == typeof(string))
        {
            property.SetValue(record, Convert.ToBase64String(framed));
        }
        else
        {
            property.SetValue(record, framed);
        }
    }

    private static byte[]? ReadEnvelopeBytes(PropertyInfo property, object record)
    {
        var value = property.GetValue(record);
        return value switch
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

            if (property.CanRead
                && property.CanWrite
                && property.GetCustomAttribute<PersonalDataAttribute>() is not null
                && (property.PropertyType == typeof(string) || property.PropertyType == typeof(byte[])))
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

    private sealed record TypeFieldPlan(PropertyInfo? SubjectIdProperty, PropertyInfo[] PersonalDataProperties);
}
