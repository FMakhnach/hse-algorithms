using System.Collections.Generic;

namespace RoaringBitmap_InvisibleJoin.Utils
{
    /// <summary>
    /// A bit of stuff.
    /// </summary>
    public static class BitwiseOperations
    {
        /// <summary>
        /// Returns value of certain bit of integer value.
        /// </summary>
        public static bool GetBit(long value, int bitPos)
            => (((long)1 << bitPos) & value) != 0;

        public static int Div2(int value, int powerOf2)
            => (value + ((value >> 31) & ((1 << powerOf2) + ~0))) >> powerOf2;

        /// <summary>
        /// Returns remained by given powerOf2.
        /// ATTENTION: powerOf2 must be actually a power of 2.
        /// </summary>
        public static int Mod2(int value, int powerOf2) => value & (powerOf2 - 1);

        /// <summary>
        /// Sets a particular bit of long value to the given bool value.
        /// </summary>
        public static long SetBit(long value, int bitPos, bool newBitValue)
        {
            long mask = (long)1 << bitPos;
            return newBitValue ? (value | mask) : (value & ~mask);
        }

        /// <summary>
        /// Popcnt for losers.
        /// </summary>
        public static int SparseBitcount(long n)
        {
            int count = 0;
            while (n != 0)
            {
                ++count;
                n &= (n - 1);
            }
            return count;
        }

        /// <summary>
        /// Finds all occurences of 1 in binary integer representation.
        /// </summary>
        /// <see cref="https://arxiv.org/pdf/1402.6407.pdf"/> 
        /// <returns> Positions ([0..63]) where 1 occures. </returns>
        public static List<int> TruePositions(long value)
        {
            List<int> res = new List<int>();
            long t;
            while (value != 0)
            {
                t = value & (-value);
                res.Add(SparseBitcount(t - 1));
                value &= value - 1;
            }
            return res;
        }
    }
}
