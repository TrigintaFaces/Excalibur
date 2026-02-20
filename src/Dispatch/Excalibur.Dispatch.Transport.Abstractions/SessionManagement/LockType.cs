// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// The type of lock.
/// </summary>
public enum LockType
{
	/// <summary>
	/// A read lock allowing multiple concurrent readers.
	/// </summary>
	Read = 0,

	/// <summary>
	/// A write lock allowing exclusive access.
	/// </summary>
	Write = 1,

	/// <summary>
	/// An upgradeable read lock that can be promoted to write.
	/// </summary>
	UpgradeableRead = 2,
}
