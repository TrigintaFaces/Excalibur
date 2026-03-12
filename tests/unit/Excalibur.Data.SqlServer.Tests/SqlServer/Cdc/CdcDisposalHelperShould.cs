// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.SqlServer;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Unit tests for <see cref="CdcDisposalHelper"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
[Trait("Feature", "CDC")]
public sealed class CdcDisposalHelperShould : UnitTestBase
{
	[Fact]
	public async Task SafeDisposeAsync_DisposesAsyncDisposable()
	{
		var resource = A.Fake<IAsyncDisposable>();

		await CdcDisposalHelper.SafeDisposeAsync(resource);

		A.CallTo(() => resource.DisposeAsync()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SafeDisposeAsync_DisposesDisposable_WhenNotAsyncDisposable()
	{
		var resource = A.Fake<IDisposable>();

		await CdcDisposalHelper.SafeDisposeAsync(resource);

		A.CallTo(() => resource.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SafeDisposeAsync_PrefersAsyncDisposable_WhenBothImplemented()
	{
		var resource = new DualDisposable();

		await CdcDisposalHelper.SafeDisposeAsync(resource);

		resource.AsyncDisposed.ShouldBeTrue();
		resource.SyncDisposed.ShouldBeFalse();
	}

	[Fact]
	public async Task SafeDisposeAsync_DoesNothing_WhenResourceIsNotDisposable()
	{
		var resource = new object();

		// Should not throw
		await CdcDisposalHelper.SafeDisposeAsync(resource);
	}

	private sealed class DualDisposable : IAsyncDisposable, IDisposable
	{
		public bool AsyncDisposed { get; private set; }
		public bool SyncDisposed { get; private set; }

		public ValueTask DisposeAsync()
		{
			AsyncDisposed = true;
			return ValueTask.CompletedTask;
		}

		public void Dispose()
		{
			SyncDisposed = true;
		}
	}
}
