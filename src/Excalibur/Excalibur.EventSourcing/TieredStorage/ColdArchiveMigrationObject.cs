// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Cryptography;

namespace Excalibur.EventSourcing.TieredStorage;

/// <summary>Owns complete bytes and the revision returned by the same object read.</summary>
/// <remarks>The revision is an opaque conditional-write token, not a globally unique incarnation.</remarks>
internal sealed class ColdArchiveMigrationObject
{
	private readonly byte[] _content;

	internal ColdArchiveMigrationObject(ReadOnlyMemory<byte> content, string revision)
	{
		ArgumentException.ThrowIfNullOrEmpty(revision);
		_content = content.ToArray();
		Revision = revision;
		Sha256 = Convert.ToHexString(SHA256.HashData(_content));
	}

	internal string Revision { get; }
	internal string Sha256 { get; }
	internal byte[] CopyContent() => _content.ToArray();
	internal bool HasSameContent(ColdArchiveMigrationObject other) => _content.AsSpan().SequenceEqual(other._content);
}
