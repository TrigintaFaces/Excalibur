// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.IbmMq;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.IbmMq;

/// <summary>
/// Unit tests for <see cref="IbmMqTransportSender"/>.
/// Validates constructor validation, destination exposure, and <c>GetService(Type)</c>.
/// </summary>
/// <remarks>
/// This class had no dedicated unit test file at all before this lock (bd-s1cprr) -- one of the three
/// transports named directly for thin failure-path coverage. The GetService arm pins the "hand out the
/// native capability" contract -- production already implements it correctly; without this lock a
/// regression to the base default would silently decline the <see cref="IIbmMqConnectionProvider"/>
/// capability.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class IbmMqTransportSenderShould : IAsyncDisposable
{
	private const string TestDestination = "ORDERS.QUEUE";
	private readonly IIbmMqConnectionProvider _fakeConnectionProvider;
	private readonly IbmMqTransportSender _sut;

	public IbmMqTransportSenderShould()
	{
		_fakeConnectionProvider = A.Fake<IIbmMqConnectionProvider>();
		_sut = new IbmMqTransportSender(
			_fakeConnectionProvider,
			TestDestination,
			NullLogger<IbmMqTransportSender>.Instance);
	}

	public ValueTask DisposeAsync() => _sut.DisposeAsync();

	[Fact]
	public void Expose_destination_from_constructor()
	{
		_sut.Destination.ShouldBe(TestDestination);
	}

	[Fact]
	public void Throw_when_connection_provider_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new IbmMqTransportSender(null!, TestDestination, NullLogger<IbmMqTransportSender>.Instance));
	}

	[Fact]
	public void Throw_when_destination_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new IbmMqTransportSender(A.Fake<IIbmMqConnectionProvider>(), null!, NullLogger<IbmMqTransportSender>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new IbmMqTransportSender(A.Fake<IIbmMqConnectionProvider>(), TestDestination, null!));
	}

	[Fact]
	public void Return_connection_provider_via_GetService()
	{
		var result = _sut.GetService(typeof(IIbmMqConnectionProvider));
		result.ShouldBe(_fakeConnectionProvider);
	}

	[Fact]
	public void Return_null_for_unknown_service_type()
	{
		var result = _sut.GetService(typeof(string));
		result.ShouldBeNull();
	}

	[Fact]
	public void Throw_when_GetService_type_is_null()
	{
		Should.Throw<ArgumentNullException>(() => _sut.GetService(null!));
	}

	/// <summary>
	/// SAFETY. A cancelled batch still reports one result per input, so the caller can tell which messages
	/// are unaccounted for.
	/// </summary>
	/// <remarks>
	/// This used to throw from inside the per-message loop, so a batch cancelled part way returned no
	/// result at all after having already sent some of its messages. The caller was told only "cancelled"
	/// and had no way to learn which inputs had been delivered — leaving resend-everything as the only
	/// safe move, which on an at-least-once transport duplicates every message that already arrived.
	/// </remarks>
	[Fact]
	public async Task Report_every_input_when_the_batch_is_cancelled()
	{
		using var cancelled = new CancellationTokenSource();
		await cancelled.CancelAsync();

		var messages = new List<TransportMessage>
		{
			TransportMessage.FromString("a"),
			TransportMessage.FromString("b"),
			TransportMessage.FromString("c"),
		};

		var result = await _sut.SendBatchAsync(messages, cancelled.Token);

		result.Results.Count.ShouldBe(3, "a cancelled batch must still account for every input.");
		result.TotalMessages.ShouldBe(3);
		(result.SuccessCount + result.FailureCount).ShouldBe(3);
		result.Results.ShouldAllBe(r => !r.IsSuccess && r.Error!.IsRetryable);

		// Nothing was put to the queue, which is what "cancelled before it was sent" has to mean.
		A.CallTo(() => _fakeConnectionProvider.CreateQueueManager()).MustNotHaveHappened();
	}

	/// <summary>
	/// LIVENESS. An uncancelled batch still sends every message and reports it.
	/// </summary>
	/// <remarks>
	/// Without this, the arm above is satisfied by a sender that reports every input as a cancelled
	/// failure and never publishes anything — safe, complete, and useless.
	/// </remarks>
	[Fact]
	public async Task Put_every_message_when_the_batch_is_not_cancelled()
	{
		var messages = new List<TransportMessage>
		{
			TransportMessage.FromString("a"),
			TransportMessage.FromString("b"),
		};

		var result = await _sut.SendBatchAsync(messages, CancellationToken.None);

		result.Results.Count.ShouldBe(2);
		A.CallTo(() => _fakeConnectionProvider.CreateQueueManager()).MustHaveHappenedTwiceExactly();
	}
}
