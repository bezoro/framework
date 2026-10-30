# Bezoro.Persistence.Benchmarks

BenchmarkDotNet workloads for the persistence module. Each benchmark keeps setup outside the measured operation and reports managed allocations.

```sh
dotnet run -c Release --project benchmarks/Bezoro.Persistence.Benchmarks -- --filter '*'
```

Use representative capacities and workloads when interpreting results. Small operations can be dominated by dispatch or measurement overhead. Results on desktop .NET do not establish Unity Mono, IL2CPP, or Godot frame budgets.

## Recorded codec diagnostic

The [codec CSV](results/2026-10-29-save-envelope-short-run.csv) was collected on Debian 13, Intel Xeon Platinum 8573C, SDK 10.0.203 and .NET 10.0.7, with three warmups and three 100 ms measured iterations. The codec implementation corresponds to `4527267f`.

All nine workloads reported zero managed bytes per operation after setup. Encoding a 4 KiB payload measured about 9.91 µs, and validation about 9.72 µs. Encoding 64 KiB measured about 151 µs. These measurements cover caller-buffer codec/checksum work, including payload copying where applicable. They do not measure file I/O or disk flush latency. Short shared-host measurements have broad error intervals; profile real save sizes and target devices.
