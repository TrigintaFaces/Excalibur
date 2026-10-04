// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing;

/// <summary>Immutable result of examining one bounded page of stream identities.</summary>
public sealed class ArchiveScanPage
{
	/// <summary>Initializes a page, copying its candidate collection.</summary>
	/// <param name="candidates">Candidates with pending work from the examined streams.</param>
	/// <param name="continuation">Continuation after the last examined stream, or null at round exhaustion.</param>
	public ArchiveScanPage(IReadOnlyList<ArchiveCandidate> candidates, ArchiveScanCursor? continuation)
	{
		ArgumentNullException.ThrowIfNull(candidates);
		Candidates = Array.AsReadOnly(candidates.ToArray());
		Continuation = continuation;
	}

	/// <summary>Gets candidates with pending work. An empty collection does not imply exhaustion.</summary>
	public IReadOnlyList<ArchiveCandidate> Candidates { get; }

	/// <summary>Gets the continuation, or null when the round is exhausted.</summary>
	public ArchiveScanCursor? Continuation { get; }
}
