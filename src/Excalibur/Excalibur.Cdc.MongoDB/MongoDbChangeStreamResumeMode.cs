// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Cdc.MongoDB;

/// <summary>
/// Identifies which change-stream resume option a stored resume token must be reopened with.
/// </summary>
/// <remarks>
/// <para>
/// MongoDB exposes two ways to reopen a change stream from a token, and they are not interchangeable.
/// <c>resumeAfter</c> reopens an ordinary checkpoint; it cannot carry a stream past an <c>invalidate</c>
/// event. <c>startAfter</c> can, and is the documented way to continue watching a namespace that was
/// dropped, renamed, or whose database was dropped. The server rejects a request that supplies both.
/// </para>
/// <para>
/// The mode therefore travels with the token rather than being decided at the point of reopening: a
/// checkpoint written just past an invalidation is only usable as a <see cref="StartAfter"/>, and that
/// must survive a process restart along with the token itself.
/// </para>
/// </remarks>
public enum MongoDbChangeStreamResumeMode
{
	/// <summary>
	/// Reopen with <c>resumeAfter</c> — the ordinary checkpoint case.
	/// </summary>
	ResumeAfter = 0,

	/// <summary>
	/// Reopen with <c>startAfter</c> — required to advance past an <c>invalidate</c> event.
	/// </summary>
	StartAfter = 1,
}
