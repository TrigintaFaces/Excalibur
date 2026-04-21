// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;

using Dapper;

namespace Excalibur.Data;

/// <summary>
/// Applies Excalibur-wide Dapper defaults at assembly load time.
/// </summary>
/// <remarks>
/// <para>
/// S804 bd-sdhocq A6: <c>AddExcaliburDataServices(...)</c> was deleted. The Dapper
/// snake-case column name binding (<c>DefaultTypeMap.MatchNamesWithUnderscores = true</c>)
/// previously lived inside that method body and would fire only when consumers explicitly
/// opted-in. A module initializer preserves the same default without requiring a
/// composition-root registration call.
/// </para>
/// <para>
/// Consumers who need strict property-name binding can override the flag after
/// <c>Excalibur.Data</c> loads. Nothing in the framework requires case-exact matching.
/// </para>
/// </remarks>
internal static class DapperDefaultsInitializer
{
#pragma warning disable CA2255 // ModuleInitializer usage: library-wide Dapper default applied at load time; supersedes the deleted AddExcaliburDataServices() aggregator.
	[ModuleInitializer]
#pragma warning restore CA2255
	internal static void Initialize()
	{
		DefaultTypeMap.MatchNamesWithUnderscores = true;
	}
}
