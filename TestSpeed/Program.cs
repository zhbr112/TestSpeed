using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Disassemblers;
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
    public int PZLINQ_Unchecked()
    {
        return Arr.AsValueEnumerable().SumUnchecked();
    }

    [Benchmark]
    public int AsParallel()
    {
        return Arr
        .ChunkMemory(Arr.Length / Environment.ProcessorCount)
        .AsParallel()
        .Sum(x => x.AsValueEnumerable().Sum());
    }

    [Benchmark]
    public int ChunkMemory()
    {
        return Arr
        .ChunkMemory(Arr.Length / Environment.ProcessorCount)
        .Select(x => Task.Run(() => x.AsValueEnumerable().Sum())).WhenTasks()
        .Sum();
    }

    [Benchmark]
    public int ForEachChunkSpanParallel()
    {
        return Arr
        .ForEachChunkSpanParallel(Arr.Length / Environment.ProcessorCount, chunk => chunk.AsValueEnumerable().Sum())
        .AsValueEnumerable().Sum();
    }

    [Benchmark]
    public int PSIMD_mt_Parallel()
    {
        int processorCount = Environment.ProcessorCount;
        int[] partialSums = new int[processorCount];
        int chankSize = Arr.Length / processorCount;

        Parallel.For(0, processorCount, i =>
        {
            int start = i * chankSize;
            int end = (i == processorCount - 1) ? Arr.Length : start + chankSize;



            partialSums[i] += PSpanSIMD(new Span<int>(Arr, start, end - start));
        });

        return partialSums.Sum();
    }

    [Benchmark]
    public int PSIMD_mt_Tasks()
    {
        int processorCount = Environment.ProcessorCount;
        var partialSums = new Task<int>[processorCount];
        int chankSize = Arr.Length / processorCount;

        for (int i = 0; i < processorCount; i++)
        {
            int start = i * chankSize;
            int end = (i == processorCount - 1) ? Arr.Length : start + chankSize;

            partialSums[i] = Task.Run(() => PSpanSIMD(new Span<int>(Arr, start, end - start)));
        }

        return Task.WhenAll(partialSums).Result.Sum();
    }
}

public static class ArrayExtensions
{
    public static IEnumerable<T> WhenTasks<T>(this IEnumerable<Task<T>> sourceArray)
    {
        return Task.WhenAll(sourceArray).Result;
    }
    /// <summary>
    /// Выполняет действие для каждого чанка массива, представленного как Span<int>.
    /// Не выделяет память для самих чанков.
    /// </summary>
    /// <param name="sourceArray">Исходный массив.</param>
    /// <param name="chunkSize">Размер чанка.</param>
    /// <param name="chunkAction">Действие, которое нужно выполнить для каждого чанка.</param>
        // Перегрузка для изменяемых чанков (Span<T> вместо ReadOnlySpan<T>)
    public static IEnumerable<TResult> ForEachChunkSpanParallel<TSourse, TResult>(this TSourse[] sourceArray, int chunkSize, Func<ReadOnlySpan<TSourse>, TResult> chunkAction)
    {
        int processorCount = Environment.ProcessorCount;
        Task<TResult>[] partialSums = new Task<TResult>[processorCount];

        if (sourceArray == null || chunkAction == null) return [];
        if (chunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSize));

        for (int i = 0; i < processorCount; i++)
        {
            int start = i * chunkSize;
            int end = (i == processorCount - 1) ? sourceArray.Length : start + chunkSize;

            partialSums[i] = Task.Run(() =>
            {
                ReadOnlySpan<TSourse> chunk = new ReadOnlySpan<TSourse>(sourceArray, start, end - start);

                return chunkAction(chunk);
            });

        }

        // Parallel.For(0, processorCount, i =>
        // {
        //     int start = i * chunkSize;
        //     int end = (i == processorCount - 1) ? sourceArray.Length : start + chunkSize;

        //     ReadOnlySpan<TSourse> chunk = new ReadOnlySpan<TSourse>(sourceArray, start, end - start);

        //     partialSums[i] = chunkAction(chunk);
        // });

        return Task.WhenAll(partialSums).Result;
    }

    public static IEnumerable<Memory<T>> ChunkMemory<T>(this IEnumerable<T> source, int chunkSize)
    {
        int processorCount = Environment.ProcessorCount;

        if (chunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSize));

        Memory<T> sourceArray;

        if (source.GetType() == typeof(T[]))
        {
            sourceArray = Unsafe.As<T[]>(source);
        }
        else if (source.GetType() == typeof(List<T>))
        {
            sourceArray = ListMarshal.AsMemory(Unsafe.As<List<T>>(source));
        }
        else
        {
            sourceArray = source.ToArray();
        }

        for (int i = 0; i < processorCount; i++)
        {
            int start = i * chunkSize;
            int end = (i == processorCount - 1) ? sourceArray.Length : start + chunkSize;

            //var chunk = new Memory<T>(sourceArray, start, end - start);

            yield return sourceArray.Slice(start, end - start);
        }
    }
}
public static class ListMarshal
{
    // Класс-двойник для доступа к внутренним полям List<T>
    private sealed class ListInternals<T>
    {
        public T[] _items;
    }

    /// <summary>
    /// Создает Memory<T> на основе внутреннего хранилища списка List<T> без копирования.
    /// </summary>
    /// <remarks>
    /// ⚠️ ОПАСНО: Полученный Memory<T> становится недействительным, если список изменяется
    /// (например, при добавлении/удалении элементов), так как это может привести к
    /// перераспределению внутреннего массива. Использование недействительного Memory<T>
    /// может привести к повреждению памяти.
    /// </remarks>
    public static Memory<T> AsMemory<T>(List<T> list)
    {
        if (list == null)
        {
            return Memory<T>.Empty;
        }

        // Реинтерпретируем ссылку на List<T> как ссылку на наш класс-двойник,
        // чтобы получить доступ к приватному полю _items.
        var internals = Unsafe.As<ListInternals<T>>(list);

        // Создаем Memory<T> из внутреннего массива, используя реальное количество элементов (Count).
        return new Memory<T>(internals._items, 0, list.Count);
    }
}