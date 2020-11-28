namespace RoaringBitmap_InvisibleJoin.Bitmaps
{
    /// <summary>
    /// Interface for bitmap containers (array and bitmap ones).
    /// </summary>
    public interface IRoaringBitmapContainer
    {
        bool this[int i] { get; set; }
        int Cardinality { get; }
        /// <summary>
        /// Rebuilds one type of container to another. 
        /// </summary>
        /// <param name="newElemKey"> Element which caused the rebuilding. </param>
        IRoaringBitmapContainer Rebuild(int newElem);
        /// <summary>
        /// Performs logical and with other bitmap container.
        /// If it is possible, the no object is constructed (returns itself).
        /// </summary>
        IRoaringBitmapContainer And(IRoaringBitmapContainer other);
    }
}
