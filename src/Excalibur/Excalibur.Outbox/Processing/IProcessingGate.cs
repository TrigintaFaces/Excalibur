// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox.Processing;

/// <summary>
/// Gate that controls whether background processing should execute on this instance.
/// </summary>
/// <remarks>
/// <para>
/// When registered in DI, outbox and inbox background services check this gate
/// before each processing cycle. If <see cref="ShouldProcess"/> returns
/// <see langword="false"/>, the cycle is skipped and the service waits for
/// the next polling interval.
/// </para>
/// <para>
/// The primary use case is leader election: only the leader instance processes
/// outbox/inbox messages. Register via <c>WithLeaderElection()</c> on the
/// outbox or inbox builder.
/// </para>
/// </remarks>
public interface IProcessingGate
{
	/// <summary>
	/// Gets a value indicating whether processing should proceed.
	/// </summary>
	/// <value><see langword="true"/> if processing should proceed; otherwise, <see langword="false"/>.</value>
	bool ShouldProcess { get; }
}
