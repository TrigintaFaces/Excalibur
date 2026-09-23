// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Options.Threading;

/// <summary>
/// Specifies what happens when a message executed in the background fails.
/// </summary>
/// <remarks>
/// This is a host-level policy, in the same shape as <c>HostOptions.BackgroundServiceExceptionBehavior</c>:
/// it is configured once for the application, never per message.
/// </remarks>
public enum BackgroundExecutionExceptionBehavior
{
	/// <summary>
	/// The failure is logged as an error and the host keeps running. This is the default.
	/// </summary>
	LogOnly = 0,

	/// <summary>
	/// The failure is logged, and the host is then asked to stop through
	/// <c>IHostApplicationLifetime.StopApplication()</c>. The host must provide
	/// <c>IHostApplicationLifetime</c>; startup fails if it does not.
	/// </summary>
	StopHost = 1,
}
