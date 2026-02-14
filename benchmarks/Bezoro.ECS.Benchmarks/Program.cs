using System;
using System.Linq;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

var fullRun = args.Any(static argument => string.Equals(argument, "--full", StringComparison.OrdinalIgnoreCase));
var fastRun = args.Any(static argument => string.Equals(argument, "--fast", StringComparison.OrdinalIgnoreCase));
var benchmarkArgs = args
	.Where(static argument =>
		!string.Equals(argument, "--full", StringComparison.OrdinalIgnoreCase) &&
		!string.Equals(argument, "--reliable", StringComparison.OrdinalIgnoreCase) &&
		!string.Equals(argument, "--fast", StringComparison.OrdinalIgnoreCase)
	)
	.ToArray();

IConfig config = fullRun
	? DefaultConfig.Instance
	: fastRun
		? ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.ShortRun.WithId("FastRun"))
		: ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.MediumRun.WithId("ReliableRun"));

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(benchmarkArgs, config);
