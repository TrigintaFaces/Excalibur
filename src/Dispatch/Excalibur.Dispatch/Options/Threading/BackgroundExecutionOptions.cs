// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Options.Threading;

/// <summary>
/// Configuration options for executing messages in the background.
/// </summary>
public sealed class BackgroundExecutionOptions
{
	/// <summary>
	/// Gets or sets what happens when a message executed in the background fails.
	/// </summary>
	/// <value>
	/// The failure policy. The default is <see cref="BackgroundExecutionExceptionBehavior.LogOnly"/>.
	/// </value>
	public BackgroundExecutionExceptionBehavior ExceptionBehavior { get; set; } = BackgroundExecutionExceptionBehavior.LogOnly;
}
