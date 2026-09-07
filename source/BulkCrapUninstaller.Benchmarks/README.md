# BCU algorithm benchmarks

These benchmarks compare the collection and search patterns changed by the static-complexity work. They use modest input sizes so a complete run remains suitable for a developer workstation.

Run all benchmarks from the repository root on Windows:

```powershell
dotnet run --project source/BulkCrapUninstaller.Benchmarks -c Release
```

Run one group while iterating:

```powershell
dotnet run --project source/BulkCrapUninstaller.Benchmarks -c Release -- --filter "*LookupBenchmarks*"
```

The groups cover repeated GUID or registry-path lookup, list versus set membership, confidence-record merging, multi-pattern substring search, and maximum selection without sorting. BenchmarkDotNet writes detailed artifacts under `BenchmarkDotNet.Artifacts`.
