// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Compat.MediatR;

/// <summary>
/// Marker interface for a notification (event) that may be handled by zero or more handlers.
/// Provides the <c>INotification</c> shape used by MediatR-based code; maps to a canonical Dispatch event.
/// </summary>
public interface INotification;
