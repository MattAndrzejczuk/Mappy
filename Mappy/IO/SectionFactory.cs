﻿namespace Mappy.IO
{
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;

    using Mappy.Collections;
    using Mappy.Data;
    using Mappy.Services;

    using TAUtil;
    using TAUtil.Gdi.Bitmap;
    using TAUtil.Sct;

    /// <summary>
    /// Provides methods for creating tiles and sections
    /// from SCT sources.
    /// </summary>
    public class SectionFactory
    {
        private readonly BitmapCache tileCache;

        public SectionFactory(BitmapCache tileCache)
        {
            this.tileCache = tileCache;
        }

        public static Bitmap MinimapFromSct(ISctSource sct)
        {
            return MinimapToBitmap(sct.GetMinimap());
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

        private static Bitmap MinimapToBitmap(byte[] minimap)
        {
            return BitmapConvert.ToBitmap(
                minimap,
                SctReader.MinimapWidth,
                SctReader.MinimapHeight);
        }

        private Bitmap TileToBitmap(byte[] tile)
        {
            Bitmap bmp = BitmapConvert.ToBitmap(
                tile,
                MapConstants.TileWidth,
                MapConstants.TileHeight);

            return this.tileCache.GetOrAddBitmap(bmp);
        }
    }
}
