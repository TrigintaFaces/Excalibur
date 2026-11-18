// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Saga.Abstractions;

/// <summary>Current or reconciled disposition of one retained hold receipt.</summary>
public enum SagaTimeoutHoldState
{
    /// <summary>The original source is currently held and excluded from claims.</summary>
    Held = 0,
    /// <summary>The hold was explicitly resumed without granting a dispatch lease.</summary>
    Resumed = 1,
    /// <summary>The original source was explicitly canceled.</summary>
    Cancelled = 2,
    /// <summary>The original source or disposition was replaced.</summary>
    Superseded = 3,
}

