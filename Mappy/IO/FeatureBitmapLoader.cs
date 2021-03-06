﻿namespace Mappy.IO
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;

    using Mappy.Data;
    using Mappy.IO.Gaf;
    using Mappy.Util;

    using TAUtil.Gaf;
    using TAUtil.Gdi.Bitmap;
    using TAUtil.Hpi;

    public class FeatureBitmapLoader : AbstractHpiLoader<KeyValuePair<string, OffsetBitmap>>
    {
        private readonly IDictionary<string, IList<FeatureRecord>> filenameFeatureMap;

        public FeatureBitmapLoader(IDictionary<string, IList<FeatureRecord>> filenameFeatureMap)
        {
            this.filenameFeatureMap = filenameFeatureMap;
        }

        protected override IEnumerable<HpiEntry> EnumerateFiles(HpiReader r)
        {
            return r.GetFilesRecursive("anims").Where(this.IsNeededFile);
        }

        protected override void LoadFile(HpiEntry file)
        {
            // extract and read the file
            var adapter = new GafEntryArrayAdapter();
            using (var b = new GafReader(file.Open(), adapter))
            {
                b.Read();
            }

            GafEntry[] gaf = adapter.Entries;

            var records = this.filenameFeatureMap[HpiPath.GetFileNameWithoutExtension(file.Name)];

            // retrieve the anim for each record
            foreach (var record in records)
            {
                var sequenceName = record.SequenceName;
                if (string.IsNullOrEmpty(sequenceName))
                {
                    // Skip if this record has no sequence name.
                    continue;
                }

                var entry = gaf.FirstOrDefault(
                    x => string.Equals(x.Name, sequenceName, StringComparison.OrdinalIgnoreCase));
                if (entry == null)
                {
                    // skip if the sequence is not in this gaf file
                    continue;
                }

                var frame = entry.Frames[0];

                Bitmap bmp;
                if (frame.Data == null || frame.Width == 0 || frame.Height == 0)
                {
                    bmp = new Bitmap(50, 50);
                }
                else
                {
                    bmp = BitmapConvert.ToBitmap(
                        frame.Data,
                        frame.Width,
                        frame.Height,
                        frame.TransparencyIndex);
                }

                var offsetImage = new OffsetBitmap(-frame.OffsetX, -frame.OffsetY, bmp);
                this.Records.Add(new KeyValuePair<string, OffsetBitmap>(record.Name, offsetImage));
            }
        }

        private bool IsNeededFile(HpiEntry entry)
        {
            var file = entry.Name;
            return file.EndsWith(".gaf", StringComparison.OrdinalIgnoreCase)
                && this.filenameFeatureMap.ContainsKey(
                    HpiPath.GetFileNameWithoutExtension(file));
        }
    }
}
