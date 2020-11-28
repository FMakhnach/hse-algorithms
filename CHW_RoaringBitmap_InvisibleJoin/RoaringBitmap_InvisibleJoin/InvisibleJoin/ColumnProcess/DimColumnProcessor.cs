using RoaringBitmap_InvisibleJoin.Bitmaps;
using RoaringBitmap_InvisibleJoin.Utils;
using System;
using System.Collections.Generic;

namespace RoaringBitmap_InvisibleJoin.InvisibleJoin.ColumnProcess
{
    /// <summary>
    /// Processes particular columns of dimensional tables.
    /// </summary>
    public class DimColumnProcessor : IColumnProcessor
    {
        /// <summary>
        /// Path to dimensional table.
        /// </summary>
        private readonly string path;
        /// <summary>
        /// Column number, which this instance represent.
        /// </summary>
        private readonly int columnNum;
        /// <summary>
        /// Facts table, which keeps primary keys of this dimensional table.
        /// </summary>
        private readonly PrimaryKeyFactsColumnProcessor primaryKeysFactsColumn;
        /// <summary>
        /// Filter producer especially for this column (depends on content type).
        /// </summary>
        private readonly FilterProducer filterProducer;
        /// <summary>
        /// DB main object.
        /// </summary>
        private readonly DBModel db;
        /// <summary>
        /// Name of the dimensional table.
        /// </summary>
        private readonly string tableName;

        public DimColumnProcessor(string path, int columnNum,
            FilterProducer filterProducer, string tableName,
            DBModel db, PrimaryKeyFactsColumnProcessor factsColumn)
        {
            this.path = path;
            this.columnNum = columnNum;
            primaryKeysFactsColumn = factsColumn;
            this.db = db;
            this.filterProducer = filterProducer;
            this.tableName = tableName;
        }

        /// <summary>
        /// Filters the table with given operation and rhs value.
        /// </summary>
        public void Filter(string operation, string otherValue)
            => Filter(filterProducer(operation, otherValue));

        /// <summary>
        /// Returns values of the column after all filtrations.
        /// </summary>
        public List<string> GetValues()
        {
            // We use respective facts column to extract primary keys.
            var keys = primaryKeysFactsColumn.GetKeys();
            // Then we get [cached] table lines as dictionary.
            var lines = db.GetDimTableLines(tableName);
            var result = new List<string>();
            // For each key we get the respective value.
            foreach (var key in keys)
            {
                result.Add(lines[key][columnNum]);
            }
            return result;
        }


        /// <summary>
        /// Filters the table using given predicate.
        /// </summary>
        private void Filter(Predicate<string> filter)
        {
            RoaringBitmap dimBitmap = new RoaringBitmap();
            // Constructing bitmap based on table.
            TableReader.ForEachSplitedLine(path, (i, parts) =>
            {
                if (filter(parts[columnNum]))
                {
                    // Parts[0] is primary key.
                    dimBitmap.Set(int.Parse(parts[0]), true);
                }
            });
            // Pushing bitmap to db.
            db.PushBitmap(tableName, dimBitmap);
        }
    }
}
