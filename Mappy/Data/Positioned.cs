namespace Mappy.Data
{
    using System;
    using System.Drawing;

    /// <summary>
    /// Wrapper class providing position information on top of the wrapped type.
    /// </summary>
    public class Positioned<T>
    {
        private Point location;

        public Positioned(T item)
            : this(item, Point.Empty)
        {
        }

        public Positioned(T item, Point location)
        {
            this.Item = item;
            this.Location = location;
        }

        public event EventHandler LocationChanged;

        public Point Location
        {
            get
            {
                return this.location;
            }

            set
            {
                this.location = value;
                this.OnLocationChanged();
            }
        }

        public T Item { get; private set; }

        private void OnLocationChanged()
        {
            this.LocationChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
