// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.IndexManagement;

using Excalibur.Data.ElasticSearch;

namespace Excalibur.Data.Tests.ElasticSearch.IndexManagement;

/// <summary>
///     Unit tests for <see cref="IndexTemplateManager" />.
/// </summary>
[Trait("Category", "Unit")]
public class IndexTemplateManagerShould
{
	private readonly ElasticsearchClient _fakeClient;
	private readonly ILogger<IndexTemplateManager> _fakeLogger;
	private readonly IndexTemplateManager _manager;

	public IndexTemplateManagerShould()
	{
		_fakeClient = A.Fake<ElasticsearchClient>();
		_fakeLogger = A.Fake<ILogger<IndexTemplateManager>>();
		_manager = new IndexTemplateManager(_fakeClient, _fakeLogger);
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenClientIsNull() =>
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new IndexTemplateManager(null!, _fakeLogger));

	[Fact]
	public void ThrowArgumentNullExceptionWhenLoggerIsNull() =>
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new IndexTemplateManager(_fakeClient, null!));

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
