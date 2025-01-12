﻿using System;
using UnityEngine;

namespace DELTation.ToonRP.Shadows
{
    [Serializable]
    public struct ToonBlobShadowsSettings
    {
        public TextureSize AtlasSize;
        public BlobShadowsMode Mode;
        [Min(0f)]
        public float Saturation;
    }
}