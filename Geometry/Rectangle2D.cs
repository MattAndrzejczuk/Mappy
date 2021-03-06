﻿namespace Geometry
{
    using System;

    public struct Rectangle2D
    {
        public static readonly Rectangle2D Empty = new Rectangle2D();

        public Rectangle2D(Vector2D center, Vector2D extents)
            : this()
        {
            this.Center = center;
            this.Extents = extents;
        }

        public Vector2D Center { get; set; }

        public Vector2D Extents { get; set; }

        public double ExtentX
        {
            get
            {
                return this.Extents.X;
            }

            set
            {
                this.Extents = new Vector2D(value, this.ExtentY);
            }
        }

        public double ExtentY
        {
            get
            {
                return this.Extents.Y;
            }

            set
            {
                this.Extents = new Vector2D(this.ExtentX, value);
            }
        }

        public Vector2D Size
        {
            get
            {
                return this.Extents * 2.0;
            }
        }

        public double Width
        {
            get
            {
                return this.Extents.X * 2.0;
            }
        }

        public double Height
        {
            get
            {
                return this.Extents.Y * 2.0;
            }
        }

        public double CenterX
        {
            get
            {
                return this.Center.X;
            }

            set
            {
                this.Center = new Vector2D(value, this.CenterY);
            }
        }

        public double CenterY
        {
            get
            {
                return this.Center.Y;
            }

            set
            {
                this.Center = new Vector2D(this.CenterX, value);
            }
        }

        public double MinX
        {
            get
            {
                return this.Center.X - this.Extents.X;
            }
        }

        public double MinY
        {
            get
            {
                return this.Center.Y - this.Extents.Y;
            }
        }

        public double MaxX
        {
            get
            {
                return this.Center.Y + this.Extents.Y;
            }
        }

        public double MaxY
        {
            get
            {
                return this.Center.X + this.Extents.X;
            }
        }

        public Vector2D MinXY
        {
            get
            {
                return this.Center - this.Extents;
            }
        }

        public Vector2D MaxXY
        {
            get
            {
                return this.Center + this.Extents;
            }
        }

        public static Rectangle2D FromCorner(Vector2D position, Vector2D size)
        {
            var halfSize = size / 2.0;
            return new Rectangle2D(position + halfSize, halfSize);
        }

        public static Rectangle2D FromCorner(
            double x,
            double y,
            double width,
            double height)
        {
            return FromCorner(new Vector2D(x, y), new Vector2D(width, height));
        }

        public static Rectangle2D FromMinMax(
            double minX,
            double minY,
            double maxX,
            double maxY)
        {
            double centerX = (minX + maxX) / 2.0;
            double centerY = (minY + maxY) / 2.0;
            double halfWidth = Math.Abs((maxX - minX) / 2.0);
            double halfHeight = Math.Abs((maxY - minY) / 2.0);

            return new Rectangle2D(
                new Vector2D(centerX, centerY),
                new Vector2D(halfWidth, halfHeight));
        }
    }
}
