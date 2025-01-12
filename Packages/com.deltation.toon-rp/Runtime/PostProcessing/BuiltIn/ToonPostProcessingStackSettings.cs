﻿using System;
using DELTation.ToonRP.Attributes;

namespace DELTation.ToonRP.PostProcessing.BuiltIn
{
    [Serializable]
    public struct ToonPostProcessingStackSettings
    {
        private const int HeaderSize = 12;

        [ToonRpHeader("FXAA", Size = HeaderSize)]
        public ToonFxaaSettings Fxaa;

        [ToonRpHeader("Tone Mapping", Size = HeaderSize)]
        public ToonToneMappingSettings ToneMapping;

        [ToonRpHeader("Vignette", Size = HeaderSize)]
        public ToonVignetteSettings Vignette;

        [ToonRpHeader("LUT", Size = HeaderSize)]
        public ToonLookupTableSettings LookupTable;

        [ToonRpHeader("Film Grain", Size = HeaderSize)]
        public ToonFilmGrainSettings FilmGrain;
    }
}