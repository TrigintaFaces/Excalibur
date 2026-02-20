// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.A3.Authorization.Requests;

/// <summary>
/// Represents a notification that requires authorization.
/// </summary>
/// <remarks>
/// Combines the functionalities of <see cref="IDispatchMessage" /> and <see cref="IRequireAuthorization" />, enabling access control
/// for notifications within the system.
/// </remarks>
public interface IAuthorizeNotification : IDispatchMessage, IRequireAuthorization;
