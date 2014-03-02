﻿namespace Mappy.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Drawing;
    using System.Windows.Forms;
    using Data;

    using Mappy.Collections;

    using Models;
    using UI.Controls;
    using UI.Drawables;
    using Util;

    public class MapPresenter
    {
        private static readonly IDrawable[] StartPositionImages = new IDrawable[10];

        private readonly ImageLayerView view;
        private readonly IMapPresenterModel model;

        private readonly List<ImageLayerCollection.Item> tileMapping = new List<ImageLayerCollection.Item>();
        private readonly IDictionary<int, ImageLayerCollection.Item> featureMapping = new Dictionary<int, ImageLayerCollection.Item>();

        private readonly ImageLayerCollection.Item[] startPositionMapping = new ImageLayerCollection.Item[10];

        private readonly MapCommandHandler commandHandler;

        private DrawableTile baseTile;

        static MapPresenter()
        {
            for (int i = 0; i < 10; i++)
            {
                var image = new DrawableBitmap(Util.GetStartImage(i + 1));
                MapPresenter.StartPositionImages[i] = image;
            }
        }

        public MapPresenter(ImageLayerView view, IMapPresenterModel model)
        {
            this.view = view;
            this.model = model;

            this.commandHandler = new MapCommandHandler(view, model);

            this.model.PropertyChanged += this.ModelPropertyChanged;

            this.PopulateView();
            this.WireMap();

            this.view.MouseDown += this.ViewMouseDown;
            this.view.MouseMove += this.ViewMouseMove;
            this.view.MouseUp += this.ViewMouseUp;
            this.view.KeyDown += this.ViewKeyDown;

            this.view.DragEnter += this.ViewDragEnter;
            this.view.DragDrop += this.ViewDragDrop;

            this.view.GridVisible = this.model.GridVisible;
            this.view.GridColor = this.model.GridColor;
            this.view.GridSize = this.model.GridSize;
        }

        public void DeleteSelection()
        {
            if (this.view.SelectedItem == null)
            {
                return;
            }

            ItemTag tag = (ItemTag)this.view.SelectedItem.Tag;
            tag.DeleteItem();
        }

        private void ViewDragDrop(object sender, DragEventArgs e)
        {
            if (this.model.Map == null)
            {
                return;
            }

            Point pos = this.view.PointToClient(new Point(e.X, e.Y));
            pos = this.view.ToVirtualPoint(pos);

            if (e.Data.GetDataPresent(typeof(StartPositionDragData)))
            {
                StartPositionDragData posData = (StartPositionDragData)e.Data.GetData(typeof(StartPositionDragData));
                this.model.SetStartPosition(posData.PositionNumber, pos.X, pos.Y);
            }
            else
            {
                string data = e.Data.GetData(DataFormats.Text).ToString();
                int id;
                if (int.TryParse(data, out id))
                {
                    int quantX = pos.X / 32;
                    int quantY = pos.Y / 32;
                    this.model.PlaceSection(id, quantX, quantY);
                    this.view.SelectedItem = this.tileMapping[this.model.Map.FloatingTiles.Count - 1];
                }
                else
                {
                    Point? featurePos = Util.ScreenToHeightIndex(this.model.Map.Tile.HeightGrid, pos);
                    if (featurePos.HasValue)
                    {
                        if (this.model.TryPlaceFeature(data, featurePos.Value.X, featurePos.Value.Y))
                        {
                            var index = this.model.Map.Tile.HeightGrid.ToIndex(featurePos.Value.X, featurePos.Value.Y);
                            this.view.SelectedItem = this.featureMapping[index];
                        }
                    }
                }
            }
        }

        private void ViewDragEnter(object sender, DragEventArgs e)
        {
            if (this.model.Map == null)
            {
                e.Effect = DragDropEffects.None;
                return;
            }

            if (e.Data.GetDataPresent(DataFormats.Text))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else if (e.Data.GetDataPresent(typeof(StartPositionDragData)))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        #region Private Methods

        private void WireMap()
        {
            if (this.model.Map == null)
            {
                return;
            }

            this.model.Map.Features.EntriesChanged += this.FeatureChanged;
            this.model.Map.FloatingTiles.ListChanged += this.TilesChanged;

            this.model.Map.Tile.TileGridChanged += this.BaseTileChanged;
            this.model.Map.Tile.HeightGridChanged += this.BaseTileChanged;

            foreach (var t in this.model.Map.FloatingTiles)
            {
                t.LocationChanged += this.TileLocationChanged;
            }

            this.model.Map.Attributes.StartPositionChanged += this.StartPositionChanged;
        }

        private void PopulateView()
        {
            this.tileMapping.Clear();
            this.featureMapping.Clear();
            this.view.Items.Clear();
            this.baseTile = null;

            if (this.model.Map == null)
            {
                this.view.CanvasSize = Size.Empty;
                return;
            }

            this.view.CanvasSize = new Size(
                this.model.Map.Tile.TileGrid.Width * 32,
                this.model.Map.Tile.TileGrid.Height * 32);

            this.baseTile = new DrawableTile(this.model.Map.Tile);
            this.baseTile.DrawHeightMap = this.model.HeightmapVisible;
            ImageLayerCollection.Item baseItem = new ImageLayerCollection.Item(
                0,
                0,
                -1,
                this.baseTile);

            baseItem.Locked = true;

            this.view.Items.Add(baseItem);

            int count = 0;
            foreach (Positioned<IMapTile> t in this.model.Map.FloatingTiles)
            {
                this.InsertTile(t, count++);
            }

            foreach (var f in this.model.Map.Features.CoordinateEntries)
            {
                this.InsertFeature(f.Value, f.Key.X, f.Key.Y);
            }

            for (int i = 0; i < 10; i++)
            {
                this.UpdateStartPosition(i);
            }
        }

        private void InsertTile(Positioned<IMapTile> t, int index)
        {
            ImageLayerCollection.Item i = new ImageLayerCollection.Item(
                    t.Location.X * 32,
                    t.Location.Y * 32,
                    index,
                    new DrawableTile(t.Item));
            i.Tag = new SectionTag(this, this.commandHandler, t);
            this.tileMapping.Insert(index, i);
            this.view.Items.Add(i);
        }

        private void RemoveTile(int index)
        {
            ImageLayerCollection.Item item = this.tileMapping[index];
            this.view.Items.Remove(item);
            this.tileMapping.RemoveAt(index);
            if (this.view.SelectedItem == item)
            {
                this.view.SelectedItem = null;
            }
        }

        private void InsertFeature(Feature f, int x, int y)
        {
            Rectangle r = f.GetDrawBounds(this.model.Map.Tile.HeightGrid, x, y);
            ImageLayerCollection.Item i = new ImageLayerCollection.Item(
                    r.X,
                    r.Y,
                    (y * this.model.Map.Features.Width) + x + 1000, // magic number to separate from tiles
                    new DrawableBitmap(f.Image));
            i.Tag = new FeatureTag(this, new Point(x, y));
            i.Visible = this.model.FeaturesVisible;
            this.featureMapping[this.ToFeatureIndex(x, y)] = i;
            this.view.Items.Add(i);
        }

        private bool RemoveFeature(int index)
        {
            if (this.featureMapping.ContainsKey(index))
            {
                ImageLayerCollection.Item item = this.featureMapping[index];
                this.view.Items.Remove(item);
                this.featureMapping.Remove(index);
                if (this.view.SelectedItem == item)
                {
                    this.view.SelectedItem = null;
                }

                return true;
            }

            return false;
        }

        private void MoveFeature(int oldIndex, int newIndex)
        {
            var old = this.featureMapping[oldIndex];

            bool isSelected = this.view.SelectedItem == old;

            this.RemoveFeature(oldIndex);
            this.InsertFeature(newIndex);

            if (isSelected)
            {
                this.view.SelectedItem = this.featureMapping[newIndex];
            }
        }

        private void UpdateFeature(int index)
        {
            this.RemoveFeature(index);

            this.InsertFeature(index);
        }

        private void InsertFeature(int index)
        {
            Point p = this.ToFeaturePoint(index);

            Feature f;
            if (this.model.Map.Features.TryGetValue(p.X, p.Y, out f))
            {
                this.InsertFeature(f, p.X, p.Y);
            }
        }

        private Point ToFeaturePoint(int index)
        {
            int x = index % this.model.Map.Features.Width;
            int y = index / this.model.Map.Features.Width;
            Point p = new Point(x, y);
            return p;
        }

        private int ToFeatureIndex(Point p)
        {
            return this.ToFeatureIndex(p.X, p.Y);
        }

        private int ToFeatureIndex(int x, int y)
        {
            return (y * this.model.Map.Features.Width) + x;
        }

        #endregion

        private void DragFeatureTo(Point featureCoords, Point location)
        {
            Point? pos = Util.ScreenToHeightIndex(
                    this.model.Map.Tile.HeightGrid,
                    location);
            if (!pos.HasValue)
            {
                return;
            }

            this.model.TranslateFeature(
                featureCoords,
                pos.Value.X - featureCoords.X,
                pos.Value.Y - featureCoords.Y);
        }

        private void RefreshFeatureVisibility()
        {
            foreach (var i in this.featureMapping.Values)
            {
                i.Visible = this.model.FeaturesVisible;
            }
        }

        private void RefreshHeightmapVisibility()
        {
            if (this.baseTile == null)
            {
                return;
            }

            this.baseTile.DrawHeightMap = this.model.HeightmapVisible;
            this.view.Invalidate();
        }

        #region Model Event Handlers

        private void ModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "Map":
                    this.WireMap();
                    this.PopulateView();
                    this.RefreshHeightmapVisibility();
                    break;
                case "FeaturesVisible":
                    this.RefreshFeatureVisibility();
                    break;
                case "HeightmapVisible":
                    this.RefreshHeightmapVisibility();
                    break;
                case "GridVisible":
                    this.view.GridVisible = this.model.GridVisible;
                    break;
                case "GridColor":
                    this.view.GridColor = this.model.GridColor;
                    break;
                case "GridSize":
                    this.view.GridSize = this.model.GridSize;
                    break;
            }
        }

        private void StartPositionChanged(object sender, StartPositionChangedEventArgs e)
        {
            this.UpdateStartPosition(e.Index);
        }

        private void UpdateStartPosition(int index)
        {
            if (this.startPositionMapping[index] != null)
            {
                this.view.Items.Remove(this.startPositionMapping[index]);

                if (this.view.SelectedItem == this.startPositionMapping[index])
                {
                    this.view.SelectedItem = null;
                }

                this.startPositionMapping[index] = null;
            }

            Point? p = this.model.Map.Attributes.GetStartPosition(index);
            if (p.HasValue)
            {
                IDrawable img = StartPositionImages[index];
                var i = new ImageLayerCollection.Item(
                    p.Value.X - (img.Width / 2),
                    p.Value.Y - 58,
                    int.MaxValue,
                    img);
                i.Tag = new StartPositionTag(this, index);
                this.startPositionMapping[index] = i;
                this.view.Items.Add(i);
            }
        }

        private void TileLocationChanged(object sender, EventArgs e)
        {
            Positioned<IMapTile> item = (Positioned<IMapTile>)sender;
            int index = this.model.Map.FloatingTiles.IndexOf(item);

            var mapping = this.tileMapping[index];
            bool selected = mapping == this.view.SelectedItem;

            this.RemoveTile(index);
            this.InsertTile(item, index);

            if (selected)
            {
                this.view.SelectedItem = this.tileMapping[index];
            }
        }

        private void BaseTileChanged(object sender, EventArgs e)
        {
            this.view.Invalidate();
        }

        private void TilesChanged(object sender, ListChangedEventArgs e)
        {
            switch (e.ListChangedType)
            {
                case ListChangedType.ItemAdded:
                    this.InsertTile(this.model.Map.FloatingTiles[e.NewIndex], e.NewIndex);
                    this.model.Map.FloatingTiles[e.NewIndex].LocationChanged += this.TileLocationChanged;
                    break;
                case ListChangedType.ItemDeleted:
                    this.RemoveTile(e.NewIndex);
                    break;
                case ListChangedType.ItemMoved:
                    this.RemoveTile(e.OldIndex);
                    this.InsertTile(this.model.Map.FloatingTiles[e.NewIndex], e.NewIndex);
                    break;
                case ListChangedType.Reset:
                    this.PopulateView(); // probably a bit heavy-handed
                    break;
                default:
                    throw new ArgumentException("unknown list changed type: " + e.ListChangedType);
            }
        }

        private void FeatureChanged(object sender, SparseGridEventArgs e)
        {
            switch (e.Action)
            {
                case SparseGridEventArgs.ActionType.Set:
                    foreach (var index in e.Indexes)
                    {
                        this.UpdateFeature(index);
                    }

                    break;
                case SparseGridEventArgs.ActionType.Move:
                    var oldIter = e.OriginalIndexes.GetEnumerator();
                    var newIter = e.Indexes.GetEnumerator();
                    while (oldIter.MoveNext() && newIter.MoveNext())
                    {
                        this.MoveFeature(oldIter.Current, newIter.Current);
                    }

                    break;
                case SparseGridEventArgs.ActionType.Remove:
                    foreach (var index in e.Indexes)
                    {
                        this.RemoveFeature(index);
                    }

                    break;
            }
        }

        #endregion

        #region View Event Handlers

        private void ViewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                this.DeleteSelection();
            }
        }

        private void ViewMouseDown(object sender, MouseEventArgs e)
        {
            var virtualLoc = this.view.ToVirtualPoint(e.Location);
            this.commandHandler.MouseDown(virtualLoc.X, virtualLoc.Y);
        }

        private void ViewMouseMove(object sender, MouseEventArgs e)
        {
            var virtualLoc = this.view.ToVirtualPoint(e.Location);
            this.commandHandler.MouseMove(virtualLoc.X, virtualLoc.Y);
        }

        private void ViewMouseUp(object sender, MouseEventArgs e)
        {
            var virtualLoc = this.view.ToVirtualPoint(e.Location);
            this.commandHandler.MouseUp(virtualLoc.X, virtualLoc.Y);
        }

        #endregion

        public abstract class ItemTag
        {
            protected ItemTag(MapPresenter presenter)
            {
                this.Presenter = presenter;
            }

            public MapPresenter Presenter { get; private set; }

            public abstract void DeleteItem();

            public abstract void DragTo(Point virtualLocation);
        }

        public class FeatureTag : ItemTag
        {
            public FeatureTag(MapPresenter presenter, Point coordinates)
                : base(presenter)
            {
                this.Coordinates = coordinates;
            }

            public Point Coordinates { get; private set; }

            public override void DeleteItem()
            {
                this.Presenter.model.RemoveFeature(this.Coordinates);
            }

            public override void DragTo(Point virtualLocation)
            {
                this.Presenter.DragFeatureTo(this.Coordinates, virtualLocation);
            }
        }

        public class StartPositionTag : ItemTag
        {
            public StartPositionTag(MapPresenter presenter, int index)
                : base(presenter)
            {
                this.Index = index;
            }

            public int Index { get; set; }

            public override void DeleteItem()
            {
                this.Presenter.model.RemoveStartPosition(this.Index);
            }

            public override void DragTo(Point virtualLocation)
            {
                this.Presenter.model.TranslateStartPositionTo(this.Index, virtualLocation.X, virtualLocation.Y);
                this.Presenter.view.SelectedItem = this.Presenter.startPositionMapping[this.Index];
            }
        }

        public class SectionTag : ItemTag
        {
            private readonly MapCommandHandler handler;

            public SectionTag(MapPresenter presenter, MapCommandHandler handler, Positioned<IMapTile> tile)
                : base(presenter)
            {
                this.Tile = tile;
                this.handler = handler;
            }

            public Positioned<IMapTile> Tile { get; set; }

            public override void DeleteItem()
            {
                int index = this.Presenter.model.Map.FloatingTiles.IndexOf(this.Tile);
                this.Presenter.model.RemoveSection(index);
            }

            public override void DragTo(Point virtualLocation)
            {
                this.handler.DragSectionTo(this.Tile, virtualLocation);
            }
        }
    }
}
