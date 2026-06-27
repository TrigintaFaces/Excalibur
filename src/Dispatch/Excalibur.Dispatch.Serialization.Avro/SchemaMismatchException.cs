// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

namespace Excalibur.Dispatch.Serialization.Avro;

/// <summary>
/// Thrown when an Avro payload's writer-schema fingerprint does not match the reader's schema and
/// no compatible writer schema can be resolved, so the payload <b>cannot</b> be decoded safely.
/// </summary>
/// <remarks>
/// <para>
/// Apache Avro binary is not self-describing: correct decoding requires the writer schema. The base
/// <see cref="AvroSerializer"/> frames every payload with the Avro single-object-encoding header
/// (<c>0xC3 0x01</c> + the 8-byte little-endian CRC-64-AVRO Rabin fingerprint of the writer schema).
/// On read, a writer/reader fingerprint mismatch with no resolvable writer schema is a
/// <b>fail-closed</b> condition: the serializer throws this exception instead of positionally
/// decoding against the wrong schema (which would silently corrupt field values).
/// </para>
/// <para>
/// This is the structural guarantee behind "no silent corruption on version skew" — a skew is
/// detected and rejected loudly, never mis-decoded.
/// </para>
/// </remarks>
public sealed class SchemaMismatchException : ApiException
{
    private const int SchemaMismatchStatusCode = 400;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaMismatchException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public SchemaMismatchException(string message)
        : base(message) => StatusCode = SchemaMismatchStatusCode;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaMismatchException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public SchemaMismatchException(string message, Exception innerException)
        : base(message, innerException) => StatusCode = SchemaMismatchStatusCode;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaMismatchException"/> class.
    /// </summary>
    public SchemaMismatchException()
        : base("Avro schema mismatch: the payload was written with a schema incompatible with the reader.")
        => StatusCode = SchemaMismatchStatusCode;

    /// <summary>
    /// Gets the type that was being deserialized when the mismatch was detected.
    /// </summary>
    /// <value>The reader type, or <see langword="null"/> if not available.</value>
    public Type? TargetType { get; init; }

    /// <summary>
    /// Gets the writer-schema fingerprint carried by the payload, if available.
    /// </summary>
    /// <value>The CRC-64-AVRO Rabin fingerprint of the writer schema.</value>
    public long? WriterFingerprint { get; init; }

    /// <summary>
    /// Gets the reader-schema fingerprint, if available.
    /// </summary>
    /// <value>The CRC-64-AVRO Rabin fingerprint of the reader schema.</value>
    public long? ReaderFingerprint { get; init; }

    /// <summary>
    /// Creates a <see cref="SchemaMismatchException"/> for a writer/reader fingerprint skew.
    /// </summary>
    /// <param name="type">The reader type.</param>
    /// <param name="writerFingerprint">The writer-schema fingerprint from the payload.</param>
    /// <param name="readerFingerprint">The reader-schema fingerprint.</param>
    /// <returns>A new <see cref="SchemaMismatchException"/> instance.</returns>
    public static SchemaMismatchException Skew(Type type, long writerFingerprint, long readerFingerprint)
        => new(string.Format(
                CultureInfo.InvariantCulture,
                "Avro schema mismatch deserializing '{0}': payload writer-schema fingerprint 0x{1:X16} " +
                "does not match the reader-schema fingerprint 0x{2:X16}, and no compatible writer schema is " +
                "registered. Failing closed to avoid silent positional mis-decode. Register the writer schema " +
                "(schema-evolution) or read with the schema the payload was written with.",
                type.FullName,
                writerFingerprint,
                readerFingerprint))
        {
            TargetType = type,
            WriterFingerprint = writerFingerprint,
            ReaderFingerprint = readerFingerprint,
        };

    /// <summary>
    /// Creates a <see cref="SchemaMismatchException"/> for a payload missing the Avro single-object header.
    /// </summary>
    /// <param name="type">The reader type.</param>
    /// <returns>A new <see cref="SchemaMismatchException"/> instance.</returns>
    public static SchemaMismatchException MissingHeader(Type type)
        => new(string.Format(
                CultureInfo.InvariantCulture,
                "Avro payload for '{0}' is missing the required single-object-encoding header (0xC3 0x01 + " +
                "8-byte schema fingerprint). The writer schema cannot be identified, so the payload cannot be " +
                "decoded safely. Failing closed.",
                type.FullName))
        {
            TargetType = type,
        };
}
