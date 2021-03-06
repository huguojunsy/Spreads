﻿// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Runtime.CompilerServices;

namespace Spreads.Algorithms
{
    // TODO this structure is probably bad idea, namespace+types are simpler

    public static class Algo
    {
        public static MathProvider Math = MathProvider.Instance;

        public class MathProvider
        {
            internal static MathProvider Instance = new MathProvider();

            private MathProvider()
            {
            }
        }

        public static HashProvider Hash = HashProvider.Instance;

        public class HashProvider
        {
            internal static HashProvider Instance = new HashProvider();

            private HashProvider()
            {
            }
        }
    }

    // we could use extension methods to extend Algo.Math, it is quite convenient at first glance
    // extension methods are normal static methods and could be inlined by JIT
    //
    internal static class SimpleMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AddTwoInts(this Algo.MathProvider provider, int first, int second)
        {
            return first + second;
        }

        public static void TestMe()
        {
            System.Math.Abs(-1);
            Algo.Math.AddTwoInts(42, 3);
        }
    }
}