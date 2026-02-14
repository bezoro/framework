using System;
using System.Linq;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

var fullRun = args.Any(static argument => string.Equals(argument, "--full", StringComparison.OrdinalIgnoreCase));
var reliableRun = args.Any(static argument => string.Equals(argument, "--reliable", StringComparison.OrdinalIgnoreCase));
var benchmarkArgs = args
	.Where(static argument =>
		!string.Equals(argument, "--full", StringComparison.OrdinalIgnoreCase) &&
		!string.Equals(argument, "--reliable", StringComparison.OrdinalIgnoreCase)
	)
	.ToArray();

IConfig config = fullRun
	? DefaultConfig.Instance
	: reliableRun
		? ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.MediumRun.WithId("ReliableRun"))
		: ManualConfig.Create(DefaultConfig.Instance).AddJob(Job.ShortRun.WithId("ShortRun"));

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(benchmarkArgs, config);
