using BenchmarkDotNet.Running;
using Kilo.ECS.Benchmark;

BenchmarkRunner.Run<EcsBenchmarks>(args: args);
