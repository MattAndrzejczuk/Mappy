﻿namespace Geometry
{
    using System;

    public struct AxisRectangle3D
    {
        public AxisRectangle3D(Vector3D position, double xExtents, double yExtents)
            : this()
        {
            this.Position = position;
            this.Extents = new Vector3D(xExtents, yExtents, 0.0);
        }

        public Vector3D Position { get; set; }

        /// <summary>
        /// Z-axis value is ignored
        /// </summary>
        public Vector3D Extents { get; set; }

        public Vector3D TopLeft
        {
            get
            {
                return new Vector3D(
                    this.Position.X - this.Extents.X,
                    this.Position.Y - this.Extents.Y,
                    this.Position.Z);
            }
        }

        public Vector3D TopRight
        {
            get
            {
                return new Vector3D(
                    this.Position.X + this.Extents.X,
                    this.Position.Y - this.Extents.Y,
                    this.Position.Z);
            }
        }

        public Vector3D BottomLeft
        {
            get
            {
                return new Vector3D(
                    this.Position.X - this.Extents.X,
                    this.Position.Y + this.Extents.Y,
                    this.Position.Z);
            }
        }

        public Vector3D BottomRight
        {
            get
            {
                return new Vector3D(
                    this.Position.X + this.Extents.X,
                    this.Position.Y + this.Extents.Y,
                    this.Position.Z);
            }
        }

        public static AxisRectangle3D FromTLBR(double t, double l, double b, double r)
        {
            double centreX = (l + r) / 2.0;
            double centreY = (t + b) / 2.0;

            double extentsX = Math.Abs((r - l) / 2.0);
            double extentsY = Math.Abs((t - b) / 2.0);

            var centreVector = new Vector3D(centreX, centreY, 0.0);

            return new AxisRectangle3D(centreVector, extentsX, extentsY);
        }

        public Vector3D Normal()
        {
            return Vector3D.ZAxis;
        }

        public Plane3D Plane()
        {
            return new Plane3D(this.Position, this.Normal());
        }

        public bool Intersect(Ray3D ray, out double distance)
        {
            distance = this.Plane().Intersect(ray);

            Vector3D relativePoint = ray.PointAt(distance) - this.Position;

            return Math.Abs(relativePoint.X) <= this.Extents.X
                    && Math.Abs(relativePoint.Y) <= this.Extents.Y;
        }
    }
}
