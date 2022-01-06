﻿using Mapsui.VectorTileLayer.Core.Primitives;

namespace Mapsui.VectorTileLayer.Core.Interfaces
{
    public interface IVectorSymbolStyler
    {
        bool HasIcon { get; }

        bool HasText { get; }

        Symbol CreateIconSymbol(MPoint point, TagsCollection tags, EvaluationContext context);

        Symbol CreateTextSymbol(MPoint point, TagsCollection tags, EvaluationContext context);

        Symbol CreateIconTextSymbol(MPoint point, TagsCollection tags, EvaluationContext context);

        Symbol CreatePathSymbols(VectorElement element, EvaluationContext context);
    }
}
