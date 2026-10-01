// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using global::Excalibur.Compliance;
using global::Excalibur.Compliance.Erasure;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Erasure;

/// <summary>
/// Retention registries for arms that are not about retention.
/// </summary>
/// <remarks>
/// The erasure service takes its registry as a REQUIRED dependency, so an arm about something else still
/// has to say what it declares. <see cref="None"/> is how it says "nothing" — a real registry over an
/// empty declaration set, which is a VALUE rather than an absence. The registration used to omit the
/// dependency entirely and the constructor accepted it, which left every declared retention invisible in
/// production while every hand-built arm stayed green.
/// </remarks>
internal static class TestRetentions
{
	/// <summary>
	/// Gets a real registry that declares nothing.
	/// </summary>
	/// <value>An empty registry: every aggregate type is erasable, which is the pre-retention behaviour.</value>
	public static IErasureRetentionRegistry None { get; } =
		new ErasureRetentionRegistry(global::Microsoft.Extensions.Options.Options.Create(new AggregateRetentionOptions()));
}
