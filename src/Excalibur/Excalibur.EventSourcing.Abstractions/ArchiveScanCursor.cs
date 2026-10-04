// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing;

/// <summary>Opaque provider-owned continuation for an archive scan round.</summary>
/// <remarks>
/// Providers derive an immutable implementation bound to their source instance, comparison rules,
/// policy snapshot, horizon and last examined stream. Callers pass it back unchanged. This contract
/// does not provide serialization, persistence across restarts, or portability between provider instances.
/// It is a scheduling continuation, never a subscriber checkpoint or delivery watermark.
/// </remarks>
public abstract class ArchiveScanCursor
{
	/// <summary>Initializes a provider-owned continuation.</summary>
	protected ArchiveScanCursor()
	{
	}
}
