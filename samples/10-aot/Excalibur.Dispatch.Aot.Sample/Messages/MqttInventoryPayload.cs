// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json.Serialization;

namespace Excalibur.Dispatch.Aot.Sample.Messages;

/// <summary>Declares the MQTT smoke payload's wire names independently of serializer naming policies.</summary>
internal sealed record MqttInventoryPayload
{
    [JsonPropertyName("sku")]
    public required string Sku { get; init; }

    [JsonPropertyName("newQuantity")]
    public required int NewQuantity { get; init; }
}
