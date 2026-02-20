// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Threading;

/// <summary>
/// Marker interface for messages that should be executed in the background.
/// </summary>
/// <remarks>
/// Messages implementing this interface will be processed asynchronously on a background thread, with the handler immediately returning a
/// 202 Accepted response to the caller. Messages expecting typed results cannot implement this interface as background execution does not
/// support returning values to the caller.
/// </remarks>
public interface IExecuteInBackground
{
	/// <summary>
	/// Gets a value indicating whether exceptions during background execution should be propagated to the global error handler, potentially
	/// causing application shutdown.
	/// </summary>
	/// <remarks>
	/// When true, critical exceptions will be logged and rethrown to trigger global error handling. When false, exceptions are logged but
	/// not propagated. Default should be false for most scenarios.
	/// </remarks>
	/// <value>
	/// A value indicating whether exceptions during background execution should be propagated to the global error handler, potentially
	/// causing application shutdown.
	/// </value>
	bool PropagateExceptions { get; }
}
