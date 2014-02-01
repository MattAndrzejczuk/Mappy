﻿namespace Mappy.Collections
{
    using System;

    public static class GridMethods
    {
        public static int ToIndex<T>(this IGrid<T> grid, int x, int y)
        {
            return (y * grid.Width) + x;
        }

        public static GridCoordinates ToCoords<T>(this IGrid<T> grid, int index)
        {
            return new GridCoordinates(index % grid.Width, index / grid.Width);
        }

        public static void Copy<T>(IGrid<T> src, IGrid<T> dest, int sourceX, int sourceY, int destX, int destY, int width, int height)
        {
            if (width < 0 || height < 0)
            {
                throw new ArgumentException("copy area has negative dimensions");
            }

            if (sourceX < 0 || sourceY < 0 || sourceX + width > src.Width || sourceY + height > src.Height)
            {
                throw new IndexOutOfRangeException("source area overlaps source bounds");
            }

            if (destX < 0 || destY < 0 || destX + width > dest.Width || destY + height > dest.Height)
            {
                throw new IndexOutOfRangeException("destination area overlaps destination bounds");
            }

            for (int dy = 0; dy < height; dy++)
            {
                for (int dx = 0; dx < width; dx++)
                {
                    dest[destX + dx, destY + dy] = src[sourceX + dx, sourceY + dy];
                }
            }
        }

        public static void Copy<T>(IGrid<T> src, IGrid<T> dest, int x, int y)
        {
            Copy(src, dest, 0, 0, x, y, src.Width, src.Height);
        }

        public static void Merge<T>(ISparseGrid<T> src, ISparseGrid<T> dest, int sourceX, int sourceY, int destX, int destY, int width, int height)
        {
            if (width < 0 || height < 0)
            {
                throw new ArgumentException("copy area has negative dimensions");
            }

            if (sourceX < 0 || sourceY < 0 || sourceX + width > src.Width || sourceY + height > src.Height)
            {
                throw new IndexOutOfRangeException("source area overlaps source bounds");
            }

            if (destX < 0 || destY < 0 || destX + width > dest.Width || destY + height > dest.Height)
            {
                throw new IndexOutOfRangeException("destination area overlaps destination bounds");
            }

            foreach (var e in src.CoordinateEntries)
            {
                var sourceCoords = e.Key;
                var value = e.Value;

                if (sourceCoords.X < sourceX
                    || sourceCoords.Y < sourceY
                    || sourceCoords.X >= sourceX + width
                    || sourceCoords.Y >= sourceY + height)
                {
                    continue;
                }

                dest[destX + (sourceCoords.X - sourceX), destY + (sourceCoords.Y - sourceY)] = value;
            }
        }

        public static void Merge<T>(ISparseGrid<T> src, ISparseGrid<T> dest, int x, int y)
        {
            if (x < 0 || y < 0 || x + src.Width > dest.Width || y + src.Height > dest.Height)
            {
                throw new IndexOutOfRangeException("part of the other grid falls outside boundaries");
            }

            foreach (var e in src.CoordinateEntries)
            {
                dest[e.Key.X + x, e.Key.Y + y] = e.Value;
            }
        }
    }
}