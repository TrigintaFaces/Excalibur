// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

internal static class ProbeMessageAssertions
{
    internal static List<string> MessageMismatches(OutboundMessage expected, OutboundMessage actual)
    {
        var differences = new List<string>();
        if (!actual.Payload.AsSpan().SequenceEqual(expected.Payload))
            differences.Add("Payload(hex): expected=" + Convert.ToHexString(expected.Payload) + "; actual=" + Convert.ToHexString(actual.Payload));
        Compare("Id", expected.Id, actual.Id);
        Compare("MessageType", expected.MessageType, actual.MessageType);
        Compare("Destination", expected.Destination, actual.Destination);
        Compare("TenantId", expected.TenantId, actual.TenantId);
        Compare("CorrelationId", expected.CorrelationId, actual.CorrelationId);
        Compare("CausationId", expected.CausationId, actual.CausationId);
        Compare("PartitionKey", expected.PartitionKey, actual.PartitionKey);
        Compare("GroupKey", expected.GroupKey, actual.GroupKey);
        if (expected.SequenceNumber != actual.SequenceNumber)
            differences.Add("SequenceNumber: expected=" + expected.SequenceNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                "; actual=" + actual.SequenceNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return differences;

        void Compare(string name, string? wanted, string? observed)
        {
            if (wanted != observed) differences.Add(name + ": expected=" + (wanted ?? "<null>") + "; actual=" + (observed ?? "<null>"));
        }
    }
}
