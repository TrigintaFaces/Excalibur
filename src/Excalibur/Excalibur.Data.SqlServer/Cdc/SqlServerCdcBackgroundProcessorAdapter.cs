// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.Processing;

namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Adapts the SQL Server <see cref="IDataChangeEventProcessor"/> to the
/// provider-agnostic <see cref="ICdcBackgroundProcessor"/> contract.
/// </summary>
/// <remarks>
/// <para>
/// This adapter bridges the provider-specific <see cref="IDataChangeEventProcessor"/>
/// (which lives in <c>Excalibur.Data.SqlServer</c>) to the provider-agnostic
/// <see cref="ICdcBackgroundProcessor"/> interface (which lives in <c>Excalibur.Cdc</c>).
/// The <see cref="CdcProcessingHostedService"/> resolves <see cref="ICdcBackgroundProcessor"/>
/// from DI to drive the polling loop.
/// </para>
/// </remarks>
internal sealed class SqlServerCdcBackgroundProcessorAdapter : ICdcBackgroundProcessor
{
	private readonly IDataChangeEventProcessor _processor;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerCdcBackgroundProcessorAdapter"/> class.
	/// </summary>
	/// <param name="processor">The SQL Server data change event processor.</param>
	public SqlServerCdcBackgroundProcessorAdapter(IDataChangeEventProcessor processor)
	{
		_processor = processor ?? throw new ArgumentNullException(nameof(processor));
	}

	/// <inheritdoc/>
	public Task<int> ProcessChangesAsync(CancellationToken cancellationToken)
	{
		return _processor.ProcessCdcChangesAsync(cancellationToken);
	}
}
