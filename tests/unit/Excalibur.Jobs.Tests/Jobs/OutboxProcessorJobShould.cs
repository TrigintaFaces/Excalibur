// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Jobs.Jobs;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Jobs.Tests.Jobs;

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
}
