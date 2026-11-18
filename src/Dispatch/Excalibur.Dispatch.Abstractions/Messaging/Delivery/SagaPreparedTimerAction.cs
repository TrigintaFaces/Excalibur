// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Messaging;

/// <summary>Identifies an ordered timer operation in a prepared saga transition.</summary>
public enum SagaPreparedTimerAction
{
    /// <summary>Unspecified sentinel; invalid when constructing any prepared timer mutation.</summary>
    None = 0,
    /// <summary>Schedules one timeout in the current generation.</summary>
    Schedule = 1,
    /// <summary>Cancels one exact timeout.</summary>
    Cancel = 2,
    /// <summary>Cancels the current generation, permitting a later explicit next generation.</summary>
    CancelAll = 3,
}

