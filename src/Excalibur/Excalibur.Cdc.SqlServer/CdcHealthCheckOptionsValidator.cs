// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Options;

namespace Excalibur.Cdc.SqlServer;

/// <summary>Applies the shared CDC rules to the SQL Server options type consumed by its health check.</summary>
internal sealed class CdcHealthCheckOptionsValidator(
	IEnumerable<IValidateOptions<Excalibur.Data.CloudNative.CdcHealthCheckOptions>> validators)
	: IValidateOptions<CdcHealthCheckOptions>
{
	private readonly IEnumerable<IValidateOptions<Excalibur.Data.CloudNative.CdcHealthCheckOptions>> _validators =
		validators ?? throw new ArgumentNullException(nameof(validators));

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, CdcHealthCheckOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		var failures = new List<string>();
		var applicable = false;
		foreach (var validator in _validators)
		{
			var result = validator.Validate(name, options);
			if (result.Failed)
			{
				failures.AddRange(result.Failures);
			}
			applicable |= !result.Skipped;
		}
		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures)
			: applicable ? ValidateOptionsResult.Success : ValidateOptionsResult.Skip;
	}
}
