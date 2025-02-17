﻿using Mapsui.VectorTileLayers.Core.Interfaces;

namespace Mapsui.VectorTileLayers.Core.Filter
{
    public class GreaterThanFilter : BinaryFilter
    {
        public GreaterThanFilter(string key, object value) : base(key, value)
        {
        }

        public override bool Evaluate(IVectorElement feature)
        {
            if (feature == null || !feature.Tags.ContainsKey(Key))
                return false;

            if (feature.Tags[Key] is float)
                return (float)feature.Tags[Key] > (float)Value;

            if (feature.Tags[Key] is long)
                return (long)feature.Tags[Key] > (long)Value;

            return false;
        }
    }
}
