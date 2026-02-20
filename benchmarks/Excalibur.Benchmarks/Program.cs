// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project

using BenchmarkDotNet.Running;

namespace Excalibur.Benchmarks;

public class Program
{
	public static void Main(string[] args)
	{
		_ = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
	}
}
