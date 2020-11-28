using RoaringBitmap_InvisibleJoin.Bitmaps;
using RoaringBitmap_InvisibleJoin.Utils;
using System;
using System.Collections.Generic;

namespace RoaringBitmap_InvisibleJoin.InvisibleJoin.ColumnProcess
{
    /// <summary>
    /// Processes particular columns of the facts table.
    /// </summary>
    public class FactsColumnProcessor : IColumnProcessor
    {
        private const string tableName = "FactResellerSales";

        /// <summary>
        /// Path to facts table column.
        /// </summary>
        protected string Path { get; }
        /// <summary>
        /// DB main object.
        /// </summary>
        protected DBModel Db { get; }
        /// <summary>
        /// Filter producer especially for this column (depends on content type).
        /// </summary>
        private readonly FilterProducer filterProducer;

        public FactsColumnProcessor(string path, FilterProducer filterProducer, DBModel db)
        {
            Path = path;
            this.filterProducer = filterProducer;
            Db = db;
        }

        /// <summary>
        /// Filters table using given comparison operation and rhs value.
        /// </summary>
        public void Filter(string operation, string otherValue)
            => Filter(filterProducer(operation, otherValue));

        /// <summary>
        /// Returns values of the column after all filtrations.
        /// </summary>
        public List<string> GetValues()
        {
            Bitmap factsTableMask = Db.GetFactsBitmap();
            // The only case when this method will be called more than once is when
            // we specified the column as an output one more than one time (and it's kinda ridiculous).
            // Thats why I don't cache this one.
            var result = new List<string>();
            TableReader.ForEachLine(Path, (i, line) =>
            {
                if (factsTableMask.Get(i))
                {
                    result.Add(line);
                }
            });

            return result;
        }


        /// <summary>
        /// Filters the table using given predicate.
        /// </summary>
        private void Filter(Predicate<string> filter)
        {
            var bitmap = new RoaringBitmap();
            TableReader.ForEachLine(Path, (i, line) =>
            {
                if (filter(line))
                {
                    bitmap.Set(i, true);
                }
            });
            Db.PushBitmap(tableName, bitmap);
        }
    }
}
