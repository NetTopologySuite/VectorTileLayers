﻿using BruTile;
using BruTile.Cache;
using Mapsui.Styles;
using Mapsui.VectorTileLayers.Core.Primitives;
using RBush;
using System;
using System.Collections.Generic;

namespace Mapsui.VectorTileLayers.Core
{
    /// <summary>
    /// Feature for vector tiles
    /// </summary>
    /// <remarks>
    /// This is a dummy feature, because Mapsui is Feature oriented for drawing.
    /// If there would be a possibility to render/draw the whole Layer only by style, 
    /// then this feature isn't needed.
    /// </remarks>
    public class BackgroundTileFeature : IFeature
    {
        public IEnumerable<TileInfo> Tiles { get; set; }

        public object this[string key] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public ICollection<IStyle> Styles => null;

        public IEnumerable<string> Fields => null;

        public MRect Extent { get; set; }

        public IDictionary<IStyle, object> RenderedGeometry => null;

        public void CoordinateVisitor(Action<double, double, CoordinateSetter> visit)
        {
        }

        public void Dispose()
        {
        }
    }
}
