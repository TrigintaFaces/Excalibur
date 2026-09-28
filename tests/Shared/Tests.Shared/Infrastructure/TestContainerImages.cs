// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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

	/// <summary>
	/// SQL Server 2022, backing every relational integration and compliance fixture.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Was CU26, repeated as a literal in 29 files. It is CU27 for a measured reason.</b> The CU26
	/// image would not start on at least one development host: <c>sqlservr</c> refused with
	/// <c>"The file archive [/opt/mssql/lib/system.netfx.sfp] is invalid"</c> and exited 1, taking
	/// every SQL Server suite with it -- 68 failures in one shard, none of them code.
	/// </para>
	/// <para>
	/// <b>The diagnosis, because the obvious remedies do not work and the next person should not
	/// repeat them.</b> A prune, <c>rmi -f</c> and re-pull did NOT repair it: the pull reports success
	/// while reusing a content-addressed blob whose recorded digest still matches, so a corrupt layer
	/// survives the one remedy everyone tries first. Running as root does not help either -- the file
	/// is owned by root in CU26 and by mssql in CU14, which looks like the answer and is not.
	/// </para>
	/// <para>
	/// <b>What identified it was a bracketing comparison.</b> The file is 417,853,440 bytes in every
	/// tag examined, so it is not truncated. CU25 and CU27 -- freshly pulled, either side of CU26 --
	/// carry an IDENTICAL md5 (<c>c08a56dc…</c>). A payload unchanged across CU25 and CU27 did not
	/// change at CU26, so CU26 holding a different hash (<c>76a06bfd…</c>) is the anomaly, not a
	/// version difference. CU14 differs from all three (<c>94ed32d7…</c>), which is why one
	/// comparison is not enough and two brackets are.
	/// </para>
	/// <para>
	/// CU27 is verified starting on this host in about ten seconds with no error. It is also the
	/// newest of the three, so this is a forward move rather than a workaround. If CU26 turns out
	/// sound elsewhere, nothing here needs undoing.
	/// </para>
	/// </remarks>
	public const string SqlServer2022 = "mcr.microsoft.com/mssql/server:2022-CU27-ubuntu-22.04";
}
