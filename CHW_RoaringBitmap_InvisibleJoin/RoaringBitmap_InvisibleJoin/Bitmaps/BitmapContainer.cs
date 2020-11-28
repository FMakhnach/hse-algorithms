using RoaringBitmap_InvisibleJoin.Utils;

namespace RoaringBitmap_InvisibleJoin.Bitmaps
{
    /// <summary>
    /// Container for dense chunks of Roaring bitmap (> 4096 values). 
    /// Takes fixed space of 64 * 1024 + 32 = 2^16 + 32 bits.
    /// </summary>
    public class BitmapContainer : IRoaringBitmapContainer
    {
        /// <summary>
        /// Actual data keeper.
        /// </summary>
        private readonly long[] chunks = new long[1024];

        /// <summary>
        /// The amount of 1-bits.
        /// </summary>
        public int Cardinality { get; private set; } = 0;

        public bool this[int i]
        {
            get
            {
                int chunkId = i / 64;
                int bit = BitwiseOperations.Mod2(i, 64);
                return BitwiseOperations.GetBit(chunks[chunkId], bit);
            }
            set
            {
                int chunkId = i / 64;
                int bit = BitwiseOperations.Mod2(i, 64);
                // Checking whether we are changing the value.
                bool prevValue = BitwiseOperations.GetBit(chunks[chunkId], bit);
                if (prevValue == false && value == true)
                {
                    chunks[chunkId] = BitwiseOperations.SetBit(chunks[chunkId], bit, value);
                    ++Cardinality;
                }
                else if (prevValue == true && value == false)
                {
                    chunks[chunkId] = BitwiseOperations.SetBit(chunks[chunkId], bit, value);
                    --Cardinality;
                }
            }
        }

        /// <summary>
        /// Performs logical AND with ArrayContainer instance.
        /// </summary>
        /// <returns> New ArrayContainer intance </returns>
        public ArrayContainer And(ArrayContainer other)
            => new ArrayContainer(other).And(this);

        /// <summary>
        /// Performs logical AND with BitmapContainerInstance.
        /// </summary>
        /// <param name="other"></param>
        /// <see cref="https://arxiv.org/pdf/1402.6407.pdf"/>
        /// <returns> Itself if result cardinality is > 4096, new ArrayContainer object otherwise </returns>
        public IRoaringBitmapContainer And(BitmapContainer other)
        {
            int c = 0;
            for (int i = 0; i < 1024; ++i)
            {
                chunks[i] &= other.chunks[i];
                c += BitwiseOperations.SparseBitcount(chunks[i]);
            }
            if (c > 4096)
            {
                Cardinality = c;
                return this;
            }
            else
            {
                ushort[] values = new ushort[c];
                int index = -1;
                for (int i = 0; i < 1024; ++i)
                {
                    foreach (var pos in BitwiseOperations.TruePositions(chunks[i]))
                    {
                        values[++index] = (ushort)(64 * i + pos);
                    }
                }
                return new ArrayContainer(values);
            }
        }

        public IRoaringBitmapContainer And(IRoaringBitmapContainer other)
        {
            if (other is BitmapContainer) return And((BitmapContainer)other);
            return And((ArrayContainer)other);
        }

        /// <summary>
        /// Rebuilds BitmapContainer to ArrayContainer.
        /// </summary>
        public IRoaringBitmapContainer Rebuild(int newElem)
        {
            // Removing the 4097's element.
            this[newElem] = false;
            ushort[] values = new ushort[4096];
            int valuesIndex = 0;
            for (int i = 0; i < chunks.Length; ++i)
            {
                if (chunks[i] != 0)
                {
                    foreach (var pos in BitwiseOperations.TruePositions(chunks[i]))
                    {
                        values[valuesIndex++] = (ushort)(64 * i + pos);
                    }
                }
            }
            return new ArrayContainer(values);
        }
    }
}
