# Image deployment benchmark

Requires the .NET 10 SDK and a built OpenTAP installation.

```sh
dotnet run benchmark.cs
```

No arguments. Run from your OpenTAP installation or built repository. The script finds
nearby OpenTAP binaries automatically (preferring Release over Debug when both exist),
prints the selected location, generates packages once, and runs the benchmarks.

| Image | Packages | Extracted payload |
|---|---:|---:|
| Many small | 100 × 256 KiB, 16 files each | 25 MiB |
| Few large | 5 × 100 MiB | 500 MiB |
| Mixed | 50 small + 3 large | 312.5 MiB |

Each image is measured using local package files only: no downloads or HTTP server.
Each image gets one discarded warmup and three measured runs, with fresh targets and
isolated caches. The normal OpenTAP cache is untouched.

Only `ImageIdentifier.Deploy()` is timed. Generation, startup, cache preparation,
payload hash validation, and cleanup are excluded. Production binaries are loaded
without rebuilding or modifying them. Data-only packages have no custom install actions.

Packages and JSON results/logs are saved under `artifacts/` beside the script. Deployment
targets and caches are cleaned after each sample. Allow several GiB of disk headroom.
Packages persist and are reused automatically on repeated runs; generation is skipped.
Deployment targets remain fresh to avoid measuring an already-installed no-op.

The first successful run saves `artifacts/baseline.json`. Later runs leave it untouched
and print median-time percentage changes against it (negative means faster). Each run
also keeps its own results. Delete `baseline.json` only when you want to establish a new
baseline. Comparisons are skipped if the generated workload differs from the baseline.

OS filesystem caches are not cleared. The harness runs under .NET 10.
