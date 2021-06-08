using System;
using System.Linq;
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global

namespace ScreenShot.src.tools.util
{
    public static class GenericUtils
    {
        public static T MinObject<T>(double v1, double v2, T r1, T r2) => MinObject(v1, r1, v2, r2);

        private static T MinObject<T>(double v1, T r1, double v2, T r2)
        {
            return v1 <= v2 ? r1 : r2;
        }

        public static T MaxObject<T>(double v1, double v2, T r1, T r2) => MaxObject(v1, r1, v2, r2);

        private static T MaxObject<T>(double v1, T r1, double v2, T r2)
        {
            return v1 >= v2 ? r1 : r2;
        }

        public static T MinObjectList<T>(params Tuple<double?, T>[] input)
        {
            var lst = input.Where(x => x.Item1.HasValue)
                .ToList();

            switch (lst.Count)
            {
                case 0:
                    throw new ArgumentException("Elements array cannot be null.");
                
                case 1:
                    return lst[0].Item2;
            }

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

            switch (lst.Count)
            {
                case 0:
                    throw new ArgumentException("Elements array cannot be null.");
                
                case 1:
                    return lst[0].Item2;
            }

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
