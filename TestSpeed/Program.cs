using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using ZLinq;


BenchmarkRunner.Run<Test>();

[MemoryDiagnoser]
[DisassemblyDiagnoser(maxDepth: 3)]
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
    public int PartitionerChunk()
    {
        var q = Partitioner.Create(0 , Arr.Length);
        int sum = 0;
        Parallel.ForEach(q, () => 0, (range, _, localSum) =>
        {
            localSum += PSpanSIMD(new ReadOnlySpan<int>(Arr, range.Item1, range.Item2 - range.Item1));
            return localSum;
        }, localSum =>
        {
            Interlocked.Add(ref sum, localSum);
        });
        return sum;
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

    public static int PSpanSIMD(ReadOnlySpan<int> Arr)
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

    [Benchmark(Baseline = true)]
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
        public required T[] _items;
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

public static class FastestArraySummer
{
    public static int Sum(int[] array)
    {
        if (array == null || array.Length == 0)
        {
            return 0;
        }

        // Для небольших массивов накладные расходы на распараллеливание не окупаются.
        // Простой последовательный цикл работает быстрее. Порог подбирается эмпирически.
        if (array.Length < 16384) // 16k элементов
        {
            int sum = 0;
            foreach (int item in array)
            {
                sum += item;
            }
            return sum;
        }

        // Используем Partitioner для создания диапазонов для параллельной обработки.
        // Это обеспечивает лучшую балансировку нагрузки, чем ручное разбиение на чанки.
        var rangePartitioner = Partitioner.Create(0, array.Length);

        long totalSum = 0;

        // Обрабатываем диапазоны параллельно.
        Parallel.ForEach(
            rangePartitioner,
            // Инициализируем локальную для потока сумму (0L - long). Это позволяет избежать блокировок в цикле.
            () => 0L,
            // Основное тело цикла, выполняемое каждым потоком для своего поддиапазона.
            (range, loopState, localSum) =>
            {
                // Суммируем назначенный чанк с помощью оптимизированного SIMD-метода.
                localSum += SumSimd(new ReadOnlySpan<int>(array, range.Item1, range.Item2 - range.Item1));
                return localSum;
            },
            // Агрегируем конечный результат из локальной суммы каждого потока.
            (localSum) => Interlocked.Add(ref totalSum, localSum)
        );

        // Сумма в предоставленном контексте помещается в int.
        // Для метода общего назначения, возможно, стоит возвращать long или выбрасывать исключение при переполнении.
        return (int)totalSum;
    }

    /// <summary>
    /// Вычисляет сумму для среза (span) целых чисел с использованием SIMD (Single Instruction, Multiple Data).
    /// </summary>
    private static long SumSimd(ReadOnlySpan<int> span)
    {
        if (span.IsEmpty)
        {
            return 0;
        }

        long sum = 0;
        var vSum = Vector<int>.Zero;
        int width = Vector<int>.Count;
        int end = span.Length - (span.Length % width);

        // Обрабатываем срез int как срез векторов для эффективной обработки.
        // Это позволяет избежать создания новых объектов Vector в цикле.
        var vectors = MemoryMarshal.Cast<int, Vector<int>>(span.Slice(0, end));
        foreach (var v in vectors)
        {
            vSum += v;
        }

        // Горизонтально суммируем элементы результирующего вектора.
        sum += Vector.Sum(vSum);

        // Суммируем оставшиеся элементы, которые не поместились в полный вектор.
        for (int i = end; i < span.Length; i++)
        {
            sum += span[i];
        }

        return sum;
    }
}