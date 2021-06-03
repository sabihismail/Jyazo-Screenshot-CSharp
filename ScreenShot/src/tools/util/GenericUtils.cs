using System;
using System.Linq;

namespace ScreenShot.src.tools
{
    public class GenericUtils
    {
        public static T MinObject<T>(double v1, double v2, T r1, T r2) => MinObject(v1, r1, v2, r2);

        public static T MinObject<T>(double v1, T r1, double v2, T r2)
        {
            if (v1 <= v2)
            {
                return r1;
            }

            return r2;
        }

        public static T MaxObject<T>(double v1, double v2, T r1, T r2) => MaxObject(v1, r1, v2, r2);

        public static T MaxObject<T>(double v1, T r1, double v2, T r2)
        {
            if (v1 >= v2)
            {
                return r1;
            }

            return r2;
        }

        public static T MinObjectList<T>(params Tuple<double?, T>[] input)
        {
            var lst = input.Where(x => x.Item1.HasValue)
                .ToList();

            if (lst.Count == 0) throw new ArgumentException("Elements array cannot be null.");
            if (lst.Count == 1) return lst[0].Item2;

            var current = lst[0];
            for (var i = 1; i < lst.Count; i++)
            {
                var element = lst[i];

                if (element.Item1 < current.Item1)
                {
                    current = element;
                }
            }

            return current.Item2;
        }

        public static T MaxObjectList<T>(params Tuple<double?, T>[] input)
        {
            var lst = input.Where(x => x.Item1.HasValue)
                .ToList();

            if (lst.Count == 0) throw new ArgumentException("Elements array cannot be null.");
            if (lst.Count == 1) return lst[0].Item2;

            var current = lst[0];
            for (var i = 1; i < lst.Count; i++)
            {
                var element = lst[i];

                if (element.Item1 > current.Item1)
                {
                    current = element;
                }
            }

            return current.Item2;
        }
    }
}
