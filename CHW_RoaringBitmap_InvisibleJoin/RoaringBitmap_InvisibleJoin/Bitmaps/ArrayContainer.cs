using RoaringBitmap_InvisibleJoin.Utils;

namespace RoaringBitmap_InvisibleJoin.Bitmaps
{
    /// <summary>
    /// Container for sparse chunks of Roaring bitmap (<= 4096 values). 
    /// </summary>
    public class ArrayContainer : IRoaringBitmapContainer
    {
        /// <summary>
        /// Actual values, represented as sorted array.
        /// </summary>
        private readonly ArraySet values;

        /// <summary>
        /// Creates empty array container instance.
        /// </summary>
        public ArrayContainer() => values = new ArraySet();

        /// <summary>
        /// Creates an array container based on 16-bit values array.
        /// </summary>
        /// <param name="array"></param>
        public ArrayContainer(ushort[] array) => values = new ArraySet(array);

        public ArrayContainer(ArrayContainer other) => values = new ArraySet(other.values);

        public bool this[int i]
        {
            get => values.Contains((ushort)i);
            set
            {
                if (value) values.Insert((ushort)i);
                else values.Remove((ushort)i);
            }
        }

        public int Cardinality => values.Cardinality;

        /// <summary>
        /// Performs logical AND with another ArrayContainer instance.
        /// </summary>
        /// <returns> Itself </returns>
        public ArrayContainer And(ArrayContainer other)
        {
            // Just intersecting other values with values of that object.
            values.Intersect(other.values);
            return this;
        }

        /// <summary>
        /// Performs logical AND with BitmapContainer instance.
        /// </summary>
        /// <returns> Itself </returns>
        public ArrayContainer And(BitmapContainer other)
        {
            for (int i = 0; i < values.Cardinality; ++i)
            {
                // If we have no corresponding element in bitmap, 
                // we remove element from this arrayContainer.
                if (!other[values[i]])
                {
                    values.RemoveAt(i);
                    --i;
                }
            }
            return this;
        }

        public IRoaringBitmapContainer And(IRoaringBitmapContainer other)
        {
            if (other is BitmapContainer) return And((BitmapContainer)other);
            return And((ArrayContainer)other);
        }

        /// <summary>
        /// Rebuilds ArrayContainer to BitmapContainer.
        /// </summary>
        public IRoaringBitmapContainer Rebuild(int newElem)
        {
            BitmapContainer newContainer = new BitmapContainer();
            for (int i = 0; i < values.Cardinality; ++i)
            {
                newContainer[values[i]] = true;
            }
            newContainer[newElem] = true;
            return newContainer;
        }
    }
}
