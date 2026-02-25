// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using ComplianceEncryptionOptions = Excalibur.Dispatch.Compliance.EncryptionOptions;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Encryption;

/// <summary>
/// Unit tests for <see cref="EncryptionDecryptionService"/>.
/// Tests bulk decryption and export per AD-255-3.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
public sealed class EncryptionDecryptionServiceShould
{
	private readonly IEncryptionProviderRegistry _registry;
	private readonly IOptions<ComplianceEncryptionOptions> _options;
	private readonly EncryptionDecryptionService _sut;

	public EncryptionDecryptionServiceShould()
	{
		_registry = A.Fake<IEncryptionProviderRegistry>();
		_options = Microsoft.Extensions.Options.Options.Create(new ComplianceEncryptionOptions { Mode = EncryptionMode.EncryptAndDecrypt });
		_sut = new EncryptionDecryptionService(
			_registry,
			_options,
			NullLogger<EncryptionDecryptionService>.Instance);
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenRegistryIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() => new EncryptionDecryptionService(
			null!,
			_options,
			NullLogger<EncryptionDecryptionService>.Instance));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenOptionsIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() => new EncryptionDecryptionService(
			_registry,
			null!,
			NullLogger<EncryptionDecryptionService>.Instance));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() => new EncryptionDecryptionService(
			_registry,
			_options,
			null!));
	}

	#endregion

	#region DecryptAllAsync Tests

	[Fact]
	public async Task DecryptAllAsync_ThrowsArgumentNullException_WhenSourceIsNull()
	{
		// Arrange
		var options = new DecryptionOptions();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
		{
			await foreach (var _ in _sut.DecryptAllAsync<TestEntity>(null!, options, CancellationToken.None).ConfigureAwait(false))
			{
				// consume
			}
		}).ConfigureAwait(false);
	}

	[Fact]
	public async Task DecryptAllAsync_ThrowsArgumentNullException_WhenOptionsIsNull()
	{
		// Arrange
		var source = ToAsyncEnumerable(Array.Empty<TestEntity>());

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
		{
			await foreach (var _ in _sut.DecryptAllAsync(source, null!, CancellationToken.None).ConfigureAwait(false))
			{
				// consume
			}
		}).ConfigureAwait(false);
	}

	[Fact]
	public async Task DecryptAllAsync_ReturnsItemsUnchanged_WhenModeIsDisabled()
	{
		// Arrange
		var disabledOptions = Microsoft.Extensions.Options.Options.Create(new ComplianceEncryptionOptions { Mode = EncryptionMode.Disabled });
		var sut = new EncryptionDecryptionService(
			_registry, disabledOptions, NullLogger<EncryptionDecryptionService>.Instance);

		var entities = new[] { new TestEntity { Name = "test" } };
		var source = ToAsyncEnumerable(entities);
		var decOptions = new DecryptionOptions();

		// Act
		var results = new List<TestEntity>();
		await foreach (var item in sut.DecryptAllAsync(source, decOptions, CancellationToken.None).ConfigureAwait(false))
		{
			results.Add(item);
		}

		// Assert
		results.Count.ShouldBe(1);
		results[0].Name.ShouldBe("test");
	}

	#endregion

	#region DecryptEntityAsync Tests

	[Fact]
	public async Task DecryptEntityAsync_ThrowsArgumentNullException_WhenEntityIsNull()
	{
		// Arrange
		var options = new DecryptionOptions();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.DecryptEntityAsync<TestEntity>(null!, options, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task DecryptEntityAsync_ThrowsArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.DecryptEntityAsync(new TestEntity(), null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task DecryptEntityAsync_ReturnsEntityUnchanged_WhenModeIsDisabled()
	{
		// Arrange
		var disabledOptions = Microsoft.Extensions.Options.Options.Create(new ComplianceEncryptionOptions { Mode = EncryptionMode.Disabled });
		var sut = new EncryptionDecryptionService(
			_registry, disabledOptions, NullLogger<EncryptionDecryptionService>.Instance);

		var entity = new TestEntity { Name = "test-entity" };
		var options = new DecryptionOptions();

		// Act
		var result = await sut.DecryptEntityAsync(entity, options, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeSameAs(entity);
	}

	[Fact]
	public async Task DecryptEntityAsync_SkipsNullByteArrayProperties()
	{
		// Arrange - entity with null EncryptedField
		var entity = new TestEntityWithEncryptedField { Data = null };
		var options = new DecryptionOptions();

		// Act
		var result = await _sut.DecryptEntityAsync(entity, options, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Data.ShouldBeNull();
	}

	[Fact]
	public async Task DecryptEntityAsync_SkipsEmptyByteArrayProperties()
	{
		// Arrange
		var entity = new TestEntityWithEncryptedField { Data = Array.Empty<byte>() };
		var options = new DecryptionOptions();

		// Act
		var result = await _sut.DecryptEntityAsync(entity, options, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Data.ShouldBeEmpty();
	}

	#endregion

	#region ExportDecryptedAsync Tests

	[Fact]
	public async Task ExportDecryptedAsync_ThrowsArgumentNullException_WhenSourceIsNull()
	{
		// Arrange
		var options = new BulkDecryptionExportOptions
		{
			Destination = new MemoryStream(),
			Format = DecryptionExportFormat.Json
		};

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.ExportDecryptedAsync<TestEntity>(null!, options, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ExportDecryptedAsync_ThrowsArgumentNullException_WhenOptionsIsNull()
	{
		// Arrange
		var source = ToAsyncEnumerable(Array.Empty<TestEntity>());

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.ExportDecryptedAsync(source, null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ExportDecryptedAsync_ThrowsArgumentException_WhenFormatIsUnsupported()
	{
		// Arrange
		var source = ToAsyncEnumerable(Array.Empty<TestEntity>());
		var options = new BulkDecryptionExportOptions
		{
			Destination = new MemoryStream(),
			Format = (DecryptionExportFormat)999
		};

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.ExportDecryptedAsync(source, options, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ExportDecryptedAsync_ExportsJsonFormat_WhenModeIsDisabled()
	{
		// Arrange
		var disabledOptions = Microsoft.Extensions.Options.Options.Create(new ComplianceEncryptionOptions { Mode = EncryptionMode.Disabled });
		var sut = new EncryptionDecryptionService(
			_registry, disabledOptions, NullLogger<EncryptionDecryptionService>.Instance);

		var entities = new[] { new TestEntity { Name = "json-test" } };
		var source = ToAsyncEnumerable(entities);
		using var stream = new MemoryStream();
		var exportOptions = new BulkDecryptionExportOptions
		{
			Destination = stream,
			Format = DecryptionExportFormat.Json
		};

		// Act
		await sut.ExportDecryptedAsync(source, exportOptions, CancellationToken.None).ConfigureAwait(false);

		// Assert
		stream.Position = 0;
		using var reader = new StreamReader(stream);
		var content = await reader.ReadToEndAsync().ConfigureAwait(false);
		content.ShouldContain("json-test");
		content.ShouldStartWith("[");
	}

	[Fact]
	public async Task ExportDecryptedAsync_ExportsCsvFormat_WhenModeIsDisabled()
	{
		// Arrange
		var disabledOptions = Microsoft.Extensions.Options.Options.Create(new ComplianceEncryptionOptions { Mode = EncryptionMode.Disabled });
		var sut = new EncryptionDecryptionService(
			_registry, disabledOptions, NullLogger<EncryptionDecryptionService>.Instance);

		var entities = new[] { new TestEntity { Name = "csv-test" } };
		var source = ToAsyncEnumerable(entities);
		using var stream = new MemoryStream();
		var exportOptions = new BulkDecryptionExportOptions
		{
			Destination = stream,
			Format = DecryptionExportFormat.Csv
		};

		// Act
		await sut.ExportDecryptedAsync(source, exportOptions, CancellationToken.None).ConfigureAwait(false);

		// Assert
		stream.Position = 0;
		using var reader = new StreamReader(stream);
		var content = await reader.ReadToEndAsync().ConfigureAwait(false);
		content.ShouldContain("Name"); // header
		content.ShouldContain("csv-test");
	}

	[Fact]
	public async Task ExportDecryptedAsync_ExportsPlaintextFormat_WhenModeIsDisabled()
	{
		// Arrange
		var disabledOptions = Microsoft.Extensions.Options.Options.Create(new ComplianceEncryptionOptions { Mode = EncryptionMode.Disabled });
		var sut = new EncryptionDecryptionService(
			_registry, disabledOptions, NullLogger<EncryptionDecryptionService>.Instance);

		var entities = new[] { new TestEntity { Name = "plain-test" } };
		var source = ToAsyncEnumerable(entities);
		using var stream = new MemoryStream();
		var exportOptions = new BulkDecryptionExportOptions
		{
			Destination = stream,
			Format = DecryptionExportFormat.Plaintext
		};

		// Act
		await sut.ExportDecryptedAsync(source, exportOptions, CancellationToken.None).ConfigureAwait(false);

		// Assert
		stream.Position = 0;
		using var reader = new StreamReader(stream);
		var content = await reader.ReadToEndAsync().ConfigureAwait(false);
		content.ShouldContain("plain-test");
	}

	#endregion

	#region Helpers

	private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> items,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		foreach (var item in items)
		{
			cancellationToken.ThrowIfCancellationRequested();
			yield return item;
			await Task.CompletedTask.ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Test entity without encrypted fields.
	/// </summary>
	private sealed class TestEntity
	{
		public string Name { get; set; } = string.Empty;
	}

	/// <summary>
	/// Test entity with an encrypted field attribute.
	/// </summary>
	private sealed class TestEntityWithEncryptedField
	{
		[EncryptedField]
		public byte[]? Data { get; set; }
	}

	#endregion
}
