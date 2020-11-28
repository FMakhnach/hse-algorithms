using RoaringBitmap_InvisibleJoin.InvisibleJoin;
using RoaringBitmap_InvisibleJoin.Utils;
using System;
using System.Collections.Generic;
using System.IO;

namespace RoaringBitmap_InvisibleJoin
{
    class Program
    {
        private static void Main(string[] args)
        {
            try
            {
                //new Tester().Test(1, 5);
                Run(args);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public static void Run(params string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("You must specify 1) path to data; 2) path to input file; 3) path to output file.");
                return;
            }
            DBRequester db = new DBRequester(args[0]);
            string[] outputCols;
            using (StreamReader input = new StreamReader(args[1]))
            {
                // The columns that should be in the result.
                outputCols = input.ReadLine().Split(',');
                int n = int.Parse(input.ReadLine());

                // Processing each query.
                for (int i = 0; i < n; ++i)
                    db.Query(input.ReadLine());
            }
            // Collecting results.
            List<string> rows = db.GetResults(outputCols);
            using (StreamWriter output = new StreamWriter(args[2]))
            {
                foreach (var row in rows)
                {
                    output.Write(row);
                    // Idk why, whatever.
                    output.Write('\n');
                }
            }
        }
    }
}
