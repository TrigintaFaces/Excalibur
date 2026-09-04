// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Tests.Shared.Infrastructure;

/// <summary>
/// Container images the test suites provision, named once.
/// </summary>
/// <remarks>
/// A Testcontainers module ships a DEFAULT image tag, and taking that default silently delegates the
/// choice to the module's release cadence. That is how six conformance tests and four integration
/// suites stopped being able to start: the Pub/Sub module defaults to
/// <c>google-cloud-cli:446.0.1-emulators</c>, Google withdrew that tag, and pulling it now fails with
/// "manifest unknown". It kept passing on machines that already had the image cached, so the failure
/// only ever appeared on a clean runner -- the worst place to learn it.
/// <para>
/// Naming the image here makes it one decision instead of fourteen. A tag that goes away breaks in a
/// single place, and the fix is a single edit rather than a hunt for the call sites that took a
/// default and the ones that copied a literal.
/// </para>
/// </remarks>
public static class TestContainerImages
{
	/// <summary>
	/// Google Cloud SDK emulators image, backing the Pub/Sub and Firestore fixtures.
	/// </summary>
	/// <remarks>
	/// The rolling <c>:emulators</c> tag rather than a version-pinned one, deliberately: the pinned
	/// tag is what was withdrawn. A rolling tag can change under us, which is the trade -- but it
	/// cannot disappear, and a suite that cannot start reports nothing useful about the code.
	/// </remarks>
	public const string GoogleCloudEmulators = "gcr.io/google.com/cloudsdktool/google-cloud-cli:emulators";
}
