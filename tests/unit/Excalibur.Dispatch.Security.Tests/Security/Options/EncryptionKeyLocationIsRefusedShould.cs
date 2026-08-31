// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Options;

/// <summary>
/// Message encryption protects its keys with the host's Data Protection key ring, and this package
/// attaches no external key provider to it. Naming a cloud key location therefore cannot change where
/// the keys live.
/// <para>
/// The failure being locked is silent and security-relevant: a host that names a vault or a KMS key
/// would start, encrypt messages, and keep the keys locally while its configuration read as though a
/// managed service held them. The refusal is what makes that visible, so these arms assert the host
/// does not start rather than that the value was forwarded somewhere.
/// </para>
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Security)]
public sealed class EncryptionKeyLocationIsRefusedShould
{
	[Fact]
	public void RefuseStartupWhenAnAzureKeyVaultUrlIsNamed()
	{
		var error = Should.Throw<OptionsValidationException>(
			() => Resolve(options => options.AzureKeyVaultUrl = new Uri("https://contoso.vault.azure.net/")));

		error.Message.ShouldContain(nameof(EncryptionOptions.AzureKeyVaultUrl));
		error.Message.ShouldContain("ProtectKeysWithAzureKeyVault");
	}

	[Fact]
	public void RefuseStartupWhenAnAwsKmsKeyArnIsNamed()
	{
		var error = Should.Throw<OptionsValidationException>(
			() => Resolve(options => options.AwsKmsKeyArn = "arn:aws:kms:us-east-1:111122223333:key/abcd"));

		error.Message.ShouldContain(nameof(EncryptionOptions.AwsKmsKeyArn));
		error.Message.ShouldContain("Data Protection key ring");
	}

	/// <summary>
	/// The discriminating arm. Everything else about the composition is identical; only the key-location
	/// options are absent. If this went red with the two above, the refusal would be unconditional and
	/// would prove nothing about the options.
	/// </summary>
	[Fact]
	public void StartWhenNoKeyLocationIsNamed()
	{
		var options = Resolve(_ => { });

		options.AzureKeyVaultUrl.ShouldBeNull();
		options.AwsKmsKeyArn.ShouldBeNull();
	}

	private static EncryptionOptions Resolve(Action<EncryptionOptions> configure)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMessageEncryption(configure);

		using var provider = services.BuildServiceProvider();
		return provider.GetRequiredService<IOptions<EncryptionOptions>>().Value;
	}
}
