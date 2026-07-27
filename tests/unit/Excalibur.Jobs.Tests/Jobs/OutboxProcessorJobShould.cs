// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Jobs.Jobs;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Jobs.Tests.Jobs;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class OutboxProcessorJobShould
{
	private readonly IServiceScopeFactory _fakeScopeFactory;
	private readonly IServiceScope _fakeScope;
	private readonly IServiceProvider _fakeServiceProvider;
	private readonly OutboxProcessorJob _sut;

	public OutboxProcessorJobShould()
	{
		_fakeScopeFactory = A.Fake<IServiceScopeFactory>();
		_fakeScope = A.Fake<IServiceScope>();
		_fakeServiceProvider = A.Fake<IServiceProvider>();

		A.CallTo(() => _fakeScopeFactory.CreateScope()).Returns(_fakeScope);
		A.CallTo(() => _fakeScope.ServiceProvider).Returns(_fakeServiceProvider);

		// No processing gate registered by default: the job dispatches unconditionally (fail-open),
		// exactly as it did before the optional leadership gate was added. Individual tests that exercise
		// the gate override this.
		A.CallTo(() => _fakeServiceProvider.GetService(typeof(IProcessingGate))).Returns(null);

		_sut = new OutboxProcessorJob(
			_fakeScopeFactory,
			NullLogger<OutboxProcessorJob>.Instance);
	}

	[Fact]
	public void ThrowOnNullScopeFactory()
	{
		Should.Throw<ArgumentNullException>(() =>
			new OutboxProcessorJob(null!, NullLogger<OutboxProcessorJob>.Instance));
	}

	[Fact]
	public void ThrowOnNullLogger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new OutboxProcessorJob(_fakeScopeFactory, null!));
	}

	[Fact]
	public async Task ReturnGracefullyWhenNoOutboxImplementation()
	{
		A.CallTo(() => _fakeServiceProvider.GetService(typeof(IOutboxDispatcher)))
			.Returns(null);

		// Should not throw
		await Should.NotThrowAsync(() =>
			_sut.ExecuteAsync(CancellationToken.None));
	}

	[Fact]
	public async Task CallRunOutboxDispatchWhenOutboxAvailable()
	{
		var fakeOutbox = A.Fake<IOutboxDispatcher>();
		A.CallTo(() => _fakeServiceProvider.GetService(typeof(IOutboxDispatcher)))
			.Returns(fakeOutbox);
		A.CallTo(() => fakeOutbox.RunOutboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.Returns(5);

		await _sut.ExecuteAsync(CancellationToken.None);

		A.CallTo(() => fakeOutbox.RunOutboxDispatchAsync(
			A<string>.That.StartsWith("job-"),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GenerateUniqueDispatcherId()
	{
		var fakeOutbox = A.Fake<IOutboxDispatcher>();
		var capturedIds = new List<string>();

		A.CallTo(() => _fakeServiceProvider.GetService(typeof(IOutboxDispatcher)))
			.Returns(fakeOutbox);
		A.CallTo(() => fakeOutbox.RunOutboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.Invokes((string id, CancellationToken _) => capturedIds.Add(id))
			.Returns(0);

		await _sut.ExecuteAsync(CancellationToken.None);
		await _sut.ExecuteAsync(CancellationToken.None);

		capturedIds.Count.ShouldBe(2);
		capturedIds[0].ShouldNotBe(capturedIds[1]);
	}

	[Fact]
	public async Task RethrowExceptionFromOutbox()
	{
		var fakeOutbox = A.Fake<IOutboxDispatcher>();
		A.CallTo(() => _fakeServiceProvider.GetService(typeof(IOutboxDispatcher)))
			.Returns(fakeOutbox);

		var exception = new InvalidOperationException("Outbox failure");
		A.CallTo(() => fakeOutbox.RunOutboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.ThrowsAsync(exception);

		var thrown = await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.ExecuteAsync(CancellationToken.None));

		thrown.ShouldBeSameAs(exception);
	}

	[Fact]
	public async Task CreateServiceScope()
	{
		A.CallTo(() => _fakeServiceProvider.GetService(typeof(IOutboxDispatcher)))
			.Returns(null);

		await _sut.ExecuteAsync(CancellationToken.None);

		A.CallTo(() => _fakeScopeFactory.CreateScope())
			.MustHaveHappenedOnceExactly();
	}

	// ---------------------------------------------------------------------------------------------
	// vttjcz — leader-processing-gate lock (safety ∧ liveness).
	//
	// OutboxProcessorJob consults an optional IProcessingGate before draining the outbox:
	//   gate is not null && !gate.ShouldProcess  =>  skip the dispatch cycle.
	// A wired-but-unenforced gate (or a flipped '!' / removed check) would let a non-leader instance
	// drain the outbox = split-brain double-dispatch. These three arms bind the full contract so the
	// violation is inexpressible behind a passing gate:
	//   (1) gate ABSENT            -> dispatch (fail-open backward-compat, single-instance)
	//   (2) gate present + false   -> SKIP     (SAFETY: a non-leader never drains)
	//   (3) gate present + true    -> dispatch (LIVENESS: the leader still drains — the arm that
	//                                 fails if the gate is inert and silently skips everyone)
	// ---------------------------------------------------------------------------------------------

	[Fact]
	public async Task DispatchWhenNoProcessingGateRegistered()
	{
		// Gate absent (default fixture returns null) => fail-open, dispatch unconditionally.
		var fakeOutbox = A.Fake<IOutboxDispatcher>();
		A.CallTo(() => _fakeServiceProvider.GetService(typeof(IOutboxDispatcher)))
			.Returns(fakeOutbox);
		A.CallTo(() => fakeOutbox.RunOutboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.Returns(0);

		await _sut.ExecuteAsync(CancellationToken.None);

		A.CallTo(() => fakeOutbox.RunOutboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SkipDispatchWhenGatePresentAndShouldProcessFalse()
	{
		// SAFETY: a registered gate that denies leadership must PREVENT the drain cycle.
		// RED if the '!gate.ShouldProcess' guard is removed or flipped (non-leader would drain).
		var fakeOutbox = A.Fake<IOutboxDispatcher>();
		A.CallTo(() => _fakeServiceProvider.GetService(typeof(IOutboxDispatcher)))
			.Returns(fakeOutbox);

		var fakeGate = A.Fake<IProcessingGate>();
		A.CallTo(() => fakeGate.ShouldProcess).Returns(false);
		A.CallTo(() => _fakeServiceProvider.GetService(typeof(IProcessingGate)))
			.Returns(fakeGate);

		await _sut.ExecuteAsync(CancellationToken.None);

		A.CallTo(() => fakeOutbox.RunOutboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task DispatchWhenGatePresentAndShouldProcessTrue()
	{
		// LIVENESS: a registered gate that grants leadership must still allow the drain cycle.
		// This is the arm an inert/always-skip gate fails — safety alone is satisfied by doing nothing.
		var fakeOutbox = A.Fake<IOutboxDispatcher>();
		A.CallTo(() => _fakeServiceProvider.GetService(typeof(IOutboxDispatcher)))
			.Returns(fakeOutbox);
		A.CallTo(() => fakeOutbox.RunOutboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.Returns(3);

		var fakeGate = A.Fake<IProcessingGate>();
		A.CallTo(() => fakeGate.ShouldProcess).Returns(true);
		A.CallTo(() => _fakeServiceProvider.GetService(typeof(IProcessingGate)))
			.Returns(fakeGate);

		await _sut.ExecuteAsync(CancellationToken.None);

		A.CallTo(() => fakeOutbox.RunOutboxDispatchAsync(
			A<string>.That.StartsWith("job-"),
			A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}
}
