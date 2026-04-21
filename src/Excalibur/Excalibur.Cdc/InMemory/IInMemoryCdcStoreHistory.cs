// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.InMemory;

/// <summary>
/// Provides history query operations for in-memory CDC stores.
/// Implementations should implement this alongside <see cref="IInMemoryCdcStore"/>.
/// </summary>
public interface IInMemoryCdcStoreHistory
{
	/// <summary>Gets all changes in the history.</summary>
	IReadOnlyList<InMemoryCdcChange> GetHistory();
}
