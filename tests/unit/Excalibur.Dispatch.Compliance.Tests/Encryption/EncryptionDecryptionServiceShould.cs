using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class EncryptionDecryptionServiceShould
{
	private readonly IEncryptionProviderRegistry _registry = A.Fake<IEncryptionProviderRegistry>();
	private readonly EncryptionOptions _encryptionOptions = new()
	{
		Mode = EncryptionMode.Disabled
	};

	private readonly NullLogger<EncryptionDecryptionService> _logger = NullLogger<EncryptionDecryptionService>.Instance;

	[Fact]
	public async Task Pass_through_items_when_encryption_disabled()
	{
		_encryptionOptions.Mode = EncryptionMode.Disabled;
		var sut = CreateService();
		var source = ToAsyncEnumerable(new EncryptionTestEntity { Name = "test" });
		var options = new DecryptionOptions();

		var results = new List<EncryptionTestEntity>();
		await foreach (var item in sut.DecryptAllAsync(source, options, CancellationToken.None).ConfigureAwait(false))
		{
			results.Add(item);
		}

		results.ShouldHaveSingleItem();
		results[0].Name.ShouldBe("test");
	}

	[Fact]
	public async Task Return_entity_unchanged_when_encryption_disabled()
	{
		_encryptionOptions.Mode = EncryptionMode.Disabled;
		var sut = CreateService();
		var entity = new EncryptionTestEntity { Name = "test" };
		var options = new DecryptionOptions();

		var result = await sut.DecryptEntityAsync(entity, options, CancellationToken.None).ConfigureAwait(false);

		result.ShouldBe(entity);
		result.Name.ShouldBe("test");
	}

	[Fact]
	public async Task Throw_for_null_source_in_decrypt_all()
	{
		var sut = CreateService();

		await Should.ThrowAsync<ArgumentNullException>(async () =>
		{
			await foreach (var _ in sut.DecryptAllAsync<EncryptionTestEntity>(null!, new DecryptionOptions(), CancellationToken.None).ConfigureAwait(false))
			{
			}
		}).ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_for_null_options_in_decrypt_all()
	{
		var sut = CreateService();
		var source = ToAsyncEnumerable(new EncryptionTestEntity());

		await Should.ThrowAsync<ArgumentNullException>(async () =>
		{
			await foreach (var _ in sut.DecryptAllAsync(source, null!, CancellationToken.None).ConfigureAwait(false))
			{
			}
		}).ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_for_null_entity_in_decrypt_entity()
	{
		var sut = CreateService();

		await Should.ThrowAsync<ArgumentNullException>(
			() => sut.DecryptEntityAsync<EncryptionTestEntity>(null!, new DecryptionOptions(), CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task Throw_for_null_options_in_decrypt_entity()
	{
		var sut = CreateService();

		await Should.ThrowAsync<ArgumentNullException>(
			() => sut.DecryptEntityAsync(new EncryptionTestEntity(), null!, CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public void Throw_for_null_registry()
	{
		Should.Throw<ArgumentNullException>(() =>
			new EncryptionDecryptionService(
				null!,
				Microsoft.Extensions.Options.Options.Create(_encryptionOptions),
				_logger));
	}

	[Fact]
	public void Throw_for_null_options()
	{
		Should.Throw<ArgumentNullException>(() =>
			new EncryptionDecryptionService(
				_registry,
				null!,
				_logger));
	}

	[Fact]
	public void Throw_for_null_logger()
	{
		Should.Throw<ArgumentNullException>(() =>
			new EncryptionDecryptionService(
				_registry,
				Microsoft.Extensions.Options.Options.Create(_encryptionOptions),
				null!));
	}

	[Fact]
	public async Task Process_multiple_items_in_stream()
	{
		_encryptionOptions.Mode = EncryptionMode.Disabled;
		var sut = CreateService();
		var source = ToAsyncEnumerable(
			new EncryptionTestEntity { Name = "item1" },
			new EncryptionTestEntity { Name = "item2" },
			new EncryptionTestEntity { Name = "item3" });
		var options = new DecryptionOptions { BatchSize = 2 };

		var results = new List<EncryptionTestEntity>();
		await foreach (var item in sut.DecryptAllAsync(source, options, CancellationToken.None).ConfigureAwait(false))
		{
			results.Add(item);
		}

		results.Count.ShouldBe(3);
	}

	private EncryptionDecryptionService CreateService() =>
		new(
			_registry,
			Microsoft.Extensions.Options.Options.Create(_encryptionOptions),
			_logger);

	private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(params T[] items)
	{
		foreach (var item in items)
		{
			yield return item;
		}

		await Task.CompletedTask.ConfigureAwait(false);
	}
}

internal sealed class EncryptionTestEntity
{
	public string? Name { get; set; }
	public byte[]? EncryptedData { get; set; }
}
