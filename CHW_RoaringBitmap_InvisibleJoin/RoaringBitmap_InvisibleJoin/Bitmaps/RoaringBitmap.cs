using RoaringBitmap_InvisibleJoin.Utils;
using System;

namespace RoaringBitmap_InvisibleJoin.Bitmaps
{
    public class RoaringBitmap : Bitmap
    {
        private int[] mostSignificantBits = new int[0];
        private IRoaringBitmapContainer[] containers = new IRoaringBitmapContainer[0];

        public override void And(Bitmap other)
        {
            RoaringBitmap roaringBitmap = other as RoaringBitmap;
            if (roaringBitmap != null)
            {
                // Performing logical AND for each chunk.
                for (int i = 0; i < containers.Length; i++)
                {
                    // We look for similar chunk in other bitmap via GetContainerByKey.
                    containers[i] = containers[i].And(roaringBitmap
                        .GetContainerByKey(mostSignificantBits[i]));
                }
            }
            else throw new InvalidOperationException(
                "Your bitmap isn't RoaringBitmap -- are you rewriting my code?");
        }

        public override bool Get(int i)
        {
            int sigBits = i / (1 << 16);
            // Looking for the particular container.
            int index = Array.BinarySearch(mostSignificantBits, sigBits);
            int val = BitwiseOperations.Mod2(i, 1 << 16);
            return index >= 0 && containers[index][val];
        }

        public override void Set(int i, bool value)
        {
            int key = i / (1 << 16);
            int index = Array.BinarySearch(mostSignificantBits, key);
            if (index >= 0)
            {
                int containerKey = BitwiseOperations.Mod2(i, 1 << 16);
                if (IsOverflowing(index, containerKey, value) ||
                    IsUnderflowing(index, containerKey, value))
                {
                    containers[index] = containers[index].Rebuild(containerKey);
                }
                else
                {
                    containers[index][containerKey] = value;
                }
            }
            else
            {
                // If value is false, we don't have to bother creating a container.
                if (value)
                {
                    Array.Resize(ref containers, containers.Length + 1);
                    Array.Resize(ref mostSignificantBits, mostSignificantBits.Length + 1);
                    for (int j = ~index + 1; j < containers.Length; ++j)
                    {
                        containers[j] = containers[j - 1];
                        mostSignificantBits[j] = mostSignificantBits[j - 1];
                    }
                    mostSignificantBits[~index] = key;
                    containers[~index] = new ArrayContainer();
                    containers[~index][BitwiseOperations.Mod2(i, 1 << 16)] = true;
                }
            }
        }

        /// <summary>
        /// Indicates whether container at <paramref name="index"/> is going to overflow
        /// if we add <paramref name="containerKey"/>.
        /// </summary>
        private bool IsOverflowing(int index, int containerKey, bool value)
            => containers[index].Cardinality == 4096
                    && containers[index][containerKey] == false && value == true;

        /// <summary>
        /// Indicates whether container at <paramref name="index"/> is going to underflow
        /// if we remove <paramref name="containerKey"/>.
        /// </summary>
        private bool IsUnderflowing(int index, int containerKey, bool value)
            => containers[index].Cardinality == 4097
                    && containers[index][containerKey] == true && value == false;

        /// <summary>
        /// Looks for container with <paramref name="mostSignificantBits"/> most significant (16) bits.
        /// </summary>
        /// <param name="mostSignificantBits"></param>
        /// <returns> Container with elements with most significant bits <paramref name="mostSignificantBits"/>.
        /// Empty ArrayContainer, if such container wasn't found. </returns>
        private IRoaringBitmapContainer GetContainerByKey(int mostSignificantBits)
        {
            int index = Array.BinarySearch(this.mostSignificantBits, mostSignificantBits);
            if (index >= 0) return containers[index];
            return new ArrayContainer();
        }
    }
}
