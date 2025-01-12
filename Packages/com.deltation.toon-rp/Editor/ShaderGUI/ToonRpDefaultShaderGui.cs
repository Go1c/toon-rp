﻿using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DELTation.ToonRP.Editor.ShaderGUI
{
    [UsedImplicitly]
    public sealed class ToonRpDefaultShaderGui : ToonRpShaderGuiBase
    {
        private static readonly int ShadowColorId = Shader.PropertyToID(PropertyNames.ShadowColorPropertyName);
        private static readonly int SpecularColorId = Shader.PropertyToID(PropertyNames.SpecularColorPropertyName);
        private static readonly int RimColorId = Shader.PropertyToID(PropertyNames.RimColorPropertyName);
        private static readonly int NormalMapId = Shader.PropertyToID(PropertyNames.NormalMapPropertyName);

        public override bool OutlinesStencilLayer => true;

        protected override void DrawProperties()
        {
            DrawSurfaceProperties();

            EditorGUILayout.Space();

            if (DrawFoldout(HeaderNames.Color))
            {
                DrawProperty(PropertyNames.MainColor);
                DrawProperty(PropertyNames.MainTexture);
            }


            EditorGUILayout.Space();

            if (DrawFoldout(HeaderNames.Lighting))
            {
                DrawProperty(PropertyNames.ShadowColorPropertyName);
                DrawProperty(PropertyNames.SpecularColorPropertyName);
                DrawProperty(PropertyNames.RimColorPropertyName);
                DrawProperty(PropertyNames.EmissionColor);
                DrawNormalMap();
                DrawProperty("_ReceiveBlobShadows");
                DrawOverrideRamp();
            }

            EditorGUILayout.Space();

            DrawButtons();
        }

        private void DrawOverrideRamp()
        {
            DrawProperty("_OverrideRamp", out MaterialProperty overrideRamp);
            if (overrideRamp.floatValue == 0)
            {
                return;
            }

            EditorGUI.indentLevel++;
            DrawProperty("_OverrideRamp_Threshold");
            DrawProperty("_OverrideRamp_Smoothness");
            DrawProperty("_OverrideRamp_SpecularThreshold");
            DrawProperty("_OverrideRamp_SpecularSmoothness");
            DrawProperty("_OverrideRamp_RimThreshold");
            DrawProperty("_OverrideRamp_RimSmoothness");
            EditorGUI.indentLevel--;
        }

        protected override void OnSetZWrite(bool zWrite)
        {
            base.OnSetZWrite(zWrite);
            ForEachMaterial(UpdateStencil);
        }


        private void DrawNormalMap()
        {
            if (DrawProperty(PropertyNames.NormalMapPropertyName))
            {
                OnNormalMapUpdated();
            }
        }

        private void OnNormalMapUpdated()
        {
            ForEachMaterial(m => m.SetKeyword(ShaderKeywords.NormalMap, m.GetTexture(NormalMapId) != null));
        }

        protected override RenderQueue GetRenderQueue(Material m) => GetRenderQueueWithAlphaTestAndTransparency(m);

        private void DrawButtons()
        {
            if (!GUILayout.Button("Disable Lighting"))
            {
                return;
            }

            Undo.RecordObjects(Targets, "Disable Lighting");

            ForEachMaterial(m =>
                {
                    m.SetColor(PropertyNames.EmissionColor, Color.black);
                    m.SetColor(ShadowColorId, Color.clear);
                    m.SetColor(SpecularColorId, Color.black);
                    m.SetColor(RimColorId, Color.black);
                    m.SetTexture(NormalMapId, null);
                    EditorUtility.SetDirty(m);
                }
            );

            OnNormalMapUpdated();
        }
    }
}