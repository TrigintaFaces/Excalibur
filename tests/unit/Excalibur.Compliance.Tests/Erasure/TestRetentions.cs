// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Erasure;

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// Retention registries for arms that are not about retention.
/// </summary>
/// <remarks>
/// <para>
/// The erasure service takes its registry as a REQUIRED dependency, so an arm about something else still
/// has to say what it declares. <see cref="None"/> is how it says "nothing" — a real registry over an
/// empty declaration set, which is a VALUE. It is deliberately not a null and deliberately not a fake:
/// the registration used to omit the dependency altogether and the service accepted it, which left every
/// declared retention invisible in production while every hand-built arm stayed green.
/// </para>
/// </remarks>
internal static class TestRetentions
{
	/// <summary>
	/// Gets a real registry that declares nothing.
	/// </summary>
	/// <value>An empty registry: every aggregate type is erasable, which is the pre-retention behaviour.</value>
	public static IErasureRetentionRegistry None { get; } =
		new ErasureRetentionRegistry(Options.Create(new AggregateRetentionOptions()));
}
