using System;
using System.Diagnostics;
using System.IO;

namespace RoaringBitmap_InvisibleJoin.Utils
{
    /// <summary>
    /// For tests.
    /// </summary>
    public class Tester
    {
        private readonly string testsPath;
        private readonly string testName;
        private readonly string answersPath;
        private readonly string answerName;
        private readonly string myAnswersPath;
        private readonly string myAnswerName;

        public Tester(string testsPath = "input", string testName = "test",
            string answersPath = "output", string answerName = "answer",
            string myAnswersPath = "myOutput", string myAnswerName = "myAnswer")
        {
            this.testsPath = testsPath;
            this.testName = testName;
            this.answersPath = answersPath;
            this.answerName = answerName;
            this.myAnswersPath = myAnswersPath;
            this.myAnswerName = myAnswerName;
        }

        public void Test(int from, int to)
        {
            for (int i = from; i <= 5; ++i) Test(i);
            Console.ResetColor();
        }

        public void Test(int num)
        {
            string testPath = Path.Combine(testsPath, $"{testName}{num}.txt");
            string answerPath = Path.Combine(answersPath, $"{answerName}{num}.txt");
            string myAnswerPath = Path.Combine(myAnswersPath, $"{myAnswerName}{num}.txt");

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Starting test {testPath}...");
            Console.ResetColor();

            Program.Run("data", testPath, myAnswerPath);

            using (StreamReader myAnswer = new StreamReader(myAnswerPath))
            using (StreamReader answer = new StreamReader(answerPath))
            {
                string myLine = null, answerLine = null;
                int lineNum = 1;
                while (!(myAnswer.EndOfStream && answer.EndOfStream))
                {
                    myLine = myAnswer.ReadLine();
                    answerLine = answer.ReadLine();
                    Console.ResetColor();
                    Console.Write($"Line {lineNum}: ");
                    if ((myLine == null) ^ (answerLine == null))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[-] Line number doesn't match!");
                        break;
                    }
                    else if (myLine != answerLine)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[-] Answers don't match at line {lineNum}: " +
                            $"expected {answerLine} but got {myLine}");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[+] Match");
                    }

                    ++lineNum;
                }
            }
            Console.WriteLine("End of test.");
        }

        public void TestTime(int from, int to)
        {
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            for (int i = from; i <= to; ++i)
            {
                string testPath = Path.Combine(testsPath, $"test{i}.txt");
                string myAnswerPath = Path.Combine(myAnswersPath, $"myAnswer{i}.txt");
                Program.Run("data", testPath, myAnswerPath);
            }
            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds);
        }
    }
}
