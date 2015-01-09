﻿namespace Mappy.Models
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Linq;
    using System.Windows.Forms;

    using Geometry;

    using Mappy.Collections;
    using Mappy.Controllers;
    using Mappy.Data;
    using Mappy.Database;
    using Mappy.IO;
    using Mappy.Minimap;
    using Mappy.Models.BandboxBehaviours;
    using Mappy.Operations;
    using Mappy.Operations.SelectionModel;
    using Mappy.Util;
    using Mappy.Util.ImageSampling;

    using TAUtil;
    using TAUtil.Gdi.Palette;
    using TAUtil.Hpi;
    using TAUtil.Sct;
    using TAUtil.Tdf;
    using TAUtil.Tnt;

    public class CoreModel : Notifier, IMinimapModel, IMainModel
    {
        private readonly OperationManager undoManager = new OperationManager();

        private readonly IFeatureDatabase featureRecords;
        private readonly IList<Section> sections;

        private readonly SectionFactory sectionFactory;

        private readonly MapModelFactory mapModelFactory;

        private readonly MapSaver mapSaver;

        private readonly IDialogService dialogService;

        private ISelectionModel map;
        private bool isDirty;
        private string openFilePath;
        private bool isFileOpen;
        private bool isFileReadOnly;
        private bool heightmapVisible;
        private bool featuresVisible = true;

        private bool minimapVisible;

        private bool gridVisible;
        private Size gridSize = new Size(16, 16);
        private Color gridColor = MappySettings.Settings.GridColor;

        private bool previousTranslationOpen;

        private Rectangle2D viewportRectangle;

        private Bitmap minimapImage;

        private int deltaX;

        private int deltaY;

        private IBandboxBehaviour bandboxBehaviour;

        private bool previousSeaLevelOpen;

        private bool canCopy;

        private bool canCut;

        public CoreModel(IDialogService dialogService)
        {
            this.dialogService = dialogService;

            this.bandboxBehaviour = new TileBandboxBehaviour(this);

            this.bandboxBehaviour.PropertyChanged += this.BandboxBehaviourPropertyChanged;

            this.featureRecords = new FeatureDictionary();
            this.sections = new List<Section>();

            this.sectionFactory = new SectionFactory();
            this.mapModelFactory = new MapModelFactory();

            this.mapSaver = new MapSaver();

            // hook up undoManager
            this.undoManager.CanUndoChanged += this.CanUndoChanged;
            this.undoManager.CanRedoChanged += this.CanRedoChanged;
            this.undoManager.IsMarkedChanged += this.IsMarkedChanged;
        }

        public event EventHandler<ListChangedEventArgs> TilesChanged;

        public event EventHandler<GridEventArgs> BaseTileGraphicsChanged;

        public event EventHandler<GridEventArgs> BaseTileHeightChanged;

        public event EventHandler<StartPositionChangedEventArgs> StartPositionChanged;

        public ISelectionModel Map
        {
            get
            {
                return this.map;
            }

            private set
            {
                if (this.SetField(ref this.map, value, "Map"))
                {
                    this.undoManager.Clear();
                    this.previousTranslationOpen = false;
                    
                    if (this.Map == null)
                    {
                        this.MinimapImage = null;
                        this.IsFileOpen = false;
                    }
                    else
                    {
                        this.MinimapImage = this.Map.Minimap;

                        this.Map.SelectedStartPositionChanged += this.MapSelectedStartPositionChanged;
                        this.Map.SelectedTileChanged += this.MapSelectedTileChanged;
                        this.Map.SelectedFeatures.CollectionChanged += this.SelectedFeaturesChanged;

                        this.Map.PropertyChanged += this.MapOnPropertyChanged;

                        this.Map.FloatingTiles.ListChanged += this.FloatingTilesOnListChanged;
                        this.Map.Tile.TileGridChanged += this.TileOnTileGridChanged;
                        this.Map.Tile.HeightGridChanged += this.TileOnHeightGridChanged;
                        this.Map.Attributes.StartPositionChanged += this.AttributesOnStartPositionChanged;
                        this.IsFileOpen = true;
                    }

                    this.FireChange("MapOpen");
                    this.FireChange("Features");
                    this.FireChange("FloatingTiles");
                    this.FireChange("BaseTile");
                    this.FireChange("MapWidth");
                    this.FireChange("MapHeight");
                    this.FireChange("SeaLevel");

                    this.FireChange("SelectedTile");
                    this.FireChange("SelectedStartPosition");
                    this.FireChange("SelectedFeatures");

                    this.FireChange("CanPaste");

                    for (var i = 0; i < 10; i++)
                    {
                        this.AttributesOnStartPositionChanged(this, new StartPositionChangedEventArgs(i));
                    }
                }
            }
        }

        public int SeaLevel
        {
            get
            {
                return this.Map == null ? 0 : this.Map.SeaLevel;
            }

            set
            {
                this.Map.SeaLevel = value;
            }
        }

        public bool CanUndo
        {
            get { return this.undoManager.CanUndo; }
        }

        public bool CanRedo
        {
            get { return this.undoManager.CanRedo; }
        }

        public bool CanCopy
        {
            get
            {
                return this.canCopy;
            }

            set
            {
                this.SetField(ref this.canCopy, value, "CanCopy");
            }
        }

        public bool CanPaste
        {
            get
            {
                return this.MapOpen;
            }
        }

        public bool CanCut
        {
            get
            {
                return this.canCut;
            }

            set
            {
                this.SetField(ref this.canCut, value, "CanCut");
            }
        }

        public IFeatureDatabase FeatureRecords
        {
            get { return this.featureRecords; }
        }

        public IList<Section> Sections
        {
            get { return this.sections; }
        } 

        public bool IsDirty
        {
            get { return this.isDirty; }
            private set { this.SetField(ref this.isDirty, value, "IsDirty"); }
        }

        public string FilePath
        {
            get { return this.openFilePath; }
            private set { this.SetField(ref this.openFilePath, value, "FilePath"); }
        }

        public bool IsFileOpen
        {
            get { return this.isFileOpen; }
            private set { this.SetField(ref this.isFileOpen, value, "IsFileOpen"); }
        }

        public bool IsFileReadOnly
        {
            get { return this.isFileReadOnly; }
            private set { this.SetField(ref this.isFileReadOnly, value, "IsFileReadOnly"); }
        }

        public bool HeightmapVisible
        {
            get { return this.heightmapVisible; }
            set { this.SetField(ref this.heightmapVisible, value, "HeightmapVisible"); }
        }

        public bool FeaturesVisible
        {
            get { return this.featuresVisible; }
            set { this.SetField(ref this.featuresVisible, value, "FeaturesVisible"); }
        }

        public int? SelectedTile
        {
            get
            {
                return this.Map == null ? null : this.Map.SelectedTile;
            }
        }

        public int? SelectedStartPosition
        {
            get
            {
                return this.Map == null ? null : this.Map.SelectedStartPosition;
            }
        }

        public ICollection<Guid> SelectedFeatures
        {
            get
            {
                return this.Map == null ? null : this.Map.SelectedFeatures;
            }
        }

        public Rectangle BandboxRectangle
        {
            get
            {
                return this.bandboxBehaviour.BandboxRectangle;
            }
        }

        public IList<Positioned<IMapTile>> FloatingTiles
        {
            get
            {
                return this.Map == null ? null : this.Map.FloatingTiles;
            }
        }

        public IMapTile BaseTile
        {
            get
            {
                return this.Map == null ? null : this.Map.Tile;
            }
        }

        public int MapWidth
        {
            get
            {
                return this.Map == null ? 0 : this.Map.Tile.TileGrid.Width;
            }
        }

        public int MapHeight
        {
            get
            {
                return this.Map == null ? 0 : this.Map.Tile.TileGrid.Height;
            }
        }

        public bool MapOpen
        {
            get
            {
                return this.Map != null;
            }
        }

        public bool MinimapVisible
        {
            get
            {
                return this.minimapVisible;
            }

            set
            {
                this.SetField(ref this.minimapVisible, value, "MinimapVisible");
            }
        }

        public bool GridVisible
        {
            get { return this.gridVisible; }
            set { this.SetField(ref this.gridVisible, value, "GridVisible"); }
        }

        public Size GridSize
        {
            get { return this.gridSize; }
            set { this.SetField(ref this.gridSize, value, "GridSize"); }
        }

        public Color GridColor
        {
            get
            {
                return this.gridColor;
            }

            set
            {
                MappySettings.Settings.GridColor = value;
                MappySettings.SaveSettings();
                this.SetField(ref this.gridColor, value, "GridColor");
            }
        }

        public Rectangle2D ViewportRectangle
        {
            get
            {
                return this.viewportRectangle;
            }

            set
            {
                this.SetField(ref this.viewportRectangle, value, "ViewportRectangle");
            }
        }

        public Bitmap MinimapImage
        {
            get
            {
                return this.minimapImage;
            }

            private set
            {
                this.SetField(ref this.minimapImage, value, "MinimapImage");
            }
        }

        public void ShowAbout()
        {
            this.dialogService.ShowAbout();
        }

        public void Initialize()
        {
            var dlg = this.dialogService.CreateProgressView();
            dlg.Title = "Loading Mappy";
            dlg.ShowProgress = true;
            dlg.CancelEnabled = true;

            var worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.WorkerSupportsCancellation = true;
            worker.DoWork += delegate(object sender, DoWorkEventArgs args)
            {
                var w = (BackgroundWorker)sender;

                LoadResult<Section> result;
                if (!SectionLoadingUtils.LoadSections(
                        i => w.ReportProgress((50 * i) / 100),
                        () => w.CancellationPending,
                        out result))
                {
                    args.Cancel = true;
                    return;
                }

                LoadResult<Feature> featureResult;
                if (!FeatureLoadingUtils.LoadFeatures(
                    i => w.ReportProgress(50 + ((50 * i) / 100)),
                    () => w.CancellationPending,
                    out featureResult))
                {
                    args.Cancel = true;
                    return;
                }

                args.Result = new SectionFeatureLoadResult
                {
                    Sections = result.Records,
                    Features = featureResult.Records,
                    Errors = result.Errors
                        .Concat(featureResult.Errors)
                        .GroupBy(x => x.HpiPath)
                        .Select(x => x.First())
                        .ToList(),
                    FileErrors = result.FileErrors
                        .Concat(featureResult.FileErrors)
                        .ToList(),
                };
            };

            worker.ProgressChanged += (sender, args) => dlg.Progress = args.ProgressPercentage;
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs args)
            {
                if (args.Error != null)
                {
                    Program.HandleUnexpectedException(args.Error);
                    Application.Exit();
                    return;
                }

                if (args.Cancelled)
                {
                    Application.Exit();
                    return;
                }

                var sectionResult = (SectionFeatureLoadResult)args.Result;

                int nextId = 0;
                foreach (var s in sectionResult.Sections)
                {
                    s.Id = nextId++;
                    this.Sections.Add(s);
                }

                this.FireChange("Sections");

                foreach (var f in sectionResult.Features)
                {
                    this.FeatureRecords.AddFeature(f);
                }

                this.FireChange("FeatureRecords");

                if (sectionResult.Errors.Count > 0 || sectionResult.FileErrors.Count > 0)
                {
                    var hpisList = sectionResult.Errors.Select(x => x.HpiPath);
                    var filesList = sectionResult.FileErrors.Select(x => x.HpiPath + "\\" + x.FeaturePath);
                    this.dialogService.ShowError("Failed to load the following files:\n\n"
                        + string.Join("\n", hpisList) + "\n"
                        + string.Join("\n", filesList));
                }

                dlg.Close();
            };

            dlg.CancelPressed += (sender, args) => worker.CancelAsync();

            dlg.MessageText = "Loading sections and features ...";
            worker.RunWorkerAsync();

            dlg.Display();
        }

        public void Undo()
        {
            this.undoManager.Undo();
        }

        public void Redo()
        {
            this.undoManager.Redo();
        }

        public void New(int width, int height)
        {
            var map = new SelectionMapModel(new BindingMapModel(new MapModel(width, height)));
            GridMethods.Fill(map.Tile.TileGrid, Globals.DefaultTile);
            this.Map = map;
            this.FilePath = null;
            this.IsFileReadOnly = false;
        }

        public bool New()
        {
            if (!this.CheckOkayDiscard())
            {
                return false;
            }

            Size size = this.dialogService.AskUserNewMapSize();
            if (size.Width == 0 || size.Height == 0)
            {
                return false;
            }

            this.New(size.Width, size.Height);
            return true;
        }

        public bool Open()
        {
            if (!this.CheckOkayDiscard())
            {
                return false;
            }

            string filename = this.dialogService.AskUserToOpenFile();
            if (string.IsNullOrEmpty(filename))
            {
                return false;
            }

            return this.OpenMap(filename);
        }

        public bool Save()
        {
            if (this.FilePath == null || this.IsFileReadOnly)
            {
                return this.SaveAs();
            }

            return this.SaveHelper(this.FilePath);
        }

        public bool SaveAs()
        {
            string path = this.dialogService.AskUserToSaveFile();

            if (path == null)
            {
                return false;
            }

            return this.SaveHelper(path);
        }

        public void OpenPreferences()
        {
            this.dialogService.CapturePreferences();
        }

        public void Close()
        {
            if (this.CheckOkayDiscard())
            {
                Application.Exit();
            }
        }

        public bool CheckOkayDiscard()
        {
            if (!this.IsDirty)
            {
                return true;
            }

            DialogResult r = this.dialogService.AskUserToDiscardChanges();
            switch (r)
            {
                case DialogResult.Yes:
                    return this.Save();
                case DialogResult.Cancel:
                    return false;
                case DialogResult.No:
                    return true;
                default:
                    throw new InvalidOperationException("unexpected dialog result: " + r);
            }
        }

        public void SaveHpi(string filename)
        {
            // flatten before save --- only the base tile is written to disk
            IReplayableOperation flatten = OperationFactory.CreateFlattenOperation(this.Map);
            flatten.Execute();

            this.mapSaver.SaveHpi(this.Map, filename);

            flatten.Undo();

            this.undoManager.SetNowAsMark();

            this.FilePath = filename;
            this.IsFileReadOnly = false;
        }

        public void Save(string filename)
        {
            // flatten before save --- only the base tile is written to disk
            IReplayableOperation flatten = OperationFactory.CreateFlattenOperation(this.Map);
            flatten.Execute();

            var otaName = filename.Substring(0, filename.Length - 4) + ".ota";
            this.mapSaver.SaveTnt(this.Map, filename);
            this.mapSaver.SaveOta(this.Map.Attributes, otaName);

            flatten.Undo();

            this.undoManager.SetNowAsMark();

            this.FilePath = filename;
            this.IsFileReadOnly = false;
        }

        public void OpenSct(string filename)
        {
            MapTile t;
            using (var s = new SctReader(filename))
            {
                t = this.sectionFactory.TileFromSct(s);
            }

            this.Map = new SelectionMapModel(new BindingMapModel(new MapModel(t)));
        }

        public void OpenTnt(string filename)
        {
            MapModel m;

            var otaFileName = filename.Substring(0, filename.Length - 4) + ".ota";
            if (File.Exists(otaFileName))
            {
                TdfNode attrs;
                using (var ota = File.OpenRead(otaFileName))
                {
                    attrs = TdfNode.LoadTdf(ota);
                }

                using (var s = new TntReader(filename))
                {
                    m = this.mapModelFactory.FromTntAndOta(s, attrs);
                }
            }
            else
            {
                using (var s = new TntReader(filename))
                {
                    m = this.mapModelFactory.FromTnt(s);
                }
            }

            this.Map = new SelectionMapModel(new BindingMapModel(m));
            this.FilePath = filename;
        }

        public void OpenHapi(string hpipath, string mappath, bool readOnly = false)
        {
            MapModel m;

            using (HpiReader hpi = new HpiReader(hpipath))
            {
                string otaPath = HpiPath.ChangeExtension(mappath, ".ota");

                TdfNode n;

                using (var ota = hpi.ReadTextFile(otaPath))
                {
                    n = TdfNode.LoadTdf(ota);
                }

                using (var s = new TntReader(hpi.ReadFile(mappath)))
                {
                    m = this.mapModelFactory.FromTntAndOta(s, n);
                }
            }

            this.Map = new SelectionMapModel(new BindingMapModel(m));
            this.FilePath = hpipath;
            this.IsFileReadOnly = readOnly;
        }

        public void DragDropStartPosition(int index, int x, int y)
        {
            if (this.Map == null)
            {
                return;
            }

            var location = new Point(x, y);

            var op = new CompositeOperation(
                OperationFactory.CreateDeselectAndMergeOperation(this.Map),
                new ChangeStartPositionOperation(this.Map, index, location),
                new SelectStartPositionOperation(this.Map, index));

            this.undoManager.Execute(op);
            this.previousTranslationOpen = false;
        }

        public void DragDropTile(int id, int x, int y)
        {
            if (this.Map == null)
            {
                return;
            }

            int quantX = x / 32;
            int quantY = y / 32;

            var section = this.Sections[id].GetTile();

            this.AddAndSelectTile(section, quantX, quantY);
        }

        public void DragDropFeature(string name, int x, int y)
        {
            if (this.Map == null)
            {
                return;
            }

            Point? featurePos = this.ScreenToHeightIndex(x, y);
            if (featurePos.HasValue && !this.Map.HasFeatureInstanceAt(featurePos.Value.X, featurePos.Value.Y))
            {
                var inst = new FeatureInstance(Guid.NewGuid(), name, featurePos.Value.X, featurePos.Value.Y);
                var addOp = new AddFeatureOperation(this.Map, inst);
                var selectOp = new SelectFeatureOperation(this.Map, inst.Id);
                var op = new CompositeOperation(
                    OperationFactory.CreateDeselectAndMergeOperation(this.Map),
                    addOp,
                    selectOp);
                this.undoManager.Execute(op);
            }
        }

        public void StartBandbox(int x, int y)
        {
            this.bandboxBehaviour.StartBandbox(x, y);
        }

        public void GrowBandbox(int x, int y)
        {
            this.bandboxBehaviour.GrowBandbox(x, y);
        }

        public void CommitBandbox()
        {
            this.bandboxBehaviour.CommitBandbox();
        }

        public void TranslateSelection(int x, int y)
        {
            if (this.SelectedStartPosition.HasValue)
            {
                this.TranslateStartPosition(
                    this.SelectedStartPosition.Value,
                    x,
                    y);
            }
            else if (this.SelectedTile.HasValue)
            {
                this.deltaX += x;
                this.deltaY += y;

                this.TranslateSection(
                    this.SelectedTile.Value,
                    this.deltaX / 32,
                    this.deltaY / 32);

                this.deltaX %= 32;
                this.deltaY %= 32;
            }
            else if (this.SelectedFeatures.Count > 0)
            {
                // TODO: restore old behaviour
                // where heightmap is taken into account when placing features

                this.deltaX += x;
                this.deltaY += y;

                int quantX = this.deltaX / 16;
                int quantY = this.deltaY / 16;

                bool success = this.TranslateFeatureBatch(
                    this.SelectedFeatures,
                    quantX,
                    quantY);

                if (success)
                {
                    this.deltaX %= 16;
                    this.deltaY %= 16;
                }
            }
        }

        public void DeleteSelection()
        {
            if (this.SelectedFeatures.Count > 0)
            {
                var ops = new List<IReplayableOperation>();
                ops.Add(new DeselectOperation(this.Map));
                ops.AddRange(this.SelectedFeatures.Select(x => new RemoveFeatureOperation(this.Map, x)));
                this.undoManager.Execute(new CompositeOperation(ops));
            }

            if (this.SelectedTile.HasValue)
            {
                var deSelectOp = new DeselectOperation(this.Map);
                var removeOp = new RemoveTileOperation(this.Map.FloatingTiles, this.SelectedTile.Value);
                this.undoManager.Execute(new CompositeOperation(deSelectOp, removeOp));
            }

            if (this.SelectedStartPosition.HasValue)
            {
                var deSelectOp = new DeselectOperation(this.Map);
                var removeOp = new RemoveStartPositionOperation(this.Map, this.SelectedStartPosition.Value);
                this.undoManager.Execute(new CompositeOperation(deSelectOp, removeOp));
            }
        }

        public void CopySelectionToClipboard()
        {
            this.TryCopyToClipboard();
        }

        public void CutSelectionToClipboard()
        {
            if (this.TryCopyToClipboard())
            {
                this.DeleteSelection();
            }
        }

        public void PasteFromClipboard()
        {
            if (this.Map == null)
            {
                return;
            }

            var data = Clipboard.GetData(DataFormats.Serializable);
            if (data == null)
            {
                return;
            }

            var tile = data as IMapTile;
            if (tile != null)
            {
                this.PasteMapTile(tile);
            }
            else
            {
                var record = data as FeatureClipboardRecord;
                if (record != null)
                {
                    this.PasteFeature(record);
                }
            }
        }

        public Point? GetStartPosition(int index)
        {
            return this.Map == null ? null : this.Map.Attributes.GetStartPosition(index);
        }

        public void SelectTile(int index)
        {
            this.undoManager.Execute(
                new CompositeOperation(
                    OperationFactory.CreateDeselectAndMergeOperation(this.Map),
                    new SelectTileOperation(this.Map, index)));
        }

        public void SelectFeature(Guid index)
        {
            this.undoManager.Execute(
                new CompositeOperation(
                    OperationFactory.CreateDeselectAndMergeOperation(this.Map),
                    new SelectFeatureOperation(this.Map, index)));
        }

        public void SelectFeatures(IEnumerable<Guid> indices)
        {
            var list = new List<IReplayableOperation>();

            list.Add(OperationFactory.CreateDeselectAndMergeOperation(this.Map));
            list.AddRange(indices.Select(x => new SelectFeatureOperation(this.Map, x)));

            this.undoManager.Execute(new CompositeOperation(list));
        }

        public void SelectStartPosition(int index)
        {
            this.undoManager.Execute(
                new CompositeOperation(
                    OperationFactory.CreateDeselectAndMergeOperation(this.Map),
                    new SelectStartPositionOperation(this.Map, index)));
        }

        public void LiftAndSelectArea(int x, int y, int width, int height)
        {
            var liftOp = OperationFactory.CreateClippedLiftAreaOperation(this.Map, x, y, width, height);
            var index = this.Map.FloatingTiles.Count;
            var selectOp = new SelectTileOperation(this.Map, index);
            this.undoManager.Execute(new CompositeOperation(liftOp, selectOp));
        }

        public void FlushTranslation()
        {
            this.previousTranslationOpen = false;
            this.deltaX = 0;
            this.deltaY = 0;
        }

        public void ClearSelection()
        {
            if (this.SelectedTile == null && (this.SelectedFeatures == null || this.SelectedFeatures.Count == 0) && this.SelectedStartPosition == null)
            {
                return;
            }

            if (this.previousTranslationOpen)
            {
                this.FlushTranslation();
            }

            var deselectOp = new DeselectOperation(this.Map);

            if (this.Map.SelectedTile.HasValue)
            {
                var mergeOp = OperationFactory.CreateMergeSectionOperation(this.Map, this.Map.SelectedTile.Value);
                this.undoManager.Execute(new CompositeOperation(deselectOp, mergeOp));
            }
            else
            {
                this.undoManager.Execute(deselectOp);
            }
        }

        public void SetMinimap(Bitmap minimap)
        {
            var op = new UpdateMinimapOperation(this.Map, minimap);
            this.undoManager.Execute(op);
        }

        public void RefreshMinimap()
        {
            Bitmap minimap;
            using (var adapter = new MapPixelImageAdapter(this.Map.Tile.TileGrid))
            {
                minimap = Util.GenerateMinimap(adapter);
            }

            var op = new UpdateMinimapOperation(this.Map, minimap);
            this.undoManager.Execute(op);
        }

        public void RefreshMinimapHighQualityWithProgress()
        {
            var worker = Mappy.Util.Util.RenderMinimapWorker();

            var dlg = this.dialogService.CreateProgressView();
            dlg.Title = "Generating Minimap";
            dlg.MessageText = "Generating high quality minimap...";

            dlg.CancelPressed += (o, args) => worker.CancelAsync();
            worker.ProgressChanged += (o, args) => dlg.Progress = args.ProgressPercentage;
            worker.RunWorkerCompleted += delegate(object o, RunWorkerCompletedEventArgs args)
            {
                if (args.Error != null)
                {
                    Program.HandleUnexpectedException(args.Error);
                    Application.Exit();
                    return;
                }

                if (!args.Cancelled)
                {
                    var img = (Bitmap)args.Result;
                    this.SetMinimap(img);
                }

                dlg.Close();
            };

            worker.RunWorkerAsync(this.Map);
            dlg.Display();
        }

        public void SetGridSize(int size)
        {
            if (size == 0)
            {
                this.GridVisible = false;
            }
            else
            {
                this.GridVisible = true;
                this.GridSize = new Size(size, size);
            }
        }

        public void ChooseColor()
        {
            Color? c = this.dialogService.AskUserGridColor(this.GridColor);
            if (c.HasValue)
            {
                this.GridColor = c.Value;
            }
        }

        public void ExportHeightmap()
        {
            var loc = this.dialogService.AskUserToSaveHeightmap();
            if (loc == null)
            {
                return;
            }

            try
            {
                var b = Mappy.Util.Util.ExportHeightmap(this.Map.Tile.HeightGrid);
                using (var s = File.Create(loc))
                {
                    b.Save(s, ImageFormat.Png);
                }
            }
            catch (Exception)
            {
                this.dialogService.ShowError("There was a problem saving the heightmap.");
            }
        }

        public void ExportMinimap()
        {
            var loc = this.dialogService.AskUserToSaveMinimap();
            if (loc == null)
            {
                return;
            }

            try
            {
                using (var s = File.Create(loc))
                {
                    this.Map.Minimap.Save(s, ImageFormat.Png);
                }
            }
            catch (Exception)
            {
                this.dialogService.ShowError("There was a problem saving the minimap.");
            }
        }

        public void ExportMapImage()
        {
            var loc = this.dialogService.AskUserToSaveMapImage();
            if (loc == null)
            {
                return;
            }

            var pv = this.dialogService.CreateProgressView();

            var tempLoc = loc + ".mappy-partial";

            var bg = new BackgroundWorker();
            bg.WorkerReportsProgress = true;
            bg.WorkerSupportsCancellation = true;
            bg.DoWork += delegate(object sender, DoWorkEventArgs args)
            {
                var worker = (BackgroundWorker)sender;
                using (var s = File.Create(tempLoc))
                {
                    var success = Mappy.Util.Util.WriteMapImage(s, this.Map.Tile.TileGrid, worker.ReportProgress, () => worker.CancellationPending);
                    args.Cancel = !success;
                }
            };

            bg.ProgressChanged += (sender, args) => pv.Progress = args.ProgressPercentage;
            pv.CancelPressed += (sender, args) => bg.CancelAsync();

            bg.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs args)
            {
                try
                {
                    pv.Close();

                    if (args.Cancelled)
                    {
                        return;
                    }

                    if (args.Error != null)
                    {
                        this.dialogService.ShowError("There was a problem saving the map image.");
                        return;
                    }

                    if (File.Exists(loc))
                    {
                        File.Replace(tempLoc, loc, null);
                    }
                    else
                    {
                        File.Move(tempLoc, loc);
                    }
                }
                finally
                {
                    if (File.Exists(tempLoc))
                    {
                        File.Delete(tempLoc);
                    }
                }
            };

            bg.RunWorkerAsync();
            pv.Display();
        }

        public void ImportCustomSection()
        {
            var paths = this.dialogService.AskUserToChooseSectionImportPaths();
            if (paths == null)
            {
                return;
            }

            var dlg = this.dialogService.CreateProgressView();

            var bg = new BackgroundWorker();
            bg.WorkerSupportsCancellation = true;
            bg.WorkerReportsProgress = true;
            bg.DoWork += delegate(object sender, DoWorkEventArgs args)
            {
                var w = (BackgroundWorker)sender;
                var sect = ImageImport.ImportSection(
                    paths.GraphicPath,
                    paths.HeightmapPath,
                    w.ReportProgress,
                    () => w.CancellationPending);
                if (sect == null)
                {
                    args.Cancel = true;
                    return;
                }

                args.Result = sect;
            };

            bg.ProgressChanged += (sender, args) => dlg.Progress = args.ProgressPercentage;
            dlg.CancelPressed += (sender, args) => bg.CancelAsync();

            bg.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs args)
            {
                dlg.Close();

                if (args.Error != null)
                {
                    this.dialogService.ShowError(
                        "There was a problem importing the section: " + args.Error.Message);
                    return;
                }

                if (args.Cancelled)
                {
                    return;
                }

                this.PasteMapTileNoDeduplicateTopLeft((IMapTile)args.Result);
            };

            bg.RunWorkerAsync();

            dlg.Display();
        }

        public void ImportHeightmap()
        {
            var w = this.Map.Tile.HeightGrid.Width;
            var h = this.Map.Tile.HeightGrid.Height;

            var loc = this.dialogService.AskUserToChooseHeightmap(w, h);
            if (loc == null)
            {
                return;
            }

            try
            {
                Bitmap bmp;
                using (var s = File.OpenRead(loc))
                {
                    bmp = (Bitmap)Image.FromStream(s);
                }

                if (bmp.Width != w || bmp.Height != h)
                {
                    var msg = string.Format(
                        "Heightmap has incorrect dimensions. The required dimensions are {0}x{1}.",
                        w,
                        h);
                    this.dialogService.ShowError(msg);
                    return;
                }

                this.ReplaceHeightmap(Mappy.Util.Util.ReadHeightmap(bmp));
            }
            catch (Exception)
            {
                this.dialogService.ShowError("There was a problem importing the selected heightmap");
            }
        }

        public void ImportMinimap()
        {
            var loc = this.dialogService.AskUserToChooseMinimap();
            if (loc == null)
            {
                return;
            }

            try
            {
                Bitmap bmp;
                using (var s = File.OpenRead(loc))
                {
                    bmp = (Bitmap)Image.FromStream(s);
                }

                if (bmp.Width > TntConstants.MaxMinimapWidth
                    || bmp.Height > TntConstants.MaxMinimapHeight)
                {
                    var msg = string.Format(
                        "Minimap dimensions too large. The maximum size is {0}x{1}.",
                        TntConstants.MaxMinimapWidth,
                        TntConstants.MaxMinimapHeight);

                    this.dialogService.ShowError(msg);
                    return;
                }

                Quantization.ToTAPalette(bmp);
                this.SetMinimap(bmp);
            }
            catch (Exception)
            {
                this.dialogService.ShowError("There was a problem importing the selected minimap.");
            }
        }

        public void ToggleFeatures()
        {
            this.FeaturesVisible = !this.FeaturesVisible;
        }

        public void ToggleHeightmap()
        {
            this.HeightmapVisible = !this.HeightmapVisible;
        }

        public void ToggleMinimap()
        {
            this.MinimapVisible = !this.MinimapVisible;
        }

        public void OpenMapAttributes()
        {
            MapAttributesResult r = this.dialogService.AskUserForMapAttributes(this.GetAttributes());

            if (r != null)
            {
                this.UpdateAttributes(r);
            }
        }

        public void CloseMap()
        {
            if (this.CheckOkayDiscard())
            {
                this.Map = null;
            }
        }

        public void SetSeaLevel(int value)
        {
            if (this.SeaLevel == value)
            {
                return;
            }

            var op = new SetSealevelOperation(this.Map, value);

            SetSealevelOperation prevOp = null;
            if (this.undoManager.CanUndo && this.previousSeaLevelOpen)
            {
                prevOp = this.undoManager.PeekUndo() as SetSealevelOperation;
            }

            if (prevOp == null)
            {
                this.undoManager.Execute(op);
            }
            else
            {
                op.Execute();
                var combinedOp = prevOp.Combine(op);
                this.undoManager.Replace(combinedOp);
            }

            this.previousSeaLevelOpen = true;
        }

        public void FlushSeaLevel()
        {
            this.previousSeaLevelOpen = false;
        }

        public void SetViewportCenterNormalized(double x, double y)
        {
            double extraX = 1.0 / (this.MapWidth - 1);
            double extraY = 4.0 / (this.MapHeight - 4);

            var rect = this.ViewportRectangle;

            x = Util.Clamp(x, rect.ExtentX, 1.0 + extraX - rect.ExtentX);
            y = Util.Clamp(y, rect.ExtentY, 1.0 + extraY - rect.ExtentY);

            rect.Center = new Vector2D(x, y);
            this.ViewportRectangle = rect;
        }

        private static void DeduplicateTiles(IGrid<Bitmap> tiles)
        {
            var len = tiles.Width * tiles.Height;
            for (int i = 0; i < len; i++)
            {
                tiles[i] = Globals.TileCache.GetOrAddBitmap(tiles[i]);
            }
        }

        private void PasteMapTile(IMapTile tile)
        {
            DeduplicateTiles(tile.TileGrid);
            this.PasteMapTileNoDeduplicate(tile);
        }

        private void PasteMapTileNoDeduplicate(IMapTile tile)
        {
            var normX = this.ViewportRectangle.CenterX;
            var normY = this.ViewportRectangle.CenterY;
            int x = (int)(this.MapWidth * normX);
            int y = (int)(this.MapHeight * normY);

            x -= tile.TileGrid.Width / 2;
            y -= tile.TileGrid.Height / 2;

            this.AddAndSelectTile(tile, x, y);
        }

        private void PasteMapTileNoDeduplicateTopLeft(IMapTile tile)
        {
            var normX = this.ViewportRectangle.MinX;
            var normY = this.ViewportRectangle.MinY;
            int x = (int)(this.MapWidth * normX);
            int y = (int)(this.MapHeight * normY);

            this.AddAndSelectTile(tile, x, y);
        }

        private void ReplaceHeightmap(Grid<int> heightmap)
        {
            if (this.Map == null)
            {
                return;
            }

            if (heightmap.Width != this.Map.Tile.HeightGrid.Width
                || heightmap.Height != this.Map.Tile.HeightGrid.Height)
            {
                throw new ArgumentException(
                    "Dimensions do not match map heightmap",
                    "heightmap");
            }

            var op = new CopyAreaOperation<int>(
                heightmap,
                this.Map.Tile.HeightGrid,
                0,
                0,
                0,
                0,
                heightmap.Width,
                heightmap.Height);
            this.undoManager.Execute(op);
        }

        private void UpdateAttributes(MapAttributesResult newAttrs)
        {
            this.undoManager.Execute(new ChangeAttributesOperation(this.Map, newAttrs));
        }

        private MapAttributesResult GetAttributes()
        {
            return MapAttributesResult.FromModel(this.Map);
        }

        private bool SaveHelper(string filename)
        {
            if (filename == null)
            {
                throw new ArgumentNullException("filename");
            }

            string extension = Path.GetExtension(filename).ToLowerInvariant();

            try
            {
                switch (extension)
                {
                    case ".tnt":
                        this.Save(filename);
                        return true;
                    case ".hpi":
                    case ".ufo":
                    case ".ccx":
                    case ".gpf":
                    case ".gp3":
                        this.SaveHpi(filename);
                        return true;
                    default:
                        this.dialogService.ShowError("Unrecognized file extension: " + extension);
                        return false;
                }
            }
            catch (IOException e)
            {
                this.dialogService.ShowError("Error saving map: " + e.Message);
                return false;
            }
        }

        private bool OpenMap(string filename)
        {
            string ext = Path.GetExtension(filename) ?? string.Empty;
            ext = ext.ToLowerInvariant();

            try
            {
                switch (ext)
                {
                    case ".hpi":
                    case ".ufo":
                    case ".ccx":
                    case ".gpf":
                    case ".gp3":
                        return this.OpenFromHapi(filename);
                    case ".tnt":
                        this.OpenTnt(filename);
                        return true;
                    case ".sct":
                        this.OpenSct(filename);
                        return true;
                    default:
                        this.dialogService.ShowError(string.Format("Mappy doesn't know how to open {0} files", ext));
                        return false;
                }
            }
            catch (IOException e)
            {
                this.dialogService.ShowError("IO error opening map: " + e.Message);
                return false;
            }
            catch (ParseException e)
            {
                this.dialogService.ShowError("Cannot open map: " + e.Message);
                return false;
            }
        }

        private bool OpenFromHapi(string filename)
        {
            List<string> maps;
            bool readOnly;

            using (HpiReader h = new HpiReader(filename))
            {
                maps = this.GetMapNames(h).ToList();
            }

            string mapName;
            switch (maps.Count)
            {
                case 0:
                    this.dialogService.ShowError("No maps found in " + filename);
                    return false;
                case 1:
                    mapName = maps.First();
                    readOnly = false;
                    break;
                default:
                    maps.Sort();
                    mapName = this.dialogService.AskUserToChooseMap(maps);
                    readOnly = true;
                    break;
            }

            if (mapName == null)
            {
                return false;
            }

            this.OpenHapi(filename, HpiPath.Combine("maps", mapName + ".tnt"), readOnly);
            return true;
        }

        private IEnumerable<string> GetMapNames(HpiReader hpi)
        {
            return hpi.GetFiles("maps")
                .Where(x => x.Name.EndsWith(".tnt", StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Name.Substring(0, x.Name.Length - 4));
        }

        private bool TryCopyToClipboard()
        {
            if (this.Map == null)
            {
                return false;
            }

            if (this.SelectedFeatures.Count > 0)
            {
                var id = this.SelectedFeatures.First();
                var inst = this.Map.GetFeatureInstance(id);
                var rec = new FeatureClipboardRecord(inst.FeatureName);
                Clipboard.SetData(DataFormats.Serializable, rec);
                return true;
            }

            if (this.SelectedTile.HasValue)
            {
                var tile = this.FloatingTiles[this.SelectedTile.Value].Item;
                Clipboard.SetData(DataFormats.Serializable, tile);
                return true;
            }

            return false;
        }

        private void PasteFeature(FeatureClipboardRecord feature)
        {
            var normX = this.ViewportRectangle.CenterX;
            var normY = this.ViewportRectangle.CenterY;
            int x = (int)(this.MapWidth * 32 * normX);
            int y = (int)(this.MapHeight * 32 * normY);

            this.DragDropFeature(feature.FeatureName, x, y);
        }

        private void AddAndSelectTile(IMapTile tile, int x, int y)
        {
            var floatingSection = new Positioned<IMapTile>(tile, new Point(x, y));
            var addOp = new AddFloatingTileOperation(this.Map, floatingSection);

            // Tile's index should always be 0,
            // because all other tiles are merged before adding this one.
            var index = 0;

            var selectOp = new SelectTileOperation(this.Map, index);
            var op = new CompositeOperation(
                OperationFactory.CreateDeselectAndMergeOperation(this.Map),
                addOp,
                selectOp);

            this.undoManager.Execute(op);
        }

        private Point? ScreenToHeightIndex(int x, int y)
        {
            return Util.ScreenToHeightIndex(this.Map.Tile.HeightGrid, new Point(x, y));
        }

        private void TranslateSection(int index, int x, int y)
        {
            this.TranslateSection(this.Map.FloatingTiles[index], x, y);
        }

        private void TranslateSection(Positioned<IMapTile> tile, int x, int y)
        {
            if (tile.Location.X + tile.Item.TileGrid.Width + x <= 0)
            {
                x = -tile.Location.X - (tile.Item.TileGrid.Width - 1);
            }

            if (tile.Location.Y + tile.Item.TileGrid.Height + y <= 0)
            {
                y = -tile.Location.Y - (tile.Item.TileGrid.Height - 1);
            }

            if (tile.Location.X + x >= this.MapWidth)
            {
                x = this.MapWidth - tile.Location.X - 1;
            }

            if (tile.Location.Y + x >= this.MapHeight)
            {
                y = this.MapHeight - tile.Location.Y - 1;
            }

            if (x == 0 && y == 0)
            {
                return;
            }

            MoveTileOperation newOp = new MoveTileOperation(tile, x, y);

            MoveTileOperation lastOp = null;
            if (this.undoManager.CanUndo)
            {
                lastOp = this.undoManager.PeekUndo() as MoveTileOperation;
            }

            if (this.previousTranslationOpen && lastOp != null && lastOp.Tile == tile)
            {
                newOp.Execute();
                this.undoManager.Replace(lastOp.Combine(newOp));
            }
            else
            {
                this.undoManager.Execute(new MoveTileOperation(tile, x, y));
            }

            this.previousTranslationOpen = true;
        }

        private bool TranslateFeatureBatch(ICollection<Guid> ids, int x, int y)
        {
            if (x == 0 && y == 0)
            {
                return true;
            }

            var coordSet = new HashSet<GridCoordinates>(ids.Select(i => this.Map.GetFeatureInstance(i).Location));

            // pre-move check to see if anything is in our way
            foreach (var item in coordSet)
            {
                var translatedPoint = new GridCoordinates(item.X + x, item.Y + y);

                if (translatedPoint.X < 0
                    || translatedPoint.Y < 0
                    || translatedPoint.X >= this.Map.FeatureGridWidth
                    || translatedPoint.Y >= this.Map.FeatureGridHeight)
                {
                    return false;
                }

                bool isBlocked = !coordSet.Contains(translatedPoint)
                    && this.Map.HasFeatureInstanceAt(translatedPoint.X, translatedPoint.Y);
                if (isBlocked)
                {
                    return false;
                }
            }

            var newOp = new BatchMoveFeatureOperation(this.Map, ids, x, y);

            BatchMoveFeatureOperation lastOp = null;
            if (this.undoManager.CanUndo)
            {
                lastOp = this.undoManager.PeekUndo() as BatchMoveFeatureOperation;
            }

            if (this.previousTranslationOpen && lastOp != null && lastOp.CanCombine(newOp))
            {
                newOp.Execute();
                this.undoManager.Replace(lastOp.Combine(newOp));
            }
            else
            {
                this.undoManager.Execute(newOp);
            }

            this.previousTranslationOpen = true;

            return true;
        }

        private void TranslateStartPosition(int i, int x, int y)
        {
            var startPos = this.Map.Attributes.GetStartPosition(i);

            if (startPos == null)
            {
                throw new ArgumentException("Start position " + i + " has not been placed");
            }

            this.TranslateStartPositionTo(i, startPos.Value.X + x, startPos.Value.Y + y);
        }

        private void TranslateStartPositionTo(int i, int x, int y)
        {
            var newOp = new ChangeStartPositionOperation(this.Map, i, new Point(x, y));

            ChangeStartPositionOperation lastOp = null;
            if (this.undoManager.CanUndo)
            {
                lastOp = this.undoManager.PeekUndo() as ChangeStartPositionOperation;
            }

            if (this.previousTranslationOpen && lastOp != null && lastOp.Index == i)
            {
                newOp.Execute();
                this.undoManager.Replace(lastOp.Combine(newOp));
            }
            else
            {
                this.undoManager.Execute(newOp);
            }

            this.previousTranslationOpen = true;
        }

        private void CanUndoChanged(object sender, EventArgs e)
        {
            this.FireChange("CanUndo");
        }

        private void CanRedoChanged(object sender, EventArgs e)
        {
            this.FireChange("CanRedo");
        }

        private void IsMarkedChanged(object sender, EventArgs e)
        {
            this.IsDirty = !this.undoManager.IsMarked;
        }

        private void TileOnHeightGridChanged(object sender, GridEventArgs e)
        {
            var h = this.BaseTileHeightChanged;
            if (h != null)
            {
                h(this, e);
            }
        }

        private void TileOnTileGridChanged(object sender, GridEventArgs e)
        {
            var h = this.BaseTileGraphicsChanged;
            if (h != null)
            {
                h(this, e);
            }
        }

        private void FloatingTilesOnListChanged(object sender, ListChangedEventArgs e)
        {
            var h = this.TilesChanged;
            if (h != null)
            {
                h(this, e);
            }
        }

        private void AttributesOnStartPositionChanged(object sender, StartPositionChangedEventArgs e)
        {
            var h = this.StartPositionChanged;
            if (h != null)
            {
                h(this, e);
            }
        }

        private void SelectedFeaturesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            this.FireChange("SelectedFeatures");

            this.UpdateCanCopy();
            this.UpdateCanCut();
        }

        private void BandboxBehaviourPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "BandboxRectangle":
                    this.FireChange("BandboxRectangle");
                    break;
            }
        }

        private void MapSelectedTileChanged(object sender, EventArgs eventArgs)
        {
            this.FireChange("SelectedTile");

            this.UpdateCanCopy();
            this.UpdateCanCut();
        }

        private void MapSelectedStartPositionChanged(object sender, EventArgs eventArgs)
        {
            this.FireChange("SelectedStartPosition");
        }

        private void UpdateCanCopy()
        {
            this.CanCopy = this.SelectedTile.HasValue || this.SelectedFeatures.Count > 0;
        }

        private void UpdateCanCut()
        {
            this.CanCut = this.SelectedTile.HasValue || this.SelectedFeatures.Count > 0;
        }

        private void MapOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            switch (propertyChangedEventArgs.PropertyName)
            {
                case "Minimap":
                    this.MinimapImage = this.Map.Minimap;
                    break;
                case "SeaLevel":
                    this.FireChange(propertyChangedEventArgs.PropertyName);
                    break;
            }
        }
    }
}
