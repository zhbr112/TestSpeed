using System.Numerics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using ZLinq;
using ZLinq.Simd;

BenchmarkRunner.Run<Test>();

[MemoryDiagnoser]
public class Test
{
    private const int N = 200_000_000;

    private int[] Arr;

    public Test()
    {
        Arr = new int[N];
        for (int i = 0; i < N; i++)
        {
            Arr[i] = 1;
        }
    }

    [Benchmark]
    public int PFor()
    {
        int sum = 0;
        for (int i = 0; i < Arr.Length; i++)
            sum += Arr[i];

        return sum;
    }

    [Benchmark]
    public int PForeach()
    {
        int sum = 0;
        foreach (int i in Arr)
            sum += i;

        return sum;
    }

    [Benchmark]
    public int PLINQ()
    {
        return Arr.Sum();
    }

    [Benchmark]
    public int PZLINQ()
    {
        return Arr.AsValueEnumerable().Sum();
    }

    [Benchmark]
    public int PZLINQUncheked()
    {
        return Arr.AsVectorizable().SumUnchecked();
    }

    [Benchmark]
    public int PSIMD()
    {
        int n = Arr.Length;
        int width = Vector<int>.Count;
        var vSum = Vector<int>.Zero;

        int i = 0;
        for (; i <= n - width; i += width)
        {
            vSum += new Vector<int>(Arr, i);
        }

        int sum = 0;
        for (int j = 0; j < width; j++)
        {
            sum += vSum[j];
        }
        for (; i < n; i++)
        {
            sum += Arr[i];
        }

        return sum;
    }

    public static int PSpanSIMD(Span<int> Arr)
    {
        int n = Arr.Length;
        int width = Vector<int>.Count;
        var vSum = Vector<int>.Zero;

        int i = 0;
        for (; i <= n - width; i += width)
        {
            vSum += new Vector<int>(Arr.Slice(i, width));
        }

        int sum = 0;
        for (int j = 0; j < width; j++)
        {
            sum += vSum[j];
        }
        for (; i < n; i++)
        {
            sum += Arr[i];
        }

        return sum;
    }

    [Benchmark]
    public int PSIMD_mt()
    {
        int processorCount = Environment.ProcessorCount;
        int[] partialSums = new int[processorCount];
        int chankSize = Arr.Length / processorCount;

        Parallel.For(0, processorCount, i =>
        {
            int start = i * chankSize;
            int end = (i == processorCount - 1) ? Arr.Length : start + chankSize;

            var slice = new Span<int>(Arr, start, end - start);

            int sum = PSpanSIMD(slice);

            partialSums[i] += sum;
        });

        return partialSums.Sum();
    }
}