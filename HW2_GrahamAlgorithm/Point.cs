using System;
using System.Collections.Generic;
using System.Text;

namespace GrahamAlgorithm
{
    public readonly struct Point
    {
        public int X { get; }
        public int Y { get; }

        public Point(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// Returns the squared distance between two points.
        /// </summary>
        public static long SquareDistance(Point p1, Point p2)
            => (p1.X - p2.X) * (p1.X - p2.X) + (p1.Y - p2.Y) * (p1.Y - p2.Y);

        public override string ToString() => $"{X} {Y}";
    }
}
