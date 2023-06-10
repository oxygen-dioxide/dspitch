

namespace DsPitch
{
    public enum PitchPointShape {
        /// <summary>
        /// SineInOut
        /// </summary>
        io,
        /// <summary>
        /// Linear
        /// </summary>
        l,
        /// <summary>
        /// SineIn
        /// </summary>
        i,
        /// <summary>
        /// SineOut
        /// </summary>
        o
    };

    public struct Point{
            public int X;
            public double Y;
            public PitchPointShape shape;
            public Point(int X, double Y, PitchPointShape shape = PitchPointShape.l) {
                this.X = X;
                this.Y = Y;
                this.shape = shape;
            }

            public Point ChangeShape(PitchPointShape shape) {
                return new Point(X, Y, shape);
            }
        }

    public class PitchUtil
    {
        public static double SinEasingInOut(double x0, double x1, double y0, double y1, double x) {
            return y0 + (y1 - y0) * (1 - Math.Cos((x - x0) / (x1 - x0) * Math.PI)) / 2;
        }

        public static double SinEasingInOutX(double x0, double x1, double y0, double y1, double y) {
            return Math.Acos(1 - (y - y0) * 2 / (y1 - y0)) / Math.PI * (x1 - x0) + x0;
        }

        public static double SinEasingIn(double x0, double x1, double y0, double y1, double x) {
            return y0 + (y1 - y0) * (1 - Math.Cos((x - x0) / (x1 - x0) * Math.PI / 2));
        }

        public static double SinEasingInX(double x0, double x1, double y0, double y1, double y) {
            return Math.Acos(1 - (y - y0) / (y1 - y0)) / Math.PI * 2 * (x1 - x0) + x0;
        }

        public static double SinEasingOut(double x0, double x1, double y0, double y1, double x) {
            return y0 + (y1 - y0) * Math.Sin((x - x0) / (x1 - x0) * Math.PI / 2);
        }

        public static double SinEasingOutX(double x0, double x1, double y0, double y1, double y) {
            return Math.Asin((y - y0) / (y1 - y0)) / Math.PI * 2 * (x1 - x0) + x0;
        }

        public static double Linear(double x0, double x1, double y0, double y1, double x) {
            return y0 + (y1 - y0) * (x - x0) / (x1 - x0);
        }

        public static double LinearX(double x0, double x1, double y0, double y1, double y) {
            return (y - y0) / (y1 - y0) * (x1 - x0) + x0;
        }

        public static string ToUtauPBM(PitchPointShape shape)
        {
            switch (shape)
            {
                case PitchPointShape.o:
                    return "r";
                case PitchPointShape.l:
                    return "s";
                case PitchPointShape.i:
                    return "j";
                case PitchPointShape.io:
                    return "";
                default:
                    return "s";
            }
        }

        public static double InterpolateShape(double x0, double x1, double y0, double y1, double x, PitchPointShape shape) {
            switch (shape) {
                case PitchPointShape.io: return SinEasingInOut(x0, x1, y0, y1, x);
                case PitchPointShape.i: return SinEasingIn(x0, x1, y0, y1, x);
                case PitchPointShape.o: return SinEasingOut(x0, x1, y0, y1, x);
                default: return Linear(x0, x1, y0, y1, x);
            }
        }

        static double deltaY(Point pt, Point lineStart, Point lineEnd, PitchPointShape shape){
            return pt.Y - InterpolateShape(lineStart.X, lineEnd.X, lineStart.Y, lineEnd.Y, pt.X, shape);
        }

        static PitchPointShape DetermineShape(Point start, Point middle, Point end){
            if(start.Y==end.Y){
                return PitchPointShape.l;
            }
            var k = (middle.Y-start.Y)/(end.Y-start.Y);
            if(k > 0.67){
                return PitchPointShape.o;
            }
            if(k < 0.33){
                return PitchPointShape.i;
            }
            return PitchPointShape.l;
        }

        //reference: https://github.com/sdercolin/utaformatix3/blob/0f026f7024386ca8362972043c3471c6f2ac9859/src/main/kotlin/process/RdpSimplification.kt#L43
        /*
        * The Ramer–Douglas–Peucker algorithm is a line simplification algorithm
        * for reducing the number of points used to define its shape.
        *
        * Wikipedia: https://en.wikipedia.org/wiki/Ramer%E2%80%93Douglas%E2%80%93Peucker_algorithm
        * Implementation reference: https://rosettacode.org/wiki/Ramer-Douglas-Peucker_line_simplification
        * */
        //perpendicularDistance is replaced with deltaY, because the units of X and Y are different. 
        //result doesn't contain the last point to enhance performance in recursion
        public static List<Point> simplifyShape(List<Point> pointList, Double epsilon) {
            if (pointList.Count <= 2) {
                return pointList;
            }
            
            // Determine line shape
            var middlePoint = pointList[pointList.Count / 2];
            var startPoint = pointList[0];
            var endPoint = pointList[^1];
            var shape = DetermineShape(startPoint, middlePoint, endPoint);
            
            // Find the point with the maximum distance from line between start and end
            var dmax = 0.0;
            var index = 0;
            var end = pointList.Count - 1;
            for (var i = 1; i < end; i++) {
                var d = Math.Abs(deltaY(pointList[i], pointList[0], pointList[end], shape));
                if (d > dmax) {
                    index = i;
                    dmax = d;
                }
            }
            // If max distance is greater than epsilon, recursively simplify
            List<Point> results = new List<Point>();
            if (dmax > epsilon) {
                // Recursive call
                var recResults1 = simplifyShape(pointList.GetRange(0, index + 1), epsilon);
                var recResults2 = simplifyShape(pointList.GetRange(index, pointList.Count - index), epsilon);

                // Build the result list
                results.AddRange(recResults1);
                results.AddRange(recResults2);
                if (results.Count < 2) {
                    throw new Exception("Problem assembling output");
                }
            } else {
                //Just return the start point
                results.Add(pointList[0].ChangeShape(shape));
            }
            return results;
        }

        public static int LastIndexOfMin<T>(IList<T> self, Func<T, double> selector, int startIndex, int endIndex)
        {
            if (self == null) {
                throw new ArgumentNullException("self");
            }

            if (self.Count == 0) {
                throw new ArgumentException("List is empty.", "self");
            }

            var min = selector(self[endIndex-1]);
            int minIndex = endIndex - 1;

            for (int i = endIndex - 1; i >= startIndex; --i) {
                var value = selector(self[i]);
                if (value < min) {
                    min = value;
                    minIndex = i;
                }
            }

            return minIndex;
        }

        
    }
}