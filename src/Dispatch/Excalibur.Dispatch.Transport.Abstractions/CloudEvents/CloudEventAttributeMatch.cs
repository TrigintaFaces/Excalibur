// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// How a protocol binding names CloudEvents attributes in binary mode.
/// </summary>
internal enum CloudEventAttributeMatch
{
	/// <summary>
	/// The transport has no binding, so it offers no binary mode and no inbound message is read as one.
	/// Structured mode is unaffected — it is recognised by content type and needs no binding.
	/// </summary>
	None = 0,

	/// <summary>
	/// Attributes are carried under their bare specification names, with no prefix. Assigned by exactly
	/// one binding; honouring it elsewhere is how an ordinary property becomes an attribute by accident.
	/// </summary>
	Bare,

	/// <summary>
	/// Attributes are carried under one or more binding-assigned prefixes.
	/// </summary>
	Prefixed,
}
