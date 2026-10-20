// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;
using System.Text.Json.Serialization;
using Excalibur.Dispatch;
using Excalibur.Dispatch.ErrorHandling;

namespace Excalibur.Outbox;

/// <summary>Persists a pending disposition without serializing exception types or changing message headers.</summary>
internal sealed class OutboxFailureEvidence
{
    private const string Prefix = "EXCALIBUR-OUTBOX-DISPOSITION:";
    public int Version { get; set; } = 1;
    public string Classification { get; set; } = "Unknown";
    public string Reason { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public int Attempts { get; set; }

    internal string Encode() => Prefix + JsonSerializer.Serialize(this, OutboxFailureEvidenceJsonContext.Default.OutboxFailureEvidence);

    internal static bool IsPending(string? lastError) => lastError?.StartsWith(Prefix, StringComparison.Ordinal) == true;

    internal static bool TryRead(string? lastError, out OutboxFailureEvidence? evidence)
    {
        evidence = null;
        if (lastError is null || !lastError.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }
        try
        {
            evidence = JsonSerializer.Deserialize(lastError.AsSpan(Prefix.Length), OutboxFailureEvidenceJsonContext.Default.OutboxFailureEvidence);
            return evidence is { Version: 1, Attempts: >= 0 } && !string.IsNullOrWhiteSpace(evidence.Classification)
                && Enum.TryParse<DeadLetterReason>(evidence.Reason, out var reason) && Enum.IsDefined(reason)
                && string.Equals(reason.ToString(), evidence.Reason, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

[JsonSerializable(typeof(OutboxFailureEvidence))]
internal sealed partial class OutboxFailureEvidenceJsonContext : JsonSerializerContext;
