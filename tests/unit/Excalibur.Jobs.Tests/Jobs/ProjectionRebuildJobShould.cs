// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;
using Excalibur.Jobs.Jobs;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Jobs.Tests.Jobs;

/// <summary>
/// Unit tests for <see cref="ProjectionRebuildJob"/>.
/// Verifies constructor guards, execute flow, missing processor handling, and error propagation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class ProjectionRebuildJobShould
{
	private readonly IServiceScopeFactory _fakeScopeFactory;
	private readonly IServiceScope _fakeScope;
	private readonly IServiceProvider _fakeScopeProvider;
	private readonly IMaterializedViewProcessor _fakeProcessor;
	private readonly ProjectionRebuildJob _sut;

	public ProjectionRebuildJobShould()
	{
		_fakeScopeFactory = A.Fake<IServiceScopeFactory>();
		_fakeScope = A.Fake<IServiceScope>();
		_fakeScopeProvider = A.Fake<IServiceProvider>();
		_fakeProcessor = A.Fake<IMaterializedViewProcessor>();

		A.CallTo(() => _fakeScopeFactory.CreateScope()).Returns(_fakeScope);
		A.CallTo(() => _fakeScope.ServiceProvider).Returns(_fakeScopeProvider);
		A.CallTo(() => _fakeScopeProvider.GetService(typeof(IMaterializedViewProcessor)))
			.Returns(_fakeProcessor);

		_sut = new ProjectionRebuildJob(_fakeScopeFactory, NullLogger<ProjectionRebuildJob>.Instance);
	}

	// --- Constructor null guards ---

	[Fact]
	public void ThrowWhenScopeFactoryIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ProjectionRebuildJob(null!, NullLogger<ProjectionRebuildJob>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ProjectionRebuildJob(_fakeScopeFactory, null!));
	}

	// --- ExecuteAsync ---

	[Fact]
	public async Task ExecuteCallRebuildAsync()
	{
		// Act
		await _sut.ExecuteAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => _fakeProcessor.RebuildAsync(A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReturnEarly_WhenProcessorIsNull()
	{
		// Arrange
		A.CallTo(() => _fakeScopeProvider.GetService(typeof(IMaterializedViewProcessor)))
			.Returns(null);

		// Act -- should not throw
		await Should.NotThrowAsync(() => _sut.ExecuteAsync(CancellationToken.None));

		// Assert
		A.CallTo(() => _fakeProcessor.RebuildAsync(A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task RethrowException_WhenRebuildFails()
	{
		// Arrange
		var exception = new InvalidOperationException("Rebuild failed");
		A.CallTo(() => _fakeProcessor.RebuildAsync(A<CancellationToken>._))
			.ThrowsAsync(exception);

		// Act & Assert
		var thrown = await Should.ThrowAsync<InvalidOperationException>(() =>
			_sut.ExecuteAsync(CancellationToken.None));
		thrown.ShouldBeSameAs(exception);
	}

	[Fact]
	public async Task PassCancellationTokenToRebuild()
	{
		// Arrange
		using var cts = new CancellationTokenSource();

		// Act
		await _sut.ExecuteAsync(cts.Token);

		// Assert
		A.CallTo(() => _fakeProcessor.RebuildAsync(cts.Token))
			.MustHaveHappenedOnceExactly();
	}
}
