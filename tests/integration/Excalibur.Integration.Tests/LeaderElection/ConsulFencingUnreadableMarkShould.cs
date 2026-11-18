// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;
using System.Text;

using Consul;

using Excalibur.Dispatch.LeaderElection.Fencing;
using Excalibur.LeaderElection.Consul;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Integration.Tests.LeaderElection;

/// <summary>
/// Fail-closed regression lock for w5ar9g: a Consul fencing counter that exists but cannot be read must
/// refuse, never resolve to a number.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this binds.</b> <c>ParseValue</c> answered <c>0</c> for a present-but-unparseable or
/// present-but-empty payload. The mint then computed <c>next = 1</c> and CASed from the
/// <c>ModifyIndex</c> it had just read — a correct index, so the write <i>succeeded</i> — moving the
/// high-water mark backward. Afterwards every token any prior leader held satisfied the
/// <c>token &gt;= current</c> check, so two leaders could both write and each admission was logged as a
/// success. No concurrency is required to reach it.
/// </para>
/// <para>
/// <b>Why this runs on real Consul.</b> The defect is what the real client returns for a non-numeric or
/// valueless KV entry, and the load-bearing assertion is that the <i>stored</i> mark survives the refusal
/// — a faked <c>IConsulClient</c> can only report which calls were made, which is the registration-presence
/// shape this repository treats as mock-grade. Docker is a hard requirement here; a skipped arm is the gap
/// that shipped the fail-open.
/// </para>
/// <para>
/// <b>RED-on-mutant.</b> Restore <c>? parsed : 0L</c> in <c>ParseValue</c> and
/// <see cref="Mint_LeavesTheHighMarkIntact_WhenTheMarkIsUnparseable"/> goes RED on the mark assertion — the
/// requirement itself, not the refusal that enforces it. "It threw" and "it did not corrupt the mark" are
/// different claims and only the second is the defect, so that arm swallows the refusal and reads the
/// stored bytes back.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "LeaderElection")]
[Trait("Database", "Consul")]
[Collection(ConsulLeaderElectionTestCollection.CollectionName)]
public sealed class ConsulFencingUnreadableMarkShould
{
	private const string NeverSkip =
		"w5ar9g is a live fail-open on a published package -- this real-Consul lock must never be skipped";

	private readonly ConsulContainerFixture _fixture;

	public ConsulFencingUnreadableMarkShould(ConsulContainerFixture fixture) => _fixture = fixture;

	[Fact]
	public async Task GetToken_Fails_WhenTheMarkIsUnparseable()
	{
		await using var world = await FenceWithMarkAsync("not-a-number");

		// NOT 0, and not null: a key that exists but cannot be read is neither a value nor UNSET.
		_ = await Should.ThrowAsync<InvalidDataException>(
			async () => await world.Provider.GetTokenAsync(world.ResourceId, CancellationToken.None));
	}

	[Fact]
	public async Task GetToken_Fails_WhenTheMarkIsPresentButEmpty()
	{
		// Consul renders a valueless key as a present pair with a null payload. Present-but-empty must
		// behave exactly as unparseable does: a predecessor may exist and its mark is unreadable.
		await using var world = await FenceWithMarkAsync(string.Empty);

		_ = await Should.ThrowAsync<InvalidDataException>(
			async () => await world.Provider.GetTokenAsync(world.ResourceId, CancellationToken.None));
	}

	[Fact]
	public async Task Mint_Refuses_WhenTheMarkIsUnparseable()
	{
		await using var world = await FenceWithMarkAsync("not-a-number");

		_ = await Should.ThrowAsync<InvalidDataException>(
			async () => await world.Provider.IssueTokenAsync(world.ResourceId, CancellationToken.None));
	}

	[Fact]
	public async Task Mint_LeavesTheHighMarkIntact_WhenTheMarkIsUnparseable()
	{
		const string planted = "not-a-number";
		await using var world = await FenceWithMarkAsync(planted);

		try
		{
			_ = await world.Provider.IssueTokenAsync(world.ResourceId, CancellationToken.None);
		}
		catch (InvalidDataException)
		{
			// The refusal is asserted by its own arm. What matters here is what the mint LEFT BEHIND.
		}

		// The mint must not have written. With the fabricated 0 restored, the CAS succeeds and this reads
		// "1" -- the reset, observed directly at the bytes rather than inferred from a call that was made.
		(await world.ReadRawMarkAsync()).ShouldBe(planted,
			"a mint onto an unreadable predecessor must not write: that write IS the backward move of the fence");
	}

	[Fact]
	public async Task Validate_DoesNotAdmit_APriorLeadersToken_WhenTheMarkIsUnreadable()
	{
		await using var world = await FenceWithMarkAsync("not-a-number");

		// The token a prior leader legitimately held. Under the fabricated 0 the fence resets to 1 and this
		// validates forever; validation must refuse instead of admitting it.
		_ = await Should.ThrowAsync<InvalidDataException>(
			async () => await world.Provider.ValidateTokenAsync(world.ResourceId, world.HighMark, CancellationToken.None));
	}

	[Fact]
	public async Task Mint_ResumesAboveTheOriginalMark_OnceTheMarkIsRepaired()
	{
		// Liveness. Every arm above asserts a refusal, and a provider that refused everything would satisfy
		// all of them while being completely inert. Repair the payload and the fence must advance again --
		// from the ORIGINAL high mark, never from zero.
		await using var world = await FenceWithMarkAsync("not-a-number");

		await world.PlantMarkAsync(world.HighMark.ToString(CultureInfo.InvariantCulture));

		var resumed = await world.Provider.IssueTokenAsync(world.ResourceId, CancellationToken.None);
		resumed.ShouldBe(world.HighMark + 1,
			"once the mark is readable again the fence advances from it, so no token is ever reissued");
	}

	/// <summary>
	/// Mints two real tokens against a fresh per-test key, then overwrites the counter with
	/// <paramref name="mark"/> — the operator write, snapshot restore or truncated write that a consumer's
	/// own KV namespace can acquire (the prefix is theirs; see <c>ConsulLeaderElectionOptions.KeyPrefix</c>).
	/// </summary>
	private async Task<FenceWorld> FenceWithMarkAsync(string mark)
	{
		_fixture.DockerAvailable.ShouldBeTrue(NeverSkip);

		var world = new FenceWorld(_fixture.ConsulAddress);

		// A real predecessor, so the planted payload overwrites an established high-water mark rather than
		// creating the key. "A predecessor may exist" is the whole difference between unreadable and absent.
		_ = await world.Provider.IssueTokenAsync(world.ResourceId, CancellationToken.None);
		world.HighMark = await world.Provider.IssueTokenAsync(world.ResourceId, CancellationToken.None);

		await world.PlantMarkAsync(mark);
		return world;
	}

	private sealed class FenceWorld : IAsyncDisposable
	{
		private readonly ConsulClient _raw;
		private readonly ServiceProvider _serviceProvider;
		private readonly string _keyPrefix = "fence-" + Guid.NewGuid().ToString("N");

		public FenceWorld(string consulAddress)
		{
			_raw = new ConsulClient(config => config.Address = new Uri(consulAddress));

			var services = new ServiceCollection();
			_ = services.AddSingleton<IConsulClient>(_ => new ConsulClient(config => config.Address = new Uri(consulAddress)));
			_ = services.Configure<ConsulLeaderElectionOptions>(o => o.KeyPrefix = _keyPrefix);
			_ = services.AddConsulFencingTokenProvider();
			_serviceProvider = services.BuildServiceProvider();
			Provider = _serviceProvider.GetRequiredService<IFencingTokenIssuer>();
		}

		public IFencingTokenIssuer Provider { get; }

		public string ResourceId { get; } = "resource-" + Guid.NewGuid().ToString("N");

		/// <summary>Gets or sets the last token the provider legitimately issued before the mark was planted.</summary>
		public long HighMark { get; set; }

		/// <summary>The provider's own counter key, derived the same way the provider derives it.</summary>
		private string CounterKey => $"{_keyPrefix}/fencing/{ResourceId}";

		public async Task PlantMarkAsync(string mark) =>
			(await _raw.KV.Put(new KVPair(CounterKey) { Value = Encoding.UTF8.GetBytes(mark) }, CancellationToken.None))
				.Response.ShouldBeTrue("the planted payload must actually reach Consul or the arm proves nothing");

		/// <summary>
		/// Reads the stored counter as text, without going through the parse under test. Returns
		/// <see langword="null"/> if the key is gone and <see cref="string.Empty"/> if it holds no value, so
		/// a caller comparing against the planted text fails loudly on either.
		/// </summary>
		public async Task<string?> ReadRawMarkAsync()
		{
			var pair = (await _raw.KV.Get(CounterKey, CancellationToken.None)).Response;
			if (pair is null)
			{
				return null;
			}

			return pair.Value is null ? string.Empty : Encoding.UTF8.GetString(pair.Value);
		}

		public async ValueTask DisposeAsync()
		{
			await _serviceProvider.DisposeAsync();
			_raw.Dispose();
		}
	}
}
