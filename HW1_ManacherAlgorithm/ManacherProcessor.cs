using System;

namespace ManacherAlgorithm
{
    public class ManacherProcessor
    {
        private string line;

        public Result GetNumOfPalindromes(string line)
        {
            this.line = line;
            return new Result(CountEven(), CountOdd());
        }

        /// <summary>
        /// Counts the amount of palindromes of even length in the string.
        /// </summary>
        private long CountEven()
        {
            // Tracking "the most right" palindrome we've found.
            int left = 0, right = -1;
            long even = 0;
            // d_Even[i] contains num of palindromes with left center at i and even length. 
            int[] d_Even = new int[line.Length];
            for (int center = 0; center < line.Length; ++center)
            {
                // Num of palindromes with center at i.
                int numOfPalindromes = 0;
                if (center <= right)
                {
                    // If we are inside the last palindrome, we can skip some chars.
                    numOfPalindromes = Math.Min(right - center + 1, d_Even[left + right - center + 1]);
                }
                // Trying to add 1 char to the right and checking whether we get a palindrome.
                // Checking bounds & palindromeness.
                while (center + numOfPalindromes < line.Length
                    && center - numOfPalindromes - 1 >= 0
                    && line[center + numOfPalindromes] == line[center - numOfPalindromes - 1])
                {
                    ++numOfPalindromes;
                }
                d_Even[center] = numOfPalindromes;
                even += numOfPalindromes;
                if (center + numOfPalindromes - 1 > right)
                {
                    // If we got out of prev bounds, we say that we found a new righter palindrome.
                    left = center - numOfPalindromes;
                    right = center + numOfPalindromes - 1;
                }
            }
            return even;
        }

        /// <summary>
        /// Counts the amount of palindromes of odd length in the string.
        /// </summary>
        private long CountOdd()
        {
            // Tracking "the most right" palindrome we've found.
            int left = 0, right = -1;
            long odd = 0;
            // d_Odd[i] contains num of palindromes with center at i and odd length. 
            int[] d_Odd = new int[line.Length];
            for (int center = 0; center < line.Length; ++center)
            {
                // Num of palindromes with given center.
                // One symbol is a palindrome => initial is 1.
                int numOfPalindromes = 1;
                if (center <= right)
                {
                    // If we are inside the last palindrome, we can skip some chars.
                    numOfPalindromes = Math.Min(right - center + 1, d_Odd[right - center + left]);
                }
                // Trying to add 1 char to the right and checking whether we get a palindrome.
                // Checking bounds & palindromeness.
                while (IsStillPalindrome(line, center, numOfPalindromes))
                {
                    ++numOfPalindromes;
                }
                d_Odd[center] = numOfPalindromes;
                odd += numOfPalindromes;
                if (center + numOfPalindromes - 1 > right)
                {
                    // If we got out of prev bounds, we say that we found a new righter palindrome.
                    left = center - numOfPalindromes + 1;
                    right = center + numOfPalindromes - 1;
                }
            }
            return odd;
        }

        private static bool IsStillPalindrome(string line, int center, int offset)
                                => center + offset < line.Length
                                && center - offset >= 0
                                && line[center + offset] == line[center - offset];

        public readonly struct Result
        {
            public long Total { get; }
            public long Even { get; }
            public long Odd { get; }

            public Result(long even, long odd)
            {
                Total = even + odd;
                Even = even;
                Odd = odd;
            }

            public override string ToString() => $"{Total} {Even} {Odd}";
        }
    }
}
