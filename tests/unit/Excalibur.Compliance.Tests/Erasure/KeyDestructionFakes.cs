// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Compliance.Tests;

/// <summary>
/// Fakes for a key provider that CAN answer authoritatively whether a key is destroyed -- the only kind of
/// provider on which erasure may confirm key destruction. An unconfigured answer is <see langword="false"/>.
/// </summary>
internal static class KeyDestructionFakes
{
	public static IKeyManagementProvider ProviderThatReportsDestruction()
	{
		var provider = A.Fake<IKeyManagementProvider>(o => o.Implements<IKeyDestructionStatusProvider>());
		A.CallTo(() => provider.GetService(typeof(IKeyDestructionStatusProvider))).Returns(provider);
		return provider;
	}

	public static void ReportsDestroyed(this IKeyManagementProvider provider, string keyId, bool destroyed) =>
		A.CallTo(() => ((IKeyDestructionStatusProvider)provider).IsKeyDestroyedAsync(keyId, A<CancellationToken>._))
			.Returns(Task.FromResult(destroyed));

	/// <summary>
	/// A key admin that destroys keys AND can say, afterwards, that they are destroyed — which is what the
	/// framework requires of a provider whose keys an erasure shreds.
	/// </summary>
	/// <remarks>
	/// A bare <c>A.Fake&lt;IKeyManagementAdmin&gt;()</c> destroys keys and cannot answer whether they stayed
	/// destroyed, so an erasure using one cannot re-establish the key state before it attests and is never
	/// recorded as complete. That is the correct outcome for such a provider and the wrong fixture for an arm
	/// about anything else: every shipped key provider implements this capability, and startup validation
	/// warns a deployment whose provider does not.
	/// </remarks>
	public static IKeyManagementAdmin AdminThatReportsEveryKeyDestroyed()
	{
		var admin = A.Fake<IKeyManagementAdmin>(o => o.Implements<IKeyDestructionStatusProvider>());
		A.CallTo(() => ((IKeyDestructionStatusProvider)admin)
				.IsKeyDestroyedAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
		return admin;
	}

	public static void ReportsEveryKeyDestroyed(this IKeyManagementProvider provider) =>
		A.CallTo(() => ((IKeyDestructionStatusProvider)provider).IsKeyDestroyedAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(true));
}
