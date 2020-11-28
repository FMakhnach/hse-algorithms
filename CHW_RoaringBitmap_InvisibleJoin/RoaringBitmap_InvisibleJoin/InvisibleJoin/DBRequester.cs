using System.Collections.Generic;
using System.Text;

namespace RoaringBitmap_InvisibleJoin.InvisibleJoin
{
    /// <summary>
    /// Performs filter requests to the data base <see cref="DBModel"/>.
    /// </summary>
    public class DBRequester
    {
        private readonly DBModel db;

        public DBRequester(string dataPath)
        {
            db = new DBModel(dataPath);
        }

        /// <summary>
        /// Applies a filter given as a string.
        /// </summary>
        public void Query(string query)
        {
            string columnName = query.Substring(0, query.IndexOf(' '));
            query = query.Substring(query.IndexOf(' ') + 1);
            string operation = query.Substring(0, query.IndexOf(' '));
            string value = query.Substring(query.IndexOf(' ') + 1);

            // In this case we have a string.
            if (value[0] == '\'')
            {
                // Deleting the first and last '
                value = value.Substring(1, value.Length - 2);
            }

            db.GetColumn(columnName).Filter(operation, value);
        }

        /// <summary>
        /// Accumulates all bitmaps and returns list of lines, where values of 
        /// <paramref name="outputCols"/> are delimeted by '|'.
        /// </summary>
        public List<string> GetResults(string[] outputCols)
        {
            db.AccumulateResults();

            List<List<string>> results = new List<List<string>>();
            // Third phase of invisible join: 
            // getting values from dimensional tables using facts table bitmap.
            foreach (var col in outputCols)
            {
                results.Add(db.GetColumn(col).GetValues());
            }
            List<string> result = new List<string>();
            for (int i = 0; i < results[0].Count; ++i)
            {
                StringBuilder sb = new StringBuilder();
                foreach (var list in results)
                {
                    sb.Append(list[i]).Append('|');
                }
                sb.Remove(sb.Length - 1, 1);
                result.Add(sb.ToString());
            }
            return result;
        }
    }
}
