// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox.Processing;

/// <summary>
/// Processing gate backed by a delegate function, typically wired to
/// <c>ILeaderElection.IsLeader</c>.
/// </summary>
internal sealed class DelegateProcessingGate : IProcessingGate
{
	private readonly Func<bool> _shouldProcess;

	/// <summary>
	/// Initializes a new instance of the <see cref="DelegateProcessingGate"/> class.
	/// </summary>
	/// <param name="shouldProcess">Delegate that returns whether processing should proceed.</param>
	public DelegateProcessingGate(Func<bool> shouldProcess)
	{
		_shouldProcess = shouldProcess ?? throw new ArgumentNullException(nameof(shouldProcess));
	}

	/// <inheritdoc/>
	public bool ShouldProcess => _shouldProcess();
}
