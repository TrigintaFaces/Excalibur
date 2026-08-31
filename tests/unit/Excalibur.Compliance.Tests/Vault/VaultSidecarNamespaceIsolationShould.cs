// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.Vault;

namespace Excalibur.Compliance.Tests.Vault;

/// <summary>
/// Structural lock on the two KV namespaces the Vault key provider addresses by concatenating a key
/// identifier: the suspension marker at <c>{Path}/{keyId}</c> and the purpose sidecar at
/// <c>{PurposePath}/{keyId}</c>.
/// </summary>
/// <remarks>
/// <para>
/// Key status is resolved by "a document exists at the suspension path". The purpose sidecar previously
/// lived at <c>{Path}/purpose/{keyId}</c> -- inside the suspension namespace -- so a key identifier of
/// <c>purpose/foo</c> composed to exactly the purpose sidecar path of the key <c>foo</c>, and recording a
/// purpose would have made that key read as suspended.
/// </para>
/// <para>
/// <b>That form is not reachable through this provider</b>, and the honest reason is worth recording rather
/// than implying: Vault Transit refuses a key name containing a path separator (<c>404 unsupported
/// path</c>, measured against Vault 1.15), so a key named <c>purpose/foo</c> cannot be created, read, or
/// suspended in the first place. The collision was prevented by an external system's naming rule, not by
/// anything this code did.
/// </para>
/// <para>
/// That is why this lock is structural rather than behavioural. Relying on a remote server's charset
/// validation to keep two of our own namespaces apart is a dependency nobody wrote down and no test would
/// have caught changing. Disjoint roots make the collision unrepresentable here, in code we own. This
/// assertion is RED against the previous nested layout, where the purpose root was a child of the
/// suspension root by construction.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class VaultSidecarNamespaceIsolationShould
{
	[Fact]
	public void KeepTheSuspensionAndPurposeRootsDisjointByDefault()
	{
		var suspension = new VaultOptions().Suspension;

		var suspensionRoot = suspension.Path;
		var purposeRoot = suspension.PurposePath;

		suspensionRoot.ShouldNotBeNullOrWhiteSpace();
		purposeRoot.ShouldNotBeNullOrWhiteSpace();
		purposeRoot.ShouldNotBe(suspensionRoot, "one root addressing the other is the collision itself.");

		// Neither may be a path-prefix of the other. Prefix containment is what lets a key identifier
		// carrying a separator walk from one namespace into the other; equality alone is too weak a check.
		IsPathPrefixOf(suspensionRoot, purposeRoot).ShouldBeFalse(
			$"the purpose root '{purposeRoot}' must not sit beneath the suspension root '{suspensionRoot}': "
			+ "a key identifier containing a separator would then address another key's purpose sidecar.");

		IsPathPrefixOf(purposeRoot, suspensionRoot).ShouldBeFalse(
			$"the suspension root '{suspensionRoot}' must not sit beneath the purpose root '{purposeRoot}': "
			+ "the same collision, reached from the other side.");
	}

    /// <summary>
    /// Liveness. The roots must remain configurable -- a lock that only proved they were different would be
    /// satisfied by hard-coding them, which would take the deployment-specific path away from consumers.
    /// </summary>
	[Fact]
	public void KeepBothRootsConfigurable()
	{
		var suspension = new VaultOptions().Suspension;

		suspension.Path = "tenant-a/suspended";
		suspension.PurposePath = "tenant-a/purpose";

		suspension.Path.ShouldBe("tenant-a/suspended");
		suspension.PurposePath.ShouldBe("tenant-a/purpose");
	}

	// Treats the roots as path segments: "a/b" is a prefix of "a/b/c" but NOT of "a/bc".
	private static bool IsPathPrefixOf(string candidatePrefix, string path) =>
		path.StartsWith(candidatePrefix + "/", StringComparison.Ordinal);
}
