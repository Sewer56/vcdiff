﻿using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#if NETCOREAPP3_1 || NET5_0
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace VCDiff.Shared
{
    internal static class Intrinsics
    {
        public const int AvxRegisterSize = 32;
        public const int SseRegisterSize = 16;
        public static readonly int MaxRegisterSize;
        public static readonly bool UseAvx;

        static Intrinsics()
        {
#if NETCOREAPP3_1 || NET5_0
            if (Sse2.IsSupported)
            {
                MaxRegisterSize = SseRegisterSize;
                UseAvx = false;
            }

            if (Avx.IsSupported)
            {
                MaxRegisterSize = AvxRegisterSize;
                UseAvx = true;
            }

            // Not set.
            if (MaxRegisterSize == 0)
            {
                // bytesLeft will never exceed.
                MaxRegisterSize = int.MaxValue;
            }
#endif
        }

        public static unsafe void FillArrayVectorized(long* first, int numValues, long value)
        {
#if NETCOREAPP3_1 || NET5_0
            long bytesLeft = (long)((long)numValues * sizeof(long));
            if (bytesLeft >= MaxRegisterSize)
            {
                // Note: This can be 0 cost in .NET 5 when paired with pinned GC.AllocateUnitializedArray.
                if (UseAvx)
                    Avx2FillArray(first, value, ref bytesLeft);
                else
                    Sse2FillArray(first, value, ref bytesLeft);
                    
                // Fill rest of array.
                var elementsLeft = (int)(bytesLeft / sizeof(long));
                for (int x = numValues - elementsLeft; x < numValues; x++)
                    first[x] = value;
            }
            else
            {
                // Copy remaining elements.
                for (int x = 0; x < numValues; x++)
                    first[x] = value;
            }
#else
            // Accelerate via loop unrolled solution.
            MemoryMarshal.CreateSpan(ref Unsafe.AsRef<long>((void*) first), numValues).Fill(value);
#endif
        }


#if NETCOREAPP3_1 || NET5_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static unsafe void Sse2FillArray(long* first, long value, ref long bytesLeft)
        {
            // Initialize.
            var numValues = SseRegisterSize / sizeof(long);
            var vectorValues = stackalloc long[numValues];
            FillPointer(vectorValues, value, numValues);

            var vector = Sse2.LoadVector128(vectorValues);
            while (bytesLeft >= SseRegisterSize)
            {
                Sse2.Store(first, vector);
                first += numValues;
                bytesLeft -= SseRegisterSize;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static unsafe void Avx2FillArray(long* first, long value, ref long bytesLeft)
        {
            // Initialize.
            var numValues    = AvxRegisterSize / sizeof(long);
            var vectorValues = stackalloc long[numValues];
            FillPointer(vectorValues, value, numValues);

            var vector = Avx2.LoadVector256(vectorValues);
            while (bytesLeft >= AvxRegisterSize)
            {
                Avx2.Store(first, vector);
                first += numValues;
                bytesLeft -= AvxRegisterSize;
            }
        }

        private static unsafe void FillPointer(long* values, long value, int numValues)
        {
            for (int x = 0; x < numValues; x++)
            {
                *values = value;
                values += 1;
            }
        }
#endif
    }
}
