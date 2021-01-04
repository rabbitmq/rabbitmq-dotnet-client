``` ini

BenchmarkDotNet=v0.12.1, OS=macOS 11.0.1 (20B50) [Darwin 20.1.0]
Intel Core i9-9980HK CPU 2.40GHz, 1 CPU, 16 logical and 8 physical cores
.NET Core SDK=5.0.101
  [Host]     : .NET Core 5.0.1 (CoreCLR 5.0.120.57516, CoreFX 5.0.120.57516), X64 RyuJIT
  DefaultJob : .NET Core 5.0.1 (CoreCLR 5.0.120.57516, CoreFX 5.0.120.57516), X64 RyuJIT


```
|              Method |     Mean |   Error |   StdDev | Ratio |     Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------------- |---------:|--------:|---------:|------:|----------:|------:|------:|----------:|
| Publish_Hello_World | 317.8 ms | 8.32 ms | 24.28 ms |  1.00 | 1000.0000 |     - |     - |  11.01 MB |
