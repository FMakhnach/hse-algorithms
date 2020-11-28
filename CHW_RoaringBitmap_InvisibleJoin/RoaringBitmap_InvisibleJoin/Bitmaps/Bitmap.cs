namespace RoaringBitmap_InvisibleJoin.Bitmaps
{
    public abstract class Bitmap
    {
        /// <summary>
        /// Performs logical "AND" on bitmaps.
        /// </summary>
        public abstract void And(Bitmap other);

        /// <summary>
        /// Sets bit at index <paramref name="i"/> to a <paramref name="value"/>.
        /// </summary>
        public abstract void Set(int i, bool value);

        /// <summary>
        /// Returns bit at index <paramref name="i"/>.
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public abstract bool Get(int i);
    }
}
