// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Compliance.SqlServer;

/// <summary>
/// Configuration options for the SQL Server key escrow service.
/// </summary>
public sealed class SqlServerKeyEscrowOptions
{
	/// <summary>
	/// Gets or sets the SQL Server connection string.
	/// </summary>
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the schema for the escrow tables. Defaults to "compliance".
	/// </summary>
	public string Schema { get; set; } = "compliance";

	/// <summary>
	/// Gets or sets the name of the key escrow table. Defaults to "KeyEscrow".
	/// </summary>
	public string TableName { get; set; } = "KeyEscrow";

	/// <summary>
	/// Gets or sets the name of the recovery tokens table. Defaults to "RecoveryTokens".
	/// </summary>
	public string TokensTableName { get; set; } = "RecoveryTokens";

	/// <summary>
	/// Gets the fully qualified table name for the key escrow table.
	/// </summary>
	public string FullyQualifiedTableName => $"[{Schema}].[{TableName}]";

	/// <summary>
	/// Gets the fully qualified table name for the recovery tokens table.
	/// </summary>
	public string FullyQualifiedTokensTableName => $"[{Schema}].[{TokensTableName}]";

	/// <summary>
	/// Gets or sets the command timeout in seconds. Defaults to 30.
	/// </summary>
	public int CommandTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets whether to automatically expire old tokens during operations.
	/// Defaults to true.
	/// </summary>
	public bool AutoExpireTokens { get; set; } = true;

	/// <summary>
	/// Gets or sets the default token expiration time. Defaults to 24 hours.
	/// </summary>
	public TimeSpan DefaultTokenExpiration { get; set; } = TimeSpan.FromHours(24);

	/// <summary>
	/// Gets or sets the default number of custodians for Shamir's Secret Sharing.
	/// Defaults to 5.
	/// </summary>
	public int DefaultCustodianCount { get; set; } = 5;

	/// <summary>
	/// Gets or sets the default threshold for Shamir's Secret Sharing.
	/// Defaults to 3.
	/// </summary>
	public int DefaultThreshold { get; set; } = 3;
}
