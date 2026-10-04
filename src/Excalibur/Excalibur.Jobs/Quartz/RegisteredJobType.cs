// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.RegularExpressions;

namespace Excalibur.Jobs.Quartz;

// Provider-local registrations root the DI type. Ignore assembly version metadata at every
// generic nesting level, but preserve type names, assembly names, and generic argument order.
internal sealed partial record RegisteredJobType(Type Type)
{
	internal bool Matches(string identity)
	{
		var normalized = AssemblyMetadata().Replace(identity, string.Empty);
		return string.Equals(normalized, AssemblyMetadata().Replace(Type.FullName ?? Type.Name, string.Empty), StringComparison.Ordinal)
			|| string.Equals(normalized, AssemblyMetadata().Replace(Type.AssemblyQualifiedName ?? Type.Name, string.Empty), StringComparison.Ordinal);
	}

	[GeneratedRegex(@",\s*(?:Version|Culture|PublicKeyToken)=[^,\]]+", RegexOptions.CultureInvariant)]
	private static partial Regex AssemblyMetadata();
}

internal sealed record RegisteredContextJob(string Key);
