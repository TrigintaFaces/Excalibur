// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Saga.Abstractions;

/// <summary>Opaque provider-issued expected held-state handle, bound to selected physical target and timer epoch.</summary>
/// <remarks>Constructing a handle grants no authority; the provider must validate its complete retained binding.</remarks>
public sealed class SagaTimeoutHoldToken
{
    /// <summary>Initializes an opaque expected-state handle; provider validation remains mandatory.</summary>
    /// <param name="value">The provider-issued nonempty binding.</param>
    public SagaTimeoutHoldToken(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        Value = value;
    }
    /// <summary>Gets the uninterpreted provider binding.</summary>
    public string Value { get; }
}

