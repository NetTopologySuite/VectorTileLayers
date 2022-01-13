﻿using BruTile;
using BruTile.Predefined;
using Mapsui.Extensions;
using Mapsui.Fetcher;
using Mapsui.Layers;
using Mapsui.Logging;
using Mapsui.VectorTileLayers.Core;
using Mapsui.VectorTileLayers.Core.Extensions;
using Mapsui.VectorTileLayers.Core.Interfaces;
using Mapsui.VectorTileLayers.Core.Primitives;
using Mapsui.VectorTileLayers.Core.Styles;
using Mapsui.VectorTileLayers.Core.Utilities;
using Mapsui.VectorTileLayers.OpenMapTiles.Parser;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Mapsui.VectorTileLayers.OpenMapTiles
{
    public class OMTVectorTileLayer : BaseLayer, IAsyncDataFetcher, IDisposable
    {
        private const int TileSizeOfData = 4096;

        private readonly ITileSource _tileSource;
        private readonly Core.Utilities.RenderFetchStrategy _renderFetchStrategy;
        private readonly int _minExtraTiles;
        private readonly int _maxExtraTiles;
        private int _numberTilesNeeded;
        private readonly TileFetchDispatcher<VectorTile> _tileFetchDispatcher;
        private readonly VectorTileFeature[] _features = { new VectorTileFeature() };
        private readonly ITileDataParser _tileDataParser = new MGLTileParser();
        private VectorTileStyle _style;
        private TileSchema _schema;

        /// <summary>
        /// Create tile layer for given vector tile source
        /// </summary>
        /// <param name="tileSource">Tile source to use for this layer</param>
        /// <param name="minTiles">Minimum number of tiles to cache</param>
        /// <param name="maxTiles">Maximum number of tiles to cache</param>
        /// <param name="dataFetchStrategy">Strategy to get list of tiles for given extent</param>
        /// <param name="renderFetchStrategy"></param>
        /// <param name="minExtraTiles">Number of minimum extra tiles for memory cache</param>
        /// <param name="maxExtraTiles">Number of maximum extra tiles for memory cache</param>
        /// <param name="fetchTileAsFeature">Fetch tile as feature</param>
        // ReSharper disable once UnusedParameter.Local // Is public and won't break this now
        public OMTVectorTileLayer(IEnumerable<IVectorTileStyle> vectorStyles, ITileSource tileSource, int minTiles = 200, int maxTiles = 300,
            IDataFetchStrategy? dataFetchStrategy = null, int minExtraTiles = -1, int maxExtraTiles = -1, 
            Func<TileInfo, VectorTile>? fetchTileAsFeature = null)
        {
            _tileSource = tileSource ?? throw new ArgumentException($"{tileSource} can not be null");
            Name = _tileSource.Name;
            Extent = _tileSource.Schema?.Extent.ToMRect();

            _schema = new GlobalSphericalMercator(_tileSource.Schema.YAxis, _tileSource.Schema.Resolutions[0].Level, 24);
            
            // It is a vector tile layer, so we could go always down to zoom 24
            MinVisible = 24.ToResolution(); // _tileSource.Schema.Resolutions.Last().Value.UnitsPerPixel;
            MaxVisible = _tileSource.Schema.Resolutions.First().Value.UnitsPerPixel;

            //_resolutions = new List<double>(25);

            //for (var i = 0; i <= 24; i++)
            //    _resolutions.Add(i.ToResolution());

            MemoryCache = new BruTile.Cache.MemoryCache<VectorTile?>(minTiles, maxTiles);
            _features[0].Cache = MemoryCache;

            Style = new VectorTileStyle(0, 24, vectorStyles);
            
            Attribution.Text = _tileSource.Attribution?.Text;
            Attribution.Url = _tileSource.Attribution?.Url;
            
            dataFetchStrategy ??= new DataFetchStrategy(3);
            _renderFetchStrategy = new Core.Utilities.RenderFetchStrategy();
            _minExtraTiles = minExtraTiles;
            _maxExtraTiles = maxExtraTiles;
            _tileFetchDispatcher = new TileFetchDispatcher<VectorTile>(MemoryCache, _tileSource.Schema, fetchTileAsFeature ?? FetchTileAsVectorTile, dataFetchStrategy);
            _tileFetchDispatcher.DataChanged += TileFetchDispatcherOnDataChanged;
            _tileFetchDispatcher.PropertyChanged += TileFetchDispatcherOnPropertyChanged;
            
            Enabled = _tileSource != null;
        }

        /// <summary>
        /// TileSource</summary>
        public ITileSource TileSource => _tileSource;

        /// <summary>
        /// Tile size for this type of layer is always 512 x 512 (OpenMapTiles and Mapbox GL)
        /// </summary>
        public int TileSize { get; } = 512;

        /// <summary>
        /// Memory cache for this layer
        /// </summary>
        private BruTile.Cache.MemoryCache<VectorTile?> MemoryCache { get; }

        /// <inheritdoc />
        public override IReadOnlyList<double> Resolutions => _schema.Resolutions.Select(r => r.Value.UnitsPerPixel).ToList();

        /// <inheritdoc />
        public override IEnumerable<IFeature> GetFeatures(MRect extent, double resolution)
        {
            if (_tileSource?.Schema == null)
            {
                return Enumerable.Empty<IFeature>();
            }

            UpdateMemoryCacheMinAndMax();

            var zoomLevel = resolution.ToZoomLevel();

            //IEnumerable<TileInfo> tiles;

            //if (zoomLevel <= _tileSource.Schema.Resolutions.Last().Key)
            //{
            //    tiles = _tileSource.Schema.GetTileInfos(extent.ToExtent(), (int)resolution.ToZoomLevel());
            //}
            //else 
            //{
            //    tiles = _schema.GetTileInfos(extent.ToExtent(), (int)resolution.ToZoomLevel());
            //}

            //foreach (var tile in tiles)
            //{
            //    if (MemoryCache.Find(tile.Index) == null)
            //    {

            //    }
            //}

            //_features[0].Tiles = tiles;
            // Get all tiles, that fill this extent. If one tile isn't loaded, then use one from a lower zoom level 
            //var schema = new BruTile.Predefined.GlobalSphericalMercator(_tileSource.Schema.Resolutions[0].0, 24);

            //_features[0].Tiles = schema.GetTileInfos(extent.ToExtent(), (int)resolution.ToZoomLevel());

            //            _features[0].Tiles = _tileSource.Schema.GetTileInfos(extent.ToExtent(), (int)resolution.ToZoomLevel());

            var tiles = new List<TileInfo>();

            foreach (var vectorTile in _renderFetchStrategy.Get<VectorTile>(extent, resolution, _tileSource.Schema, MemoryCache))
            {
                tiles.Add(vectorTile.TileInfo);
            }

            _features[0].Tiles = tiles;

            return _features;
        }

        /// <inheritdoc />
        public void AbortFetch()
        {
            _tileFetchDispatcher.StopFetching();
        }

        /// <inheritdoc />
        public void ClearCache()
        {
            MemoryCache.Clear();
        }

        /// <inheritdoc />
        public override void RefreshData(FetchInfo fetchInfo)
        {
            if (Enabled
                && fetchInfo.Extent?.GetArea() > 0
                && MaxVisible >= fetchInfo.Resolution
                && MinVisible <= fetchInfo.Resolution)
            {
                _tileFetchDispatcher.SetViewport(fetchInfo);
                _tileFetchDispatcher.StartFetching();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                MemoryCache.Dispose();
            }

            base.Dispose(disposing);
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void TileFetchDispatcherOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            if (propertyChangedEventArgs.PropertyName == nameof(Busy))
            {
                Busy = _tileFetchDispatcher.Busy;
                
                // Check, if Busy is false. Then start with layout process of symbols
                if (!Busy)
                {
                    var temp = true;
                }
            }
        }

        private void UpdateMemoryCacheMinAndMax()
        {
            if (_minExtraTiles < 0 || _maxExtraTiles < 0) return;
            if (_numberTilesNeeded == _tileFetchDispatcher.NumberTilesNeeded) return;

            _numberTilesNeeded = _tileFetchDispatcher.NumberTilesNeeded;
            MemoryCache.MinTiles = _numberTilesNeeded + _minExtraTiles;
            MemoryCache.MaxTiles = _numberTilesNeeded + _maxExtraTiles;
        }

        private void TileFetchDispatcherOnDataChanged(object sender, DataChangedEventArgs e)
        {
            OnDataChanged(e);
        }

        public VectorTile? FetchTileAsVectorTile(TileInfo tileInfo)
        {
            if (_tileSource == null)
                return null;

            var vectorTile = MemoryCache.Find(tileInfo.Index);

            if (vectorTile != null)
                return vectorTile;

            // We don't create tiles with zoom higher than TileSource could provide
            //if (tileInfo.Index.Level > _tileSource.Schema.Resolutions.Count - 1)
            //    return null;

            // Get binary data from tile source as byte[]
            var (tileData, overzoom) = GetTileData(tileInfo);

            // We find data in the tile source for this tile, perhaps on lower levels
            if (tileData != null) // && overzoom == Overzoom.None)
                vectorTile = ToVectorTile(tileInfo, overzoom, ref tileData);

            MemoryCache.Add(tileInfo.Index, vectorTile);

            return vectorTile;
        }

        /// <summary>
        /// Get data for tile
        /// </summary>
        /// <remarks>
        /// If this tile couldn't be found, than we try to get tile data for a tile with lower zoom level
        /// </remarks>
        /// <param name="tileInfo">Tile info for tile to get data for</param>
        /// <returns>Raw tile data, factor for enlargement for this data and offsets for parts of this data, which to use</returns>
        private (byte[], Overzoom) GetTileData(TileInfo tileInfo)
        {
            var zoom = tileInfo.Index.Level;
            var scale = 1;
            var offsetX = 0f;
            var offsetY = 0f;
            var offsetFactor = TileSizeOfData;

            // Check MinZoom of source. MaxZoom isn't checked, because of overzoom
            if (zoom < 0)
                return (null, Overzoom.None);

            Logger.Log(Logging.LogLevel.Information, $"Before GetTile from source at {DateTime.Now.Ticks}: {tileInfo.Index.Col}/{tileInfo.Index.Row}/{tileInfo.Index.Level}");

            // Get byte data for this tile
            var tileData = _tileSource.GetTile(tileInfo);

            Logger.Log(Logging.LogLevel.Information, $"After GetTile from source at {DateTime.Now.Ticks}: {tileInfo.Index.Col}/{tileInfo.Index.Row}/{tileInfo.Index.Level}");

            if (tileData != null)
                return (tileData, Overzoom.None);

            // We only create overzoom tiles when zoom is between min and max zoom
            //if (zoom < MinZoom || zoom > MaxZoom)
            //    return (null, Overzoom.None);

            var info = new TileInfo { Index = new TileIndex(tileInfo.Index.Col, tileInfo.Index.Row, tileInfo.Index.Level) };
            var row = info.Index.Row;
            var col = info.Index.Col;

            while (tileData == null && zoom >= 0)
            {
                scale <<= 1;
                offsetX = offsetX + (col % 2) * offsetFactor;
                offsetY = offsetY + (row % 2) * offsetFactor * (_tileSource.Schema.YAxis == YAxis.TMS ? +1f : -1f);
                zoom--;
                row >>= 1;
                col >>= 1;
                offsetFactor <<= 1;
                //info.Extent = new Extent(minX, minY, minX + halfWidth, minY + halfHeight);
                info.Index = new TileIndex(col, row, zoom);
                tileData = _tileSource.GetTile(info);
            }

            if (zoom < 0)
                return (null, Overzoom.None);

            offsetY = offsetFactor - offsetY + (_tileSource.Schema.YAxis == YAxis.TMS ? -TileSizeOfData : 0f);

            var overzoom = new Overzoom(scale, offsetX, offsetY);

            return (tileData, overzoom);
        }

        private VectorTile? ToVectorTile(TileInfo tileInfo, Overzoom overzoom, ref byte[]? tileData)
        {
            // A TileSource may return a byte array that is null. This is currently only implemented
            // for MbTilesTileSource. It is to indicate that the tile is not present in the source,
            // although it should be given the tile schema. It does not mean the tile could not
            // be accessed because of some temporary reason. In that case it will throw an exception.
            // For Mapsui this is important because it will not try again and again to fetch it. 
            // Here we return the geometry as null so that it will be added to the tile cache. 
            // TileLayer.GetFeatureInView will have to return only the non null geometries.

            if (tileData == null)
                return null;

            var sink = new VectorTile(tileInfo, TileSize, TileSizeOfData, Style);

            // Parse tile and convert it to a feature list
            Stream stream = new MemoryStream(tileData);

            if (IsGZipped(stream))
                stream = new GZipStream(stream, CompressionMode.Decompress);

            try
            {
                _tileDataParser.Parse(tileInfo, stream, sink, overzoom, new TileClipper(new MRect(-8, -8, TileSize + 8, TileSize + 8)));
            }
            catch (Exception e)
            {
                Logger.Log(LogLevel.Error, $"Exception while parsing tile {tileInfo.Index.Col}/{tileInfo.Index.Row}/{tileInfo.Index.Level}", e);
            }

            return sink;
        }

        /// <summary>
        /// Check, if stream contains gzipped data 
        /// </summary>
        /// <param name="stream">Stream to check</param>
        /// <returns>True, if the stream is gzipped</returns>
        private static bool IsGZipped(Stream stream)
        {
            return IsZipped(stream, 3, "1F-8B-08");
        }

        /// <summary>
        /// Check, if stream contains zipped data
        /// </summary>
        /// <param name="stream">Stream to check</param>
        /// <param name="signatureSize">Length of bytes to check for signature</param>
        /// <param name="expectedSignature">Signature to check</param>
        /// <returns>True, if the stream is zipped</returns>
        private static bool IsZipped(Stream stream, int signatureSize = 4, string expectedSignature = "50-4B-03-04")
        {
            if (stream.Length < signatureSize)
                return false;

            byte[] signature = new byte[signatureSize];
            int bytesRequired = signatureSize;
            int index = 0;

            while (bytesRequired > 0)
            {
                int bytesRead = stream.Read(signature, index, bytesRequired);
                bytesRequired -= bytesRead;
                index += bytesRead;
            }

            stream.Seek(0, SeekOrigin.Begin);

            string actualSignature = BitConverter.ToString(signature);
            if (actualSignature == expectedSignature)
                return true;

            return false;
        }
    }
}
