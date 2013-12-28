﻿namespace Mappy.Palette
{
    using System.Drawing;

    public class CompositePalette : IPalette, IReversePalette
    {
        public CompositePalette(IPalette forward, IReversePalette reverse)
        {
            this.ForwardPalette = forward;
            this.ReversePalette = reverse;
        }

        public IPalette ForwardPalette { get; private set; }

        public IReversePalette ReversePalette { get; private set; }

        Color IPalette.this[int index]
        {
            get
            {
                return this.ForwardPalette[index];
            }
        }

        int IReversePalette.this[Color color]
        {
            get
            {
                return this.ReversePalette[color];
            }
        }
    }
}