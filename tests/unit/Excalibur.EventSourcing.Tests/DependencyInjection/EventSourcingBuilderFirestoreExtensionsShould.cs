// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Cryptography;
using System.Text.Json;

using Excalibur.EventSourcing.Firestore;
using Excalibur.EventSourcing.DependencyInjection;

using Google.Cloud.Firestore;

namespace Excalibur.EventSourcing.Tests.DependencyInjection;

/// <summary>
/// Unit tests for <see cref="EventSourcingBuilderFirestoreExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventSourcingBuilderFirestoreExtensionsShould
{
	private static readonly string LineBreak = new string((char)10, 1);

	private static IEventSourcingBuilder CreateBuilder(ServiceCollection? services = null)
	{
		var svc = services ?? new ServiceCollection();
		return new ExcaliburEventSourcingBuilder(svc);
	}

	#region UseFirestore(Action<IFirestoreEventSourcingBuilder>) Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForConfigureOverload()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IEventSourcingBuilder)null!).UseFirestore((Action<IFirestoreEventSourcingBuilder>)(_ => { })));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.UseFirestore((Action<IFirestoreEventSourcingBuilder>)null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_ConfigureOverload()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseFirestore(fs =>
			fs.ProjectId("test-project").CollectionName("events"));

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterEventStore_WhenCalledWithConfigureAction()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act
		builder.UseFirestore(fs =>
			fs.ProjectId("test-project").CollectionName("events"));

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IEventStore));
	}

	#endregion

	#region Credential Wiring Tests

	/// <summary>
	/// Builds a syntactically valid service-account credential document around a freshly generated,
	/// throwaway key pair. The key never leaves the test process and is never written to source.
	/// </summary>
	private static string CreateServiceAccountJson(string projectId)
	{
		using var rsa = RSA.Create(2048);
		var pem = string.Concat(
			"-----BEGIN " + "PRIVATE KEY-----" + LineBreak,
			Convert.ToBase64String(rsa.ExportPkcs8PrivateKey(), Base64FormattingOptions.InsertLineBreaks),
			LineBreak + "-----END " + "PRIVATE KEY-----" + LineBreak);

		return JsonSerializer.Serialize(new
		{
			type = "service_account",
			project_id = projectId,
			private_key_id = "test-key-id",
			private_key = pem,
			client_email = $"tester@{projectId}.iam.gserviceaccount.com",
			client_id = "000000000000000000000",
			token_uri = "https://oauth2.googleapis.com/token",
		});
	}

	/// <summary>
	/// Points application default credential discovery at a file that does not exist, so ambient
	/// discovery fails deterministically on any machine. Without this the assertions would silently
	/// depend on whether the host happens to have ambient credentials configured.
	/// </summary>
	private static string PoisonAmbientCredentials() =>
		Path.Combine(Path.GetTempPath(), $"excalibur-absent-adc-{Guid.NewGuid():N}.json");

	[Fact]
	public void ApplyConfiguredCredentialsJson_SoTheClientDoesNotFallBackToAmbientCredentials()
	{
		// Arrange -- ambient discovery is guaranteed to fail, so a client can only be built when the
		// explicitly configured service account actually reaches it.
		var previous = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
		Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", PoisonAmbientCredentials());
		try
		{
			var services = new ServiceCollection();
			var builder = CreateBuilder(services);

			builder.UseFirestore(fs => fs
				.ProjectId("explicit-credential-project")
				.CredentialsJson(CreateServiceAccountJson("explicit-credential-project")));

			using var provider = services.BuildServiceProvider();

			// Act
			var db = provider.GetRequiredService<FirestoreDb>();

			// Assert -- resolution succeeded despite unusable ambient credentials.
			db.ShouldNotBeNull();
			db.ProjectId.ShouldBe("explicit-credential-project");
		}
		finally
		{
			Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", previous);
		}
	}

	[Fact]
	public void ApplyConfiguredCredentialsPath_SoTheClientDoesNotFallBackToAmbientCredentials()
	{
		// Arrange
		var previous = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
		Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", PoisonAmbientCredentials());
		var credentialFile = Path.Combine(Path.GetTempPath(), $"excalibur-sa-{Guid.NewGuid():N}.json");
		File.WriteAllText(credentialFile, CreateServiceAccountJson("path-credential-project"));
		try
		{
			var services = new ServiceCollection();
			var builder = CreateBuilder(services);

			builder.UseFirestore(fs => fs
				.ProjectId("path-credential-project")
				.CredentialsPath(credentialFile));

			using var provider = services.BuildServiceProvider();

			// Act
			var db = provider.GetRequiredService<FirestoreDb>();

			// Assert
			db.ShouldNotBeNull();
			db.ProjectId.ShouldBe("path-credential-project");
		}
		finally
		{
			Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", previous);
			File.Delete(credentialFile);
		}
	}

	[Fact]
	public void FallBackToAmbientCredentials_WhenNoCredentialsAreConfigured()
	{
		// Arrange -- the sentinel path is what ambient discovery would read, so naming it in the
		// failure proves the client went to ambient discovery rather than to a configured identity.
		var previous = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
		var sentinel = PoisonAmbientCredentials();
		Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", sentinel);
		try
		{
			var services = new ServiceCollection();
			var builder = CreateBuilder(services);

			builder.UseFirestore(fs => fs.ProjectId("ambient-project").CollectionName("events"));

			using var provider = services.BuildServiceProvider();

			// Act
			var ex = Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<FirestoreDb>());

			// Assert
			ex.Message.ShouldContain(Path.GetFileName(sentinel));
		}
		finally
		{
			Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", previous);
		}
	}

	#endregion

	#region Fluent Chaining Tests

	[Fact]
	public void SupportFluentChaining_WithOtherBuilderMethods()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act -- verify chaining compiles and returns builder
		var result = builder
			.UseFirestore(fs =>
				fs.ProjectId("test-project").CollectionName("events"))
			.UseIntervalSnapshots(100);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion
}
