using System;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

_ = Environment.Is64BitProcess;

var getHashCodeTest = new GetHashCodeTest();
getHashCodeTest.Setup();


BenchmarkRunner.Run(typeof(Program).Assembly);

//[SimpleJob(RuntimeMoniker.Net472)]
[SimpleJob(RuntimeMoniker.Net80)]
public class GetHashCodeTest
{
  private readonly object myObjectThin = new();
  private readonly object myObjectFat = new();
  private const int Count = 100000;

  [GlobalSetup]
  public void Setup()
  {
    if (ObjectHeaderUtils.HasSyncBlock(myObjectThin))
      throw new ArgumentException("No sync block expected");

    ObjectHeaderUtils.AllocateSyncBlock(myObjectFat);

    if (!ObjectHeaderUtils.HasSyncBlock(myObjectFat))
      throw new ArgumentException("Sync block expected");

    _ = myObjectThin.GetHashCode();
    _ = myObjectFat.GetHashCode();

    ObjectHeaderUtils.Dump(myObjectThin);
    ObjectHeaderUtils.Dump(myObjectFat);
  }

  [Benchmark(Baseline = true)]
  public int ThinHashCode()
  {
    return myObjectThin.GetHashCode();

    var sum = 0;
    for (var index = 0; index < Count; index++)
    {
      sum += myObjectThin.GetHashCode();
    }

    return sum;
  }

  [Benchmark]
  public int FatHashCode()
  {
    return myObjectFat.GetHashCode();

    var sum = 0;
    for (var index = 0; index < Count; index++)
    {
      sum += myObjectFat.GetHashCode();
    }

    return sum;
  }

  [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
  private void Use(int hashCode) { }
}