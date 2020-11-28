using System;
using System.IO;

namespace ManacherAlgorithm
{
    class Program
    {
        private static void Main(string[] args)
        {
            // new Tester().Test(10000);

            if (args.Length != 2)
            {
                Console.WriteLine("Please, pass input and output files as parameters!");
                return;
            }
            string inputPath = args[0];
            string outputPath = args[1];
            try
            {
                // Only one line, ..AllText must be fine.
                string input = File.ReadAllText(inputPath);
                var result = new ManacherProcessor().GetNumOfPalindromes(input);
                File.WriteAllText(outputPath, result.ToString());
            }
            catch (IOException e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}
