﻿using BruTile;
using Mapsui.Layers;
using SkiaSharp;
using System.Collections.Generic;

namespace Mapsui.VectorTileLayer.MapboxGL
{
    /// <summary>
    /// Class holding all relevant data from the Mapbox GL Json Style File
    /// </summary>
    public class MGLStyleFile
    {
        public MGLStyleFile(string name, int version)
        {
            Name = name;
            Version = version;
        }

        /// <summary>
        /// Name of this style file
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Version of this style file
        /// </summary>
        public int Version { get; }

        /// <summary>
        /// Center of map as provided in the style file
        /// </summary>
        public MPoint Center { get; internal set; }

        /// <summary>
        /// Sources is a list of all IDrawableTileSources, that this style file provides
        /// </summary>
        public List<ITileSource> TileSources { get; } = new List<ITileSource>();

        /// <summary>
        /// List of all TileLayers, that this style file contains
        /// </summary>
        public List<TileLayer> TileLayers { get; } = new List<TileLayer>();

        /// <summary>
        /// SpriteAtlas containing all sprites of this style file
        /// </summary>
        public MGLSpriteAtlas SpriteAtlas { get; } = new MGLSpriteAtlas();

        /// <summary>
        /// GlyphAtlas containing all glyphs for this style file
        /// </summary>
        public object GlyphAtlas { get; internal set; }

        public Dictionary<string, SKTypeface> SpecialFonts { get; internal set; }
    }
}
