using System;
using System.Linq;
using System.Text;

namespace GrahamAlgorithm
{
    public class GrahamScanner
    {
        private Stack<Point> convexHull;
        private Point[] initialInput;

        /// <summary>
        /// Calculates the convex hull. To get result use <see cref="GetData(Direction, OutputFormat)"/>.
        /// </summary>
        public GrahamScanner CalculateConvexHull(Point[] points)
        {
            convexHull = new Stack<Point>(points.Length);
            if (points.Length == 0)
                return this;

            SaveInput(points);

            Point start = FindStartingPoint(points);
            convexHull.Push(start);
            if (points.Length == 1)
                return this;

            points = SortByPolarAngle(points, start);

            // The actual Graham algorithm.
            convexHull.Push(points[1]);
            for (int i = 2; i < points.Length; ++i)
            {
                PopWhileRightTurn(points[i]);
                convexHull.Push(points[i]);
            }
            // Connecting with the first point.
            PopWhileRightTurn(points[0]);

            return this;
        }

        /// <summary>
        /// Returns data as a string in a given format. 
        /// Throws error if the hull hasn't been calculated yet.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public string GetData(Direction direction, OutputFormat format)
        {
            if (convexHull == null)
                throw new InvalidOperationException("Convex hull must be calculated first!");

            Point[] hullArray = convexHull.ToArray();
            hullArray = ChangeArrayDueToDirection(hullArray, direction);

            switch (format)
            {
                case OutputFormat.Plain:
                    return GetPlainFormat(hullArray);
                case OutputFormat.WKT:
                    return GetWKTFormat(hullArray);
                default:
                    throw new ArgumentException($"Format {format} is not supported!");
            }
        }


        private static Point[] ChangeArrayDueToDirection(Point[] hullArray, Direction direction)
        {
            switch (direction)
            {
                case Direction.Counterclockwise:
                    Array.Reverse(hullArray);
                    break;
                case Direction.Clockwise:
                    // In this case we have right order initially, 
                    // but the start element is in the end.
                    Point[] move = new Point[hullArray.Length];
                    for (int i = 1; i < hullArray.Length; ++i)
                    {
                        move[i] = hullArray[i - 1];
                    }
                    move[0] = hullArray[hullArray.Length - 1];
                    hullArray = move;
                    break;
                default:
                    throw new ArgumentException($"Direction {direction} is not supported!");
            }

            return hullArray;
        }

        /// <summary>
        /// Finds the starting point, which is left-est bottom-est one.
        /// </summary>
        private static Point FindStartingPoint(Point[] points)
        {
            Point start = points[0];
            foreach (var p in points)
            {
                if (p.Y < start.Y || (p.Y == start.Y && p.X < start.X))
                {
                    start = p;
                }
            }

            return start;
        }

        /// <summary>
        /// Checks whether the given point is making a "turn to the right" in respect to 
        /// the direction which is made by the last 2 points in the hull.
        /// The check is performed using cross product of the vectors.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        private bool IsRightTurn(Point point)
        {
            return (convexHull.Top().X - convexHull.NextToTop().X) * (point.Y - convexHull.Top().Y) -
                   (convexHull.Top().Y - convexHull.NextToTop().Y) * (point.X - convexHull.Top().X) <= 0;
        }

        /// <summary>
        /// Returns convex hull data in Plain format.
        /// </summary>
        private string GetPlainFormat(Point[] points)
            => new StringBuilder()
               .AppendLine(points.Length.ToString())
               .AppendJoin(Environment.NewLine, points)
               .ToString();

        /// <summary>
        /// Returns convex hull data in WKT format.
        /// </summary>
        private string GetWKTFormat(Point[] points)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("MULTIPOINT ((")
              .AppendJoin("), (", initialInput)
              .AppendLine("))");

            sb.Append("POLYGON ((")
              .AppendJoin(", ", points)
              .Append($", {points[0]}))");

            return sb.ToString();
        }

        /// <summary>
        /// Pops top elements while the given point is making a "turn to the right" in respect to 
        /// the direction which is made by the last 2 points in the hull.
        /// </summary>
        private void PopWhileRightTurn(Point point)
        {
            while (convexHull.Size > 1 && IsRightTurn(point))
            {
                convexHull.Pop();
            }
        }

        private void SaveInput(Point[] points)
        {
            initialInput = new Point[points.Length];
            for (int i = 0; i < points.Length; ++i)
            {
                initialInput[i] = points[i];
            }
        }

        private Point[] SortByPolarAngle(Point[] points, Point start)
                    => points.OrderBy(x => Math.Round(-PolarCos(x, start), 6))
                             .ThenBy(x => Point.SquareDistance(start, x))
                             .ToArray();

        /// <summary>
        /// Calculates cosine of polar angle of <paramref name="p"/> relative to <paramref name="start"/>.
        /// </summary>
        private static double PolarCos(Point p, Point start)
        {
            // If directly above start (or even the start itself).
            if (p.X == start.X)
                return p.Y == start.Y ? 2 : 0;

            double hyp = Math.Sqrt(Point.SquareDistance(start, p));
            double cat = p.X - start.X;
            return cat / hyp;
        }


        public enum OutputFormat
        {
            Plain, WKT
        }

        public enum Direction
        {
            Counterclockwise, Clockwise
        }
    }
}
