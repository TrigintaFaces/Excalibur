// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;

using Excalibur.Migrate.Tool.Commands;

namespace Excalibur.Migrate.Tool;

/// <summary>
/// Entry point for the Excalibur Migration CLI tool.
/// </summary>
public static class Program
{
	/// <summary>
	/// Main entry point for the CLI tool.
	/// </summary>
	/// <param name="args">Command line arguments.</param>
	/// <returns>Exit code.</returns>
	public static async Task<int> Main(string[] args)
	{
		var rootCommand = new RootCommand("Excalibur database migration tool")
		{
			Name = "excalibur-migrate",
		};

		// Global options
		var providerOption = new Option<string>(
			aliases: ["--provider", "-p"],
			description: "Database provider (sqlserver, postgres)")
		{
			IsRequired = true,
		};
		providerOption.AddCompletions("sqlserver", "postgres");

		var connectionOption = new Option<string>(
			aliases: ["--connection", "-c"],
			description: "Database connection string")
		{
			IsRequired = true,
		};

		var assemblyOption = new Option<string?>(
			aliases: ["--assembly", "-a"],
			description: "Assembly containing migration scripts (optional, uses entry assembly by default)");

		var namespaceOption = new Option<string?>(
			aliases: ["--namespace", "-n"],
			description: "Namespace prefix for migration resources (optional)");

		var verboseOption = new Option<bool>(
			aliases: ["--verbose", "-v"],
			description: "Enable verbose output",
			getDefaultValue: () => false);

		// Add global options
		rootCommand.AddGlobalOption(providerOption);
		rootCommand.AddGlobalOption(connectionOption);
		rootCommand.AddGlobalOption(assemblyOption);
		rootCommand.AddGlobalOption(namespaceOption);
		rootCommand.AddGlobalOption(verboseOption);

		// Add subcommands
		rootCommand.AddCommand(CreateUpCommand(providerOption, connectionOption, assemblyOption, namespaceOption, verboseOption));
		rootCommand.AddCommand(CreateDownCommand(providerOption, connectionOption, assemblyOption, namespaceOption, verboseOption));
		rootCommand.AddCommand(CreateStatusCommand(providerOption, connectionOption, assemblyOption, namespaceOption, verboseOption));
		rootCommand.AddCommand(CreateScriptCommand(providerOption, connectionOption, assemblyOption, namespaceOption, verboseOption));

		var parser = new CommandLineBuilder(rootCommand)
			.UseDefaults()
			.UseExceptionHandler((ex, context) =>
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.Error.WriteLine($"Error: {ex.Message}");
				Console.ResetColor();

				if (context.ParseResult.GetValueForOption(verboseOption))
				{
					Console.Error.WriteLine(ex.StackTrace);
				}

				context.ExitCode = 1;
			})
			.Build();

		return await parser.InvokeAsync(args).ConfigureAwait(false);
	}

	private static Command CreateUpCommand(
		Option<string> providerOption,
		Option<string> connectionOption,
		Option<string?> assemblyOption,
		Option<string?> namespaceOption,
		Option<bool> verboseOption)
	{
		var command = new Command("up", "Apply all pending migrations");

		command.SetHandler(
			async (provider, connection, assembly, ns, verbose) =>
			{
				var handler = new UpCommandHandler();
				await handler.ExecuteAsync(provider, connection, assembly, ns, verbose).ConfigureAwait(false);
			},
			providerOption,
			connectionOption,
			assemblyOption,
			namespaceOption,
			verboseOption);

		return command;
	}

	private static Command CreateDownCommand(
		Option<string> providerOption,
		Option<string> connectionOption,
		Option<string?> assemblyOption,
		Option<string?> namespaceOption,
		Option<bool> verboseOption)
	{
		var toOption = new Option<string>(
			aliases: ["--to", "-t"],
			description: "Target migration ID to roll back to (this migration remains applied)")
		{
			IsRequired = true,
		};

		var command = new Command("down", "Roll back migrations to a specific version")
		{
			toOption,
		};

		command.SetHandler(
			async (provider, connection, assembly, ns, verbose, targetMigrationId) =>
			{
				var handler = new DownCommandHandler();
				await handler.ExecuteAsync(provider, connection, assembly, ns, verbose, targetMigrationId).ConfigureAwait(false);
			},
			providerOption,
			connectionOption,
			assemblyOption,
			namespaceOption,
			verboseOption,
			toOption);

		return command;
	}

	private static Command CreateStatusCommand(
		Option<string> providerOption,
		Option<string> connectionOption,
		Option<string?> assemblyOption,
		Option<string?> namespaceOption,
		Option<bool> verboseOption)
	{
		var command = new Command("status", "Show migration status (applied and pending)");

		command.SetHandler(
			async (provider, connection, assembly, ns, verbose) =>
			{
				var handler = new StatusCommandHandler();
				await handler.ExecuteAsync(provider, connection, assembly, ns, verbose).ConfigureAwait(false);
			},
			providerOption,
			connectionOption,
			assemblyOption,
			namespaceOption,
			verboseOption);

		return command;
	}

	private static Command CreateScriptCommand(
		Option<string> providerOption,
		Option<string> connectionOption,
		Option<string?> assemblyOption,
		Option<string?> namespaceOption,
		Option<bool> verboseOption)
	{
		var outputOption = new Option<string>(
			aliases: ["--output", "-o"],
			description: "Output file path for the migration script")
		{
			IsRequired = true,
		};

		var command = new Command("script", "Generate SQL script for pending migrations (without applying)")
		{
			outputOption,
		};

		command.SetHandler(
			async (provider, connection, assembly, ns, verbose, output) =>
			{
				var handler = new ScriptCommandHandler();
				await handler.ExecuteAsync(provider, connection, assembly, ns, verbose, output).ConfigureAwait(false);
			},
			providerOption,
			connectionOption,
			assemblyOption,
			namespaceOption,
			verboseOption,
			outputOption);

		return command;
	}
}
