// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.SqlServer;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Unit tests for <see cref="SqlServerCdcBackgroundProcessorAdapter"/>.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class SqlServerCdcBackgroundProcessorAdapterShould : UnitTestBase
{
	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenProcessorIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new SqlServerCdcBackgroundProcessorAdapter(null!));
	}

	[Fact]
	public async Task ProcessChangesAsync_DelegatesToUnderlyingProcessor()
	{
		var processor = A.Fake<IDataChangeEventProcessor>();
		A.CallTo(() => processor.ProcessCdcChangesAsync(A<CancellationToken>._))
			.Returns(42);
		var adapter = new SqlServerCdcBackgroundProcessorAdapter(processor);
		using var cts = new CancellationTokenSource();

		var result = await adapter.ProcessChangesAsync(cts.Token);

		result.ShouldBe(42);
		A.CallTo(() => processor.ProcessCdcChangesAsync(cts.Token))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ProcessChangesAsync_ForwardsCancellationToken()
	{
		var processor = A.Fake<IDataChangeEventProcessor>();
		A.CallTo(() => processor.ProcessCdcChangesAsync(A<CancellationToken>._))
			.Returns(0);
		var adapter = new SqlServerCdcBackgroundProcessorAdapter(processor);
		using var cts = new CancellationTokenSource();
		var token = cts.Token;

		await adapter.ProcessChangesAsync(token);

		A.CallTo(() => processor.ProcessCdcChangesAsync(token))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ProcessChangesAsync_PropagatesExceptions()
	{
		var processor = A.Fake<IDataChangeEventProcessor>();
		A.CallTo(() => processor.ProcessCdcChangesAsync(A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("CDC failure"));
		var adapter = new SqlServerCdcBackgroundProcessorAdapter(processor);

		await Should.ThrowAsync<InvalidOperationException>(() =>
			adapter.ProcessChangesAsync(CancellationToken.None));
	}
}
