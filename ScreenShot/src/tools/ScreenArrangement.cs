using System;
using System.Windows;

namespace ScreenShot.src.tools
{
    public class ScreenGraph
    {
        public WPFScreen Current { get; set; }

        public ScreenGraph Left { get; set; }

        public ScreenGraph Top { get; set; }

        public ScreenGraph Right { get; set; }

        public ScreenGraph Bottom { get; set; }

        public static ScreenGraph Generate()
        {
            var (primary, others) = WPFScreen.AllScreensSeparated();

            var graph = new ScreenGraph
            {
                Current = primary,
            };

            foreach (var other in others)
            {
                var newGraph = new ScreenGraph
                {
                    Current = other,
                };

                AddWindow(graph, newGraph);
            }

            return graph;
        }

        public ScreenGraph GetClosest(Point point)
        {
            var axis = GenericUtils.MinObject(point.X, Axis.X, point.Y, Axis.Y);

            var left = GetDistanceFromPointToWindow(point, Left, axis);
            var top = GetDistanceFromPointToWindow(point, Top, axis);
            var right = GetDistanceFromPointToWindow(point, Right, axis);
            var bottom = GetDistanceFromPointToWindow(point, Bottom, axis);

            var newGraph = GenericUtils.MinObjectList(
                Tuple.Create(left, Left),
                Tuple.Create(top, Top),
                Tuple.Create(right, Right),
                Tuple.Create(bottom, Bottom)
            );

            return newGraph;
        }
        
        private double? GetDistanceFromPointToWindow(Point point, ScreenGraph window, Axis axis)
        {
            if (window == null) return null;

            var rect = window.Current.DeviceBounds;

            if (axis == Axis.X)
            {
                return Math.Min(Math.Abs(rect.Left - point.X), Math.Abs(rect.Right - point.X));
            }
            else if (axis == Axis.Y)
            {
                return Math.Min(Math.Abs(rect.Top - point.Y), Math.Abs(rect.Bottom - point.Y));
            }

            throw new ArgumentException("Should't ever get here but axis was not implemented: " + axis.ToString());
        }

        private static void AddWindow(ScreenGraph primaryScreen, ScreenGraph newScreen)
        {
            var primaryBounds = primaryScreen.Current.DeviceBounds;
            var newBounds = newScreen.Current.DeviceBounds;

            if (primaryBounds.Left > newBounds.Left)
            {
                if (primaryScreen.Left != null)
                {
                    AddWindow(primaryScreen.Left, newScreen);
                }
                else
                {
                    primaryScreen.Left = newScreen;
                    newScreen.Right = primaryScreen;
                }
            } 
            else if (primaryBounds.Right < newBounds.Right)
            {
                if (primaryScreen.Right != null)
                {
                    AddWindow(primaryScreen.Right, newScreen);
                }
                else
                {
                    primaryScreen.Right = newScreen;
                    newScreen.Left = primaryScreen;
                }
            }
            else if (primaryBounds.Top < newBounds.Top)
            {
                if (primaryScreen.Top != null)
                {
                    AddWindow(primaryScreen.Top, newScreen);
                }
                else
                {
                    primaryScreen.Top = newScreen;
                    newScreen.Bottom = primaryScreen;
                }
            }
            else if (primaryBounds.Bottom > newBounds.Bottom)
            {
                if (primaryScreen.Bottom != null)
                {
                    AddWindow(primaryScreen.Bottom, newScreen);
                }
                else
                {
                    primaryScreen.Bottom = newScreen;
                    newScreen.Top = primaryScreen;
                }
            }
        }

        private enum Axis
        {
            X,
            Y,
        }
    }
}
