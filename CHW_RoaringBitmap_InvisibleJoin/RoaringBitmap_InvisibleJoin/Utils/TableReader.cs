using System;
using System.IO;

namespace RoaringBitmap_InvisibleJoin.Utils
{
    /// <summary>
    /// Reads table line-by-line.
    /// </summary>
    public static class TableReader
    {
        /// <summary>
        /// Performs an action for each line of the table. 
        /// </summary>
        /// <param name="path"> Path to the table. </param>
        /// <param name="action"> Action which is performed for each line. First argument is line num (from 0), second is the actual line. </param>
        public static void ForEachLine(string path, Action<int, string> action)
        {
            using (StreamReader sr = new StreamReader(path))
            {
                int i = 0;
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    action(i, line);
                    ++i;
                }
            }
        }

        /// <summary>
        /// Performs an action for each line of the table.
        /// </summary>
        /// <param name="path"> Path to the table. </param>
        /// <param name="action"> Action which is performed for each line. First argument is line num (from 0), second is the actual line. </param>
        public static void ForEachSplitedLine(string path, Action<int, string[]> action)
        {
            using (StreamReader sr = new StreamReader(path))
            {
                int i = 0;
                string[] parts;
                while (!sr.EndOfStream)
                {
                    parts = sr.ReadLine().Split('|');
                    action(i, parts);
                    ++i;
                }
            }
        }
    }
}
