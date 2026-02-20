// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Middleware function.
/// </summary>
/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
public delegate Task MiddlewareFunc<TContext>(TContext context, Func<TContext, Task> next);
