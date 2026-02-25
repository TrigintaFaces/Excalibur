// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Application.Requests.Jobs;

namespace Excalibur.A3.Authorization.Requests;

/// <summary>
/// Represents a job that requires authorization.
/// </summary>
/// <remarks>
/// Combines the functionalities of <see cref="IJob" /> and <see cref="IAuthorizeCommand" />, ensuring that the job can enforce access
/// control and permissions checks before execution.
/// </remarks>
public interface IAuthorizeJob : IJob, IRequireAuthorization;
