using System;
using System.IO;

namespace GrahamAlgorithm
{
    class Program
    {
        /// <summary>
        /// Reads points from file. 
        /// Doesn't manage any of possible exceptions!
        /// </summary>
        /// <exception cref="IOException"></exception>
        /// <exception cref="FormatException"></exception>
        /// <exception cref="NullReferenceException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        private static Point[] ReadPointsFromFile(string path)
        {
            using StreamReader sr = new StreamReader(path);
            int size = int.Parse(sr.ReadLine());
            Point[] result = new Point[size];
            for (int i = 0; i < size; i++)
            {
                string[] input = sr.ReadLine().Split();
                result[i] = new Point(int.Parse(input[0]), int.Parse(input[1]));
            }
            return result;
        }

        private static void Main(string[] args)
        {
            if (args.Length != 4)
            {
                Console.WriteLine("Wrong input! You must specify direction, output format, input and output files.");
            }
            else
            {
                string directionArg = args[0];
                string formatArg = args[1];
                GrahamScanner.Direction direction =
                    directionArg switch
                    {
                        "cw" => GrahamScanner.Direction.Clockwise,
                        "cc" => GrahamScanner.Direction.Counterclockwise,
                        _ => throw new ArgumentException($"Invalid direction: {directionArg}")
                    };
                GrahamScanner.OutputFormat format =
                    formatArg switch
                    {
                        "plain" => GrahamScanner.OutputFormat.Plain,
                        "wkt" => GrahamScanner.OutputFormat.WKT,
                        _ => throw new ArgumentException($"Invalid format: {formatArg}")
                    };

                Point[] input;
                try
                {
                    input = ReadPointsFromFile(args[2]);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    return;
                }

                GrahamScanner grahamScanner = new GrahamScanner();
                string result = grahamScanner.CalculateConvexHull(input).GetData(direction, format);

                try
                {
                    File.WriteAllText(args[3], result);
                }
                catch (IOException e)
                {
                    Console.WriteLine(e);
                }
            }
        }
    }
}
