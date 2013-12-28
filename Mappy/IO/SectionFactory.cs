﻿namespace Mappy.IO
{
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;

    using Mappy.Data;
    using Mappy.Palette;

    using TAUtil;
    using TAUtil.Sct;

    public class SectionFactory
    {
        private readonly BitmapDeserializer bitmapDeserializer;

        public SectionFactory(IPalette palette)
        {
            this.bitmapDeserializer = new BitmapDeserializer(palette);
        }

        public MapTile TileFromSct(ISctSource sct)
        {
            MapTile tile = new MapTile(sct.DataWidth, sct.DataHeight);

            List<Bitmap> tiles = new List<Bitmap>(sct.TileCount);
            tiles.AddRange(sct.EnumerateTiles().Select(this.TileToBitmap));

            ReadData(sct, tile, tiles);

            ReadHeights(sct, tile);

            return tile;
        }

        public Bitmap MinimapFromSct(ISctSource sct)
        {
            return this.MinimapToBitmap(sct.GetMinimap());
        }

        private static void ReadHeights(ISctSource sct, MapTile tile)
        {
            var enumer = sct.EnumerateAttrs().GetEnumerator();
            for (int y = 0; y < sct.DataHeight * 2; y++)
            {
                for (int x = 0; x < sct.DataWidth * 2; x++)
                {
                    enumer.MoveNext();
                    tile.HeightGrid.Set(x, y, enumer.Current.Height);
                }
            }
        }

        private static void ReadData(ISctSource sct, MapTile tile, List<Bitmap> tiles)
        {
            var enumer = sct.EnumerateData().GetEnumerator();
            for (int y = 0; y < sct.DataHeight; y++)
            {
                for (int x = 0; x < sct.DataWidth; x++)
                {
                    enumer.MoveNext();
                    tile.TileGrid.Set(x, y, tiles[enumer.Current]);
                }
            }
        }

        private Bitmap MinimapToBitmap(byte[] minimap)
        {
            return this.bitmapDeserializer.Deserialize(
                minimap,
                SctReader.MinimapWidth,
                SctReader.MinimapHeight);
        }

        private Bitmap TileToBitmap(byte[] tile)
        {
            Bitmap bmp = this.bitmapDeserializer.Deserialize(
                tile,
                MapConstants.TileWidth,
                MapConstants.TileHeight);

            return Globals.TileCache.GetOrAddBitmap(bmp);
        }
    }
}