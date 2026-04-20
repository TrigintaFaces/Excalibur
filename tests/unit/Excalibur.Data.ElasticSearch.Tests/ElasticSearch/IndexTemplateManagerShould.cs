// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;
using Excalibur.Data.ElasticSearch.Internal;

using Excalibur.Data.ElasticSearch;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
///     Unit tests for <see cref="IndexTemplateManager" />.
/// </summary>
/// <remarks>
///     S799 bd-iqlx2p + F1 split: constructed via the paired internal
///     <see cref="IIndexTemplateStore"/> + <see cref="IComponentTemplateStore"/>
///     seams (ADR-142 §D7 ≤5-method cap). The SDK <c>ElasticsearchClient</c>
///     is no longer faked — the public ctor's null-guard is still exercised
///     with a null literal cast.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class IndexTemplateManagerShould
{
	private readonly IIndexTemplateStore _fakeIndexStore;
	private readonly IComponentTemplateStore _fakeComponentStore;
	private readonly ILogger<IndexTemplateManager> _fakeLogger;
	private readonly IndexTemplateManager _manager;

	public IndexTemplateManagerShould()
	{
		_fakeIndexStore = A.Fake<IIndexTemplateStore>();
		_fakeComponentStore = A.Fake<IComponentTemplateStore>();
		_fakeLogger = A.Fake<ILogger<IndexTemplateManager>>();
		_manager = new IndexTemplateManager(_fakeIndexStore, _fakeComponentStore, _fakeLogger);
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenClientIsNull() =>
		// Act & Assert
		// Exercises the public (ElasticsearchClient, ILogger) ctor null-guard
		// path without faking the SDK type — the null literal cast selects the
		// overload while the CreateIndexStore/CreateComponentStore helpers'
		// `ArgumentNullException.ThrowIfNull(client)` fires on construction.
		_ = Should.Throw<ArgumentNullException>(() => new IndexTemplateManager((ElasticsearchClient)null!, _fakeLogger));

	[Fact]
	public void ThrowArgumentNullExceptionWhenIndexStoreIsNull() =>
		// Act & Assert — internal test-seam ctor null-guard on the index-template seam.
		_ = Should.Throw<ArgumentNullException>(() =>
			new IndexTemplateManager((IIndexTemplateStore)null!, _fakeComponentStore, _fakeLogger));

	[Fact]
	public void ThrowArgumentNullExceptionWhenComponentStoreIsNull() =>
		// Act & Assert — internal test-seam ctor null-guard on the component-template seam.
		_ = Should.Throw<ArgumentNullException>(() =>
			new IndexTemplateManager(_fakeIndexStore, (IComponentTemplateStore)null!, _fakeLogger));

	[Fact]
	public void ThrowArgumentNullExceptionWhenLoggerIsNull() =>
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new IndexTemplateManager(_fakeIndexStore, _fakeComponentStore, null!));

	[Fact]
	public async Task ValidateTemplateAsyncReturnValidWhenTemplateIsValid()
	{
		// Arrange
		var template = new IndexTemplateConfiguration { IndexPatterns = ["logs-*", "metrics-*"], Priority = 100 };

		// Act
		var result = await _manager.ValidateTemplateAsync(template, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public async Task ValidateTemplateAsyncReturnInvalidWhenIndexPatternsEmpty()
	{
		// Arrange
		var template = new IndexTemplateConfiguration { IndexPatterns = [], Priority = 100 };

		// Act
		var result = await _manager.ValidateTemplateAsync(template, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain("Index patterns cannot be empty");
	}

	[Fact]
	public async Task ValidateTemplateAsyncReturnInvalidWhenPriorityIsNegative()
	{
		// Arrange
		var template = new IndexTemplateConfiguration { IndexPatterns = ["logs-*"], Priority = -1 };

		// Act
		var result = await _manager.ValidateTemplateAsync(template, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain("Priority must be non-negative");
	}

	[Fact]
	public async Task ValidateTemplateAsyncThrowArgumentNullExceptionWhenTemplateIsNull() =>
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() => _manager.ValidateTemplateAsync(null!, CancellationToken.None)).ConfigureAwait(false);
}
