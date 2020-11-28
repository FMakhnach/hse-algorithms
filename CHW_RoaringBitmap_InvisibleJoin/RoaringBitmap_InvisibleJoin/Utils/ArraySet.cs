using System;

namespace RoaringBitmap_InvisibleJoin.Utils
{
    /// <summary>
    /// Keeps element in a sorted array of unique 16-bit integer (ushort) values.
    /// </summary>
    public class ArraySet
    {
        /// <summary>
        /// Max size of array.
        /// It is not an additional memory, but a compile time constant.
        /// </summary>
        private const int maxSize = 4096;
        private ushort[] array;

        public ArraySet() => array = new ushort[0];

        /// <summary>
        /// Creates a set based on given ushort array.
        /// ATTENTION: no deep copying is performed!
        /// Since we have absolutely no way to change the inner array 
        /// (each insertion and deletion is made with reallocating) it is safe.
        /// </summary>
        public ArraySet(ushort[] other) => array = other;

        /// <summary>
        /// Creates a set based on other set.
        /// </summary>
        /// <see cref="ArraySet(ushort[])"/>
        public ArraySet(ArraySet other) : this(other.array) { }

        public int Cardinality => array.Length;

        /// <summary>
        /// We need access to the elements in couple of places.
        /// </summary>
        public ushort this[int i] => array[i];

        public bool Contains(ushort value)
            => Array.BinarySearch(array, value) >= 0;

        public void Insert(ushort value)
        {
            int index = Array.BinarySearch(array, value);
            if (index < 0)
            {
                if (Cardinality == maxSize)
                    throw new InvalidOperationException("Cannot add a new element to full array set!");
                Array.Resize(ref array, array.Length + 1);
                for (int i = array.Length - 1; i > ~index; --i)
                {
                    array[i] = array[i - 1];
                }
                array[~index] = value;
            }
        }

        public void Intersect(ArraySet other)
        {
            // Empty set intersection is empty set.
            if (array.Length == 0 || other.array.Length == 0)
            {
                array = new ushort[0];
                return;
            }
            // i to go through inner array, j to go throw other array, k for result array.
            int i = 0, j = 0, k = 0;
            // Merging two sorted arrays.
            // The condition for exiting loop is both pointers at the correcponding ends.
            while (i != array.Length && j != other.array.Length)
            {
                if (array[i] < other.array[j]) ++i;
                else if (array[i] > other.array[j]) ++j;
                else
                {
                    // We can use the inner array as an array for result, cause the result
                    // of this whole operation must be subset of array.
                    array[k++] = array[i];
                    ++i; ++j;
                }
            }
            if (i == array.Length)
            {
                ushort last = array[array.Length - 1];
                for (; j < other.array.Length; ++j)
                {
                    if (last == other.array[j]) array[k++] = last;
                    else if (last < other.array[j]) break;
                }
            }
            else if (j == array.Length)
            {
                ushort last = other.array[other.array.Length - 1];
                for (; i < array.Length; ++i)
                {
                    if (array[i] == last) array[k++] = last;
                    else if (array[i] > last) break;
                }
            }
            // At that point k is new array size.
            if (k != array.Length)
            {
                Array.Resize(ref array, k);
            }
        }

        /// <summary>
        /// Removing element by value (if exists).
        /// </summary>
        public void Remove(ushort value)
        {
            int index = Array.BinarySearch(array, value);
            if (index >= 0) RemoveAt(index);
        }

        /// <summary>
        /// Removing element at particular index.
        /// </summary>
        /// <param name="index"></param>
        public void RemoveAt(int index)
        {
            for (int i = index; i < array.Length - 1; ++i)
            {
                array[i] = array[i + 1];
            }
            Array.Resize(ref array, array.Length - 1);
        }
    }
}
