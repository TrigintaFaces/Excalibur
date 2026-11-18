// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Aot.Sample.Messages;

/// <summary>Application payload persisted by the native outbox authority control.</summary>
[MessageName("native-outbox-authority")]
public sealed record OutboxAuthorityMessage : IDispatchEvent
{
    /// <summary>Gets the scenario's observable message label.</summary>
    public required string Label { get; init; }

    /// <summary>Gets a nonzero payload value checked after generated deserialization.</summary>
    public required int Sequence { get; init; }
}
