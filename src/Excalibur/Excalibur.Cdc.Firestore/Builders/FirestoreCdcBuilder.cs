// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Google.Cloud.Firestore;

namespace Excalibur.Cdc.Firestore;

/// <summary>
/// Internal implementation of the Firestore CDC builder.
/// </summary>
internal sealed class FirestoreCdcBuilder : IFirestoreCdcBuilder
{
	private readonly FirestoreCdcOptions _options;

	internal FirestoreCdcBuilder(FirestoreCdcOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <summary>Gets the state project ID if set via <see cref="WithStateStore(string)"/>.</summary>
	internal string? StateProjectId { get; private set; }

	/// <summary>Gets the state db factory if set via <see cref="WithStateStore(Func{IServiceProvider, FirestoreDb})"/>.</summary>
	internal Func<IServiceProvider, FirestoreDb>? StateDbFactory { get; private set; }

	/// <summary>Gets the state store configure callback.</summary>
	internal Action<ICdcStateStoreBuilder>? StateStoreConfigure { get; private set; }

	/// <summary>Gets the source BindConfiguration section path.</summary>
	internal string? SourceBindConfigurationPath { get; private set; }

	/// <inheritdoc/>
	public IFirestoreCdcBuilder CollectionPath(string collectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(collectionPath);
		_options.CollectionPath = collectionPath;
		return this;
	}

	/// <inheritdoc/>
	public IFirestoreCdcBuilder ProcessorName(string processorName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(processorName);
		_options.ProcessorName = processorName;
		return this;
	}

	/// <inheritdoc/>
	public IFirestoreCdcBuilder MaxBatchSize(int maxBatchSize)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBatchSize);
		_options.MaxBatchSize = maxBatchSize;
		return this;
	}

	/// <inheritdoc/>
	public IFirestoreCdcBuilder PollInterval(TimeSpan interval)
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
		_options.PollInterval = interval;
		return this;
	}

	/// <inheritdoc/>
	public IFirestoreCdcBuilder WithStateStore(string projectId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
		StateProjectId = projectId;
		return this;
	}

	/// <inheritdoc/>
	public IFirestoreCdcBuilder WithStateStore(string projectId, Action<ICdcStateStoreBuilder> configure)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
		ArgumentNullException.ThrowIfNull(configure);
		StateProjectId = projectId;
		StateStoreConfigure = configure;
		return this;
	}

	/// <inheritdoc/>
	public IFirestoreCdcBuilder WithStateStore(Func<IServiceProvider, FirestoreDb> dbFactory)
	{
		ArgumentNullException.ThrowIfNull(dbFactory);
		StateDbFactory = dbFactory;
		return this;
	}

	/// <inheritdoc/>
	public IFirestoreCdcBuilder WithStateStore(
		Func<IServiceProvider, FirestoreDb> dbFactory,
		Action<ICdcStateStoreBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(dbFactory);
		ArgumentNullException.ThrowIfNull(configure);
		StateDbFactory = dbFactory;
		StateStoreConfigure = configure;
		return this;
	}

	/// <inheritdoc/>
	public IFirestoreCdcBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
		SourceBindConfigurationPath = sectionPath;
		return this;
	}
}
