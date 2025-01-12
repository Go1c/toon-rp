﻿using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.ToonRP.Editor.ShaderGUI
{
    [UsedImplicitly]
    public sealed class ToonRpParticlesUnlitShaderGui : ToonRpShaderGuiBase
    {
        protected override void DrawProperties()
        {
            DrawSurfaceProperties();

            EditorGUILayout.Space();

            if (DrawFoldout(HeaderNames.Color))
            {
                DrawProperty(PropertyNames.MainColor);
                DrawProperty(PropertyNames.MainTexture);
                DrawProperty(PropertyNames.EmissionColor);
            }
        }

        protected override RenderQueue GetRenderQueue(Material m) => GetRenderQueueWithAlphaTestAndTransparency(m);
    }
}