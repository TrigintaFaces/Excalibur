// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Testing.Conformance.DependencyInjection;

/// <summary>
/// Classifies a public builder extension against the Minimal-Wiring DX contract
/// (see <c>management/specs/conformance-minimal-wiring-spec.md</c> §5).
/// </summary>
/// <remarks>
/// <para>
/// The bucket determines which <see cref="MinimalWiringConformanceTestKit{TBuilderExtension}"/>
/// assertion shape applies to an extension. Every row in the authoritative inventory
/// (<c>management/specs/conformance-minimal-wiring-inventory.csv</c>) maps to exactly one bucket.
/// </para>
/// </remarks>
public enum MinimalWiringBucket
{
	/// <summary>
	/// <b>Bucket A — Sensible defaults via <c>TryAdd</c>.</b>
	/// Extension succeeds against an otherwise-empty <c>IServiceCollection</c> by
	/// <c>TryAdd</c>-registering default implementations for every service it resolves at build time.
	/// Consumer-provided registrations win because <c>TryAdd</c> preserves overrides.
	/// </summary>
	SensibleDefaults = 0,

	/// <summary>
	/// <b>Bucket B — Explicit prerequisite with clear registration-time error.</b>
	/// Extension has a non-defaultable dependency (e.g., transport or storage choice) and
	/// fails fast at registration or <c>ValidateOnStart</c> time with a message that names the
	/// required sibling and the call to make it right.
	/// </summary>
	ExplicitPrerequisite = 1,

	/// <summary>
	/// <b>Bucket C — Optional progressive enhancement.</b>
	/// Extension has no hard prerequisite but detects a sibling at build time to enable
	/// enhanced behavior. Both "alone" and "with sibling" paths resolve every advertised
	/// service without exception.
	/// </summary>
	ProgressiveEnhancement = 2,
}
