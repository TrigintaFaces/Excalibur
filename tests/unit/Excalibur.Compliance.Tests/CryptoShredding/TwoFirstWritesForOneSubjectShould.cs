// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Encryption;
using Excalibur.Compliance.Erasure;
using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Compliance.Tests.CryptoShredding;

/// <summary>
/// Two ordinary concurrent first writes for one new data subject must leave that subject with one key,
/// every version of it usable, and both writes readable.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect these arms detect.</b> Minting a subject's key used to be a read followed by
/// <c>RotateKeyAsync</c> when the read said "absent" -- two steps with no atomicity, and a second step that
/// is create-OR-rotate. So the writer that arrived second found the key present by the time it wrote, took
/// the rotate branch, and retired the version the first writer had just created. Nobody requested a
/// rotation and no unusual configuration was involved: two concurrent first writes were the whole input.
/// </para>
/// <para>
/// <b>What the harm is, and what it is NOT.</b> It is tempting to assert that the demotion makes an
/// encryption throw, because every key status other than Active is refused on the encrypt path. It does
/// not, and an arm written that way cannot be made red: the field encryptor names no key VERSION, so the
/// encrypt path resolves the key by handle and gets the LATEST version, which after the racing rotation is
/// the new Active one. The reachable harm is that a subject the design gives one key acquires a version per
/// racing write, with the earlier ones fenced out of encryption -- and on a backend that rotates by minting
/// a whole new key, one such key apiece, each of which an erasure must then find and destroy.
/// </para>
/// <para>
/// <b>Bound to the real stack.</b> The real in-memory key provider, real AES-GCM, and the real
/// <c>SubjectKeyManager</c>/<c>FieldEncryptor</c> pair. A substitute here could not exhibit the race at all,
/// because the race lives in the provider's own rotate branch.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class TwoFirstWritesForOneSubjectShould
{
	private static readonly TenantId Tenant = new(TenantDefaults.DefaultTenantId);

	private const int Writers = 8;

	// A race is probabilistic, so ONE round is not a reliable detector: the defective sequence needs the
	// second writer's read to land before the first writer's insert and its write to land after it, and a
	// single round can miss that window and report green against a broken implementation. Measured while
	// building these arms -- one round against the defect failed one safety arm and passed the other, from the
	// same mutant. Repeating over fresh subjects makes detection reliable without making the arm
	// timing-dependent: a CORRECT implementation cannot demote in any round, so the green direction stays
	// deterministic and no round has a deadline.
	private const int Rounds = 25;

	private const string Subject = "a-subject-two-requests-arrive-for-at-once";

	/// <summary>
	/// SAFETY, and the RED arm. Concurrent first writes leave no version of the subject's key demoted.
	/// </summary>
	/// <remarks>
	/// RED input: restore the read-then-rotate sequence in <c>SubjectKeyManager.GetOrCreateKeyAsync</c>, or
	/// have the provider's create-if-absent operation fall through to its create-or-rotate path. Version 1 is
	/// then <c>DecryptOnly</c> and this arm fails.
	/// </remarks>
	[Fact]
	public async Task LeaveEveryVersionOfTheSubjectsKeyUsableForEncryption()
	{
		await using var host = BuildStack();
		await StartAsync(host).ConfigureAwait(true);

		var keys = host.GetRequiredService<ISubjectKeyManager>();
		var hasher = host.GetRequiredService<IDataSubjectHasher>();
		var provider = host.GetRequiredService<IKeyManagementProvider>();

		for (var round = 0; round < Rounds; round++)
		{
			var subject = $"{Subject}-{round}";

			var handles = await RaceAsync(
				async () => (await keys.GetOrCreateKeyAsync(Tenant, subject, default, TestContext.Current.CancellationToken)).KeyId)
				.ConfigureAwait(true);

			// Every writer must have been handed the SAME handle: one subject, one key.
			var keyId = SubjectKeyHandle.ForSubject(Tenant, subject, hasher).Value;
			handles.Distinct(StringComparer.Ordinal).ShouldHaveSingleItem(
				"concurrent first writes for one subject must all name one key handle.");
			handles[0].ShouldBe(keyId);

			// Guard: the key must really exist, or the assertions below hold vacuously.
			var current = await provider.GetKeyAsync(keyId, TestContext.Current.CancellationToken)
				.ConfigureAwait(true);
			current.ShouldNotBeNull("the subject's key must exist after a write.");

			var firstVersion = await provider.GetKeyVersionAsync(keyId, 1, TestContext.Current.CancellationToken)
				.ConfigureAwait(true);

			firstVersion.ShouldNotBeNull("version 1 must exist and must not have been replaced.");
			firstVersion.Status.ShouldBe(
				KeyStatus.Active,
				$"round {round}: a concurrent first write rotated the key another write had just created, fencing "
				+ "version 1 out of encryption although nobody asked for a rotation.");
		}
	}

	/// <summary>
	/// SAFETY. Concurrent first writes produce exactly one version, so a subject does not accumulate a key
	/// per racing request.
	/// </summary>
	/// <remarks>
	/// Separate from the arm above because the two failures are different: a demoted version breaks the
	/// status invariant, while an extra version is waste that an erasure then has to find. On a backend that
	/// rotates by minting a new key, each extra version is a whole key with its own material.
	/// </remarks>
	[Fact]
	public async Task ProduceExactlyOneVersionForOneSubject()
	{
		await using var host = BuildStack();
		await StartAsync(host).ConfigureAwait(true);

		var keys = host.GetRequiredService<ISubjectKeyManager>();
		var hasher = host.GetRequiredService<IDataSubjectHasher>();
		var provider = host.GetRequiredService<IKeyManagementProvider>();

		for (var round = 0; round < Rounds; round++)
		{
			var subject = $"count-{Subject}-{round}";

			_ = await RaceAsync(
				async () => (await keys.GetOrCreateKeyAsync(Tenant, subject, default, TestContext.Current.CancellationToken)).KeyId)
				.ConfigureAwait(true);

			var keyId = SubjectKeyHandle.ForSubject(Tenant, subject, hasher).Value;

			(await provider.GetKeyVersionAsync(keyId, 1, TestContext.Current.CancellationToken)
				.ConfigureAwait(true))
				.ShouldNotBeNull("version 1 must exist, or this arm proves nothing about the count.");

			(await provider.GetKeyVersionAsync(keyId, 2, TestContext.Current.CancellationToken)
				.ConfigureAwait(true))
				.ShouldBeNull(
					$"round {round}: {Writers} concurrent first writes produced more than one version of one "
					+ "subject's key. Each extra version is material an erasure must locate and destroy, created by "
					+ "write concurrency alone.");
		}
	}

	/// <summary>
	/// LIVENESS, and the arm that makes the two above mean something. Both concurrent writes round-trip.
	/// </summary>
	/// <remarks>
	/// Without this, a manager that provisioned nothing at all -- so no version could be demoted and no
	/// second version could exist -- would satisfy every safety assertion here.
	/// </remarks>
	[Fact]
	public async Task StillEncryptAndDecryptEveryConcurrentWrite()
	{
		await using var host = BuildStack();
		await StartAsync(host).ConfigureAwait(true);

		var encryptor = host.GetRequiredService<IFieldEncryptor>();

		var payloads = Enumerable.Range(0, Writers)
			.Select(static i => System.Text.Encoding.UTF8.GetBytes($"personal-data-{i}"))
			.ToArray();

		var envelopes = await Task.WhenAll(
			payloads.Select(payload => encryptor
				.EncryptAsync(Subject, default, payload, TestContext.Current.CancellationToken)
				.AsTask()))
			.ConfigureAwait(true);

		for (var i = 0; i < Writers; i++)
		{
			var plaintext = await encryptor
				.DecryptAsync(envelopes[i], TestContext.Current.CancellationToken)
				.ConfigureAwait(true);

			plaintext.ShouldBe(
				payloads[i],
				"every concurrent write for one subject must still be readable afterwards.");
		}
	}

	// Releases all writers at once, which is what makes the interleaving reachable. Starting them
	// sequentially would serialise the check-then-act and the race would never occur.
	private static async Task<string[]> RaceAsync(Func<Task<string>> write)
	{
		using var gate = new SemaphoreSlim(0, Writers);

		var racers = Enumerable.Range(0, Writers)
			.Select(_ => Task.Run(async () =>
			{
				await gate.WaitAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
				return await write().ConfigureAwait(false);
			}))
			.ToArray();

		_ = gate.Release(Writers);

		return await Task.WhenAll(racers).ConfigureAwait(true);
	}

	private static ServiceProvider BuildStack()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		_ = services.AddDataSubjectHashing();
		_ = services.Configure<DataSubjectHashingOptions>(options =>
			options.Pepper = "test-pepper-0123456789abcdef0123456789ab");

		_ = services.AddEncryption(builder => builder.UseInMemoryKeyManagement("test").SetAsPrimary("test"));
		_ = services.AddSingleton<IKeyManagementAdmin>(sp =>
			(IKeyManagementAdmin)sp.GetRequiredService<IKeyManagementProvider>());

		_ = services.AddCryptoShredding();

		// The read path takes a destruction ledger as a required collaborator, so the stack cannot be built
		// without one. These arms never destroy anything -- they are about two concurrent FIRST writes -- so the
		// ledger stays empty throughout and no field is ever reported erased. Registering it is what lets the
		// stack exist; an empty one is the correct state for arms that erase nothing.
		_ = services.AddInMemoryErasureStore();

		return services.BuildServiceProvider();
	}

	private static async Task StartAsync(IServiceProvider provider)
	{
		_ = provider.GetServices<IEncryptionProvider>().ToList();
		foreach (var hostedService in provider.GetServices<IHostedService>())
		{
			await hostedService.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
		}
	}
}
