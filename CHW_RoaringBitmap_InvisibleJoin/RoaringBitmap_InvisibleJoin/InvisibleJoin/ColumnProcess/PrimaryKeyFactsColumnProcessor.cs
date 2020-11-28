using RoaringBitmap_InvisibleJoin.Bitmaps;
using RoaringBitmap_InvisibleJoin.Utils;
using System.Collections.Generic;

namespace RoaringBitmap_InvisibleJoin.InvisibleJoin.ColumnProcess
{
    /// <summary>
    /// Extension for primary keys column. Caches the keys.
    /// </summary>
    public class PrimaryKeyFactsColumnProcessor : FactsColumnProcessor
    {
        private List<int> primaryKeysCache = null;

        public PrimaryKeyFactsColumnProcessor(string path, FilterProducer filterProducer,
            DBModel db) : base(path, filterProducer, db)
        { }

        /// <summary>
        /// Returns values of this column based on the facts bitmap.
        /// </summary>
        public List<int> GetKeys()
        {
            if (primaryKeysCache == null)
            {
                primaryKeysCache = new List<int>();
                Bitmap bitmap = Db.GetFactsBitmap();
                TableReader.ForEachLine(Path, (i, line) =>
                {
                    if (bitmap.Get(i))
                    {
                        primaryKeysCache.Add(int.Parse(line));
                    }
                });
            }
            return primaryKeysCache;
        }
    }
}
