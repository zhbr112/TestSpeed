// using System.Diagnostics;
// using System.Numerics;
// using ZLinq;
// using ZLinq.Simd;

// static unsafe int PFor(int[] arr)
// {
//     int sum = 0;
//     for(int i = 0; i < arr.Length; i++)
//         sum += arr[i];
    
//     return sum;
// }

// static unsafe int PForeach(int[] arr)
// {
//     int sum = 0;
//     foreach(int i in arr)
//         sum += i;
    
//     return sum;
// }

// static unsafe int PLINQ(int[] arr)
// {
//     return arr.Sum();
// }

// static int PZLINQ(int[] arr)
// {
//     return arr.AsVectorizable().Aggregate((x, y) =>Vector.Add(x,y),(x, y)=>x+y);
// }

// static unsafe int PSIMD(int[] arr)
// {
//     int n = arr.Length;
//     int width = Vector<int>.Count;
//     var vSum = Vector<int>.Zero;

//     int i = 0;
//     for (; i <= n - width; i += width)
//     {
//         vSum += new Vector<int>(arr, i);
//     }

//     int sum = 0;
//     for(int j =0;j<width;j++)
//     {
//         sum += vSum[j];
//     }
//     for (; i < n; i++)
//     {
//         sum += arr[i];
//     }

//     return sum;
// }

// static unsafe int PSpanSIMD(Span<int> arr)
// {
//     int n = arr.Length;
//     int width = Vector<int>.Count;
//     var vSum = Vector<int>.Zero;

//     int i = 0;
//     for (; i <= n - width; i += width)
//     {
//         vSum += new Vector<int>(arr.Slice(i,width));
//     }

//     int sum = 0;
//     for (int j = 0; j < width; j++)
//     {
//         sum += vSum[j];
//     }
//     for (; i < n; i++)
//     {
//         sum += arr[i];
//     }

//     return sum;
// }

// static unsafe int PSIMD_mt(int[] arr)
// {
//     int processorCount = Environment.ProcessorCount;
//     int[] partialSums = new int[processorCount];
//     int chankSize = arr.Length / processorCount;

//     Parallel.For(0, processorCount, i =>
//     {
//         int start = i * chankSize;
//         int end = (i == processorCount - 1) ? arr.Length : start + chankSize;

//         var slice = new Span<int>(arr, start, end - start);

//         int sum = PSpanSIMD(slice);

//         partialSums[i] += sum;
//     });

//     return partialSums.Sum();
// }

// static unsafe int PSIMD_mt2(int[] arr)
// {
//     int processorCount = Environment.ProcessorCount;
//     int[] partialSums = new int[processorCount];
//     int chankSize = arr.Length / processorCount;
//     var theardList = new Thread[processorCount];
//     for (int i = 0; i < processorCount; i++)
//     {
//         theardList[i] = new Thread((object? j) =>
//         {
//             int ii = (int)j!;
//             int start = ii * chankSize;
//             int end = (ii == processorCount - 1) ? arr.Length : start + chankSize;

//             var slice = new Span<int>(arr, start, end - start);

//             int sum = PSpanSIMD(slice);

//             partialSums[ii] += sum;
//         });

//         theardList[i].Start(i);
//     }

//     foreach(Thread thread in theardList) thread.Join();

//     return partialSums.Sum();
// }

// static unsafe void Bench(int[] arr, int iter)
// {
//     var times = new List<List<double>>();
//     times.Add([]);
//     times.Add([]);
//     times.Add([]);
//     times.Add([]);

//     Stopwatch sw;
//     int res;
//     for(int i = 0;i < iter;i++)
//     {

//         sw = Stopwatch.StartNew();
//         res = PLINQ(arr);
//         sw.Stop();
//         times[0].Add(sw.ElapsedMilliseconds);

//         sw = Stopwatch.StartNew();
//         res = PZLINQ(arr);
//         sw.Stop();
//         times[1].Add(sw.ElapsedMilliseconds);

//         sw = Stopwatch.StartNew();
//         res = PSIMD(arr);
//         sw.Stop();
//         times[2].Add(sw.ElapsedMilliseconds);

//         sw = Stopwatch.StartNew();
//         res = PSIMD_mt(arr);
//         sw.Stop();
//         times[3].Add(sw.ElapsedMilliseconds);
//     }

//     foreach(var (inx, i) in times.Index())
//     {
//         var q=i.ToList();

//         q.Sort();
//         double mean = q.Average();
//         double median = q[q.Count / 2];

//         var a = inx switch
//         {
//             0 => "Linq",
//             1 => "ZLinq",
//             2 => "SIMD",
//             3 => "SIMD Parallel",
//             _ => ""

//         };
//         Console.WriteLine($"{a}: {median}");
//     }
// }
// unsafe
// {
//     const int N = 200_000_000;
//     var arr = new int[N];
//     for (int i = 0; i < N; i++)
//     {
//         arr[i] = 1;
//     }

//     Bench(arr, 50);
// }
