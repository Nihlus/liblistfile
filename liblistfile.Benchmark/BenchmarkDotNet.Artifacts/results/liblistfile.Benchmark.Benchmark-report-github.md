``` ini

BenchmarkDotNet=v0.11.3, OS=linuxmint 19
Intel Core i7-4790K CPU 4.00GHz (Haswell), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=2.1.500
  [Host]     : .NET Core 2.1.6 (CoreCLR 4.6.27019.06, CoreFX 4.6.27019.05), 64bit RyuJIT
  DefaultJob : .NET Core 2.1.6 (CoreCLR 4.6.27019.06, CoreFX 4.6.27019.05), 64bit RyuJIT


```
|       Method |     Mean |    Error |   StdDev |
|------------- |---------:|---------:|---------:|
| OldAlgorithm | 150.2 ms | 2.125 ms | 1.987 ms |
