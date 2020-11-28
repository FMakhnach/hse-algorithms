using System;
using System.Text;

namespace ManacherAlgorithm
{
    // It was not required, made just for self-check.
    public class Tester
    {
        private readonly int minStrLen;
        private readonly int maxStrLen;
        private readonly string lettersPool;
        private readonly Random random;

        public Tester(int minLen = 1, int maxLen = 1000, string lettersPool = "abcdefg")
        {
            minStrLen = minLen;
            maxStrLen = maxLen;
            this.lettersPool = lettersPool;
            random = new Random();
        }

        public void Test(int numOfTests)
        {
            var dumb = new DumbProcessor();
            ManacherProcessor processor = new ManacherProcessor();

            for (int i = 0; i < numOfTests; i++)
            {
                string input = GenerateString();
                string expected = dumb.GetNumOfPalindromes(input).ToString();
                string result = processor.GetNumOfPalindromes(input).ToString();
                if (result == expected)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[+] Test {i} passed successfully!");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[!] Test \"{input}\" failed! Expected \"{expected}\", got \"{result}\".");
                }
                Console.ResetColor();
            }
        }

        private string GenerateString()
        {
            int length = random.Next(minStrLen, maxStrLen + 1);
            StringBuilder sb = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                sb.Append(lettersPool[random.Next(lettersPool.Length)]);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Straightforward solution for testing purposes.
        /// </summary>
        public class DumbProcessor
        {
            private string line;

            public bool IsPalindrome(int from, int to)
            {
                int middle = (from + to) / 2;
                for (int i = from; i <= middle; ++i)
                {
                    if (line[i] != line[to + from - i])
                    {
                        return false;
                    }
                }
                return true;
            }

            public ManacherProcessor.Result GetNumOfPalindromes(string line)
            {
                this.line = line;
                long even = 0, odd = 0;
                for (int i = 0; i < line.Length; ++i)
                {
                    for (int j = i; j < line.Length; ++j)
                    {
                        if (IsPalindrome(i, j))
                        {
                            // For ex. if i == j it is just 1 char => odd.
                            if ((j - i) % 2 == 0)
                            {
                                ++odd;
                            }
                            else
                            {
                                ++even;
                            }
                        }
                    }
                }
                return new ManacherProcessor.Result(even, odd);
            }
        }
    }
}
