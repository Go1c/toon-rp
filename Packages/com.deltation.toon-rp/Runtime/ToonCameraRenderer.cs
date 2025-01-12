﻿using DELTation.ToonRP.Extensions;
using DELTation.ToonRP.PostProcessing;
using DELTation.ToonRP.Shadows;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace DELTation.ToonRP
{
    public sealed partial class ToonCameraRenderer
    {
        private const string DefaultCmdName = "Render Camera";
        public static readonly ShaderTagId[] ShaderTagIds =
        {
            new("ToonRPForward"),
            new("SRPDefaultUnlit"),
        };

        private static readonly int CameraColorBufferId = Shader.PropertyToID("_ToonRP_CameraColorBuffer");
        private static readonly int PostProcessingSourceId = Shader.PropertyToID("_ToonRP_PostProcessingSource");
        private static readonly int CameraDepthBufferId = Shader.PropertyToID("_ToonRP_CameraDepthBuffer");
        private static readonly int ScreenParamsId = Shader.PropertyToID("_ToonRP_ScreenParams");
        private static readonly int UnityMatrixInvPId = Shader.PropertyToID("unity_MatrixInvP");
        private readonly DepthPrePass _depthPrePass = new();
        private readonly ToonRenderingExtensionsCollection _extensionsCollection = new();
        private readonly CommandBuffer _finalBlitCmd = new() { name = "Final Blit" };
        private readonly ToonGlobalRamp _globalRamp = new();
        private readonly ToonLighting _lighting = new();
        private readonly ToonPostProcessing _postProcessing = new();
        private readonly ToonShadows _shadows = new();

        private Camera _camera;

        private string _cmdName = DefaultCmdName;
        private RenderTextureFormat _colorFormat;
        private ScriptableRenderContext _context;
        private CullingResults _cullingResults;
        private DepthPrePassMode _depthPrePassMode;
        private GraphicsFormat _depthStencilFormat;
        private ToonRenderingExtensionContext _extensionContext;
        private int _msaaSamples;
        private bool _renderToTexture;
        private int _rtHeight;
        private int _rtWidth;
        private ToonCameraRendererSettings _settings;

        public static DepthPrePassMode GetOverrideDepthPrePassMode(in ToonCameraRendererSettings settings,
            in ToonPostProcessingSettings postProcessingSettings,
            in ToonRenderingExtensionSettings extensionSettings)
        {
            DepthPrePassMode mode = settings.DepthPrePass;

            if (postProcessingSettings.Passes != null)
            {
                foreach (ToonPostProcessingPassAsset pass in postProcessingSettings.Passes)
                {
                    if (pass == null)
                    {
                        continue;
                    }

                    mode = DepthPrePassModeUtils.CombineDepthPrePassModes(mode, pass.RequiredDepthPrePassMode());
                }
            }

            if (extensionSettings.Extensions != null)
            {
                foreach (ToonRenderingExtensionAsset extension in extensionSettings.Extensions)
                {
                    if (extension == null)
                    {
                        continue;
                    }

                    mode = DepthPrePassModeUtils.CombineDepthPrePassModes(mode, extension.RequiredDepthPrePassMode());
                }
            }

            return mode;
        }

        public void Render(ScriptableRenderContext context, Camera camera, in ToonCameraRendererSettings settings,
            in ToonRampSettings globalRampSettings, in ToonShadowSettings toonShadowSettings,
            in ToonPostProcessingSettings postProcessingSettings,
            in ToonRenderingExtensionSettings extensionSettings)
        {
            _context = context;
            _camera = camera;
            _settings = settings;

            CommandBuffer cmd = CommandBufferPool.Get();
            PrepareBufferName();
            cmd.BeginSample(_cmdName);

            PrepareMsaa(camera);
            PrepareForSceneWindow();

            if (!Cull(toonShadowSettings))
            {
                return;
            }

            _depthPrePassMode = GetOverrideDepthPrePassMode(settings, postProcessingSettings, extensionSettings);
            _postProcessing.UpdatePasses(camera, postProcessingSettings);
            Setup(cmd, globalRampSettings, toonShadowSettings, extensionSettings);
            _extensionsCollection.Update(extensionSettings);
            _extensionsCollection.Setup(_extensionContext);
            _postProcessing.Setup(_context, postProcessingSettings, _settings, _colorFormat, _camera, _rtWidth,
                _rtHeight
            );

            if (_depthPrePassMode != DepthPrePassMode.Off)
            {
                _extensionsCollection.RenderEvent(ToonRenderingEvent.BeforeDepthPrepass);
                _depthPrePass.Setup(_context, _cullingResults, _camera, settings, _depthPrePassMode, _rtWidth, _rtHeight
                );
                _depthPrePass.Render();
                _extensionsCollection.RenderEvent(ToonRenderingEvent.AfterDepthPrepass);
            }

            using (new ProfilingScope(cmd, NamedProfilingSampler.Get(ToonRpPassId.PrepareRenderTargets)))
            {
                SetRenderTargets(cmd);
                ClearRenderTargets(cmd);
            }

            DrawVisibleGeometry(cmd);
            DrawUnsupportedShaders();
            DrawGizmosPreImageEffects();

            if (_postProcessing.AnyFullScreenEffectsEnabled)
            {
                RenderPostProcessing(cmd);
            }
            else
            {
                BlitToCameraTarget();
            }

            DrawGizmosPostImageEffects();

            Cleanup(cmd);
            Submit(cmd);
            CommandBufferPool.Release(cmd);
        }

        private void SetRenderTargets(CommandBuffer cmd)
        {
            if (_renderToTexture)
            {
                cmd.SetRenderTarget(
                    CameraColorBufferId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                    CameraDepthBufferId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
                );
            }
            else
            {
                cmd.SetRenderTarget(
                    BuiltinRenderTextureType.CameraTarget, RenderBufferLoadAction.DontCare,
                    RenderBufferStoreAction.Store
                );
            }

            ExecuteBuffer(cmd);
        }


        private void PrepareMsaa(Camera camera)
        {
            _msaaSamples = (int) _settings.Msaa;
            QualitySettings.antiAliasing = _msaaSamples;
            // QualitySettings.antiAliasing returns 0 if MSAA is not supported
            _msaaSamples = Mathf.Max(QualitySettings.antiAliasing, 1);
            _msaaSamples = camera.allowMSAA ? _msaaSamples : 1;
        }

        partial void PrepareBufferName();

        partial void PrepareForSceneWindow();

        private bool Cull(in ToonShadowSettings toonShadowSettings)
        {
            if (!_camera.TryGetCullingParameters(out ScriptableCullingParameters parameters))
            {
                return false;
            }

            if (toonShadowSettings.Mode == ToonShadowSettings.ShadowMode.Vsm)
            {
                parameters.shadowDistance = Mathf.Min(toonShadowSettings.MaxDistance, _camera.farClipPlane);
            }

            _cullingResults = _context.Cull(ref parameters);
            return true;
        }

        private void Setup(CommandBuffer cmd, in ToonRampSettings globalRampSettings,
            in ToonShadowSettings toonShadowSettings, in ToonRenderingExtensionSettings extensionSettings)
        {
            SetupLighting(cmd, globalRampSettings, toonShadowSettings);

            _context.SetupCameraProperties(_camera);
            Matrix4x4 gpuProjectionMatrix =
                GL.GetGPUProjectionMatrix(_camera.projectionMatrix, SystemInfo.graphicsUVStartsAtTop);
            cmd.SetGlobalMatrix(UnityMatrixInvPId, Matrix4x4.Inverse(gpuProjectionMatrix));

            float renderScale = _camera.cameraType == CameraType.Game ? _settings.RenderScale : 1.0f;
            int maxRtWidth = int.MaxValue;
            int maxRtHeight = int.MaxValue;
            if (_camera.cameraType == CameraType.Game)
            {
                if (_settings.MaxRenderTextureWidth > 0)
                {
                    maxRtWidth = _settings.MaxRenderTextureWidth;
                }

                if (_settings.MaxRenderTextureHeight > 0)
                {
                    maxRtHeight = _settings.MaxRenderTextureHeight;
                }
            }

            _rtWidth = _camera.pixelWidth;
            _rtHeight = _camera.pixelHeight;

            _renderToTexture = _settings.AllowHdr || _msaaSamples > 1 ||
                               _postProcessing.AnyFullScreenEffectsEnabled ||
                               !Mathf.Approximately(renderScale, 1.0f) ||
                               _rtWidth > maxRtWidth ||
                               _rtHeight > maxRtHeight
                ;
            _colorFormat = _settings.AllowHdr ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
            bool requireStencil = RequireStencil(extensionSettings);
            _depthStencilFormat = requireStencil ? GraphicsFormat.D24_UNorm_S8_UInt : GraphicsFormat.D24_UNorm;

            if (_renderToTexture)
            {
                _rtWidth = Mathf.CeilToInt(_rtWidth * renderScale);
                _rtHeight = Mathf.CeilToInt(_rtHeight * renderScale);
                float aspectRatio = (float) _rtWidth / _rtHeight;

                if (_rtWidth > maxRtWidth || _rtHeight > maxRtHeight)
                {
                    _rtWidth = maxRtWidth;
                    _rtHeight = maxRtHeight;

                    if (aspectRatio > 1)
                    {
                        _rtHeight = Mathf.CeilToInt(_rtWidth / aspectRatio);
                    }
                    else
                    {
                        _rtWidth = Mathf.CeilToInt(_rtHeight * aspectRatio);
                    }
                }

                cmd.GetTemporaryRT(
                    CameraColorBufferId, _rtWidth, _rtHeight, 0,
                    _settings.RenderTextureFilterMode, _colorFormat,
                    RenderTextureReadWrite.Default, _msaaSamples
                );

                var depthDesc = new RenderTextureDescriptor(_rtWidth, _rtHeight,
                    GraphicsFormat.None, _depthStencilFormat,
                    0
                )
                {
                    msaaSamples = _msaaSamples,
                };
                cmd.GetTemporaryRT(CameraDepthBufferId, depthDesc, FilterMode.Point);
            }

            ExecuteBuffer(cmd);

            _extensionContext =
                new ToonRenderingExtensionContext(_context, _camera, _settings, _cullingResults, _rtWidth, _rtHeight);
        }

        private bool RequireStencil(in ToonRenderingExtensionSettings extensionSettings)
        {
            if (_settings.Stencil)
            {
                return true;
            }

            if (extensionSettings.Extensions == null)
            {
                return false;
            }

            foreach (ToonRenderingExtensionAsset extension in extensionSettings.Extensions)
            {
                if (extension == null || !extension.RequiresStencil())
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private void SetupLighting(CommandBuffer cmd, ToonRampSettings globalRampSettings,
            ToonShadowSettings shadowSettings)
        {
            ExecuteBuffer(cmd);

            _globalRamp.Setup(_context, globalRampSettings);

            VisibleLight visibleLight =
                _cullingResults.visibleLights.Length > 0 ? _cullingResults.visibleLights[0] : default;
            _lighting.Setup(_context, visibleLight.light);

            {
                _shadows.Setup(_context, _cullingResults, shadowSettings, _camera);
                _shadows.Render(visibleLight.light);
            }
        }

        private void ClearRenderTargets(CommandBuffer cmd)
        {
            const string sampleName = "Clear Render Targets";

            cmd.BeginSample(sampleName);

            CameraClearFlags cameraClearFlags = _camera.clearFlags;
            bool clearDepth = cameraClearFlags <= CameraClearFlags.Depth;
            bool clearColor;
            Color backgroundColor;

#if UNITY_EDITOR
            if (_camera.cameraType == CameraType.Preview)
            {
                clearColor = true;
                backgroundColor = Color.black;
                backgroundColor.r = backgroundColor.g = backgroundColor.b = 0.25f;
            }
            else
#endif // UNITY_EDITOR
            {
                clearColor = cameraClearFlags == CameraClearFlags.Color;
                backgroundColor = clearColor ? _camera.backgroundColor.linear : Color.clear;
            }

            cmd.ClearRenderTarget(clearDepth, clearColor, backgroundColor);

            cmd.EndSample(sampleName);
            ExecuteBuffer(cmd);
        }

        private void RenderPostProcessing(CommandBuffer cmd)
        {
            int sourceId;
            if (_msaaSamples > 1)
            {
                using (new ProfilingScope(cmd, NamedProfilingSampler.Get(ToonRpPassId.ResolveCameraColor)))
                {
                    cmd.GetTemporaryRT(
                        PostProcessingSourceId, _camera.pixelWidth, _camera.pixelHeight, 0,
                        _settings.RenderTextureFilterMode, _colorFormat,
                        RenderTextureReadWrite.Default
                    );
                    cmd.Blit(CameraColorBufferId, PostProcessingSourceId);
                }

                ExecuteBuffer(cmd);
                sourceId = PostProcessingSourceId;
            }
            else
            {
                sourceId = CameraColorBufferId;
            }

            ExecuteBuffer(cmd);

            _extensionsCollection.RenderEvent(ToonRenderingEvent.BeforePostProcessing);
            _postProcessing.RenderFullScreenEffects(
                _rtWidth, _rtHeight, _colorFormat,
                sourceId, BuiltinRenderTextureType.CameraTarget
            );
            _extensionsCollection.RenderEvent(ToonRenderingEvent.AfterPostProcessing);
        }


        private void BlitToCameraTarget()
        {
            if (_renderToTexture)
            {
                _finalBlitCmd.Blit(CameraColorBufferId, BuiltinRenderTextureType.CameraTarget);
                ExecuteBuffer(_finalBlitCmd);
            }
        }

        private void Cleanup(CommandBuffer cmd)
        {
            _shadows.Cleanup();

            if (_depthPrePassMode != DepthPrePassMode.Off)
            {
                _depthPrePass.Cleanup();
            }

            _extensionsCollection.Cleanup();
            _postProcessing.Cleanup();

            if (_renderToTexture)
            {
                cmd.ReleaseTemporaryRT(CameraColorBufferId);
                cmd.ReleaseTemporaryRT(CameraDepthBufferId);
            }

            ExecuteBuffer(cmd);
        }

        private void Submit(CommandBuffer cmd)
        {
            cmd.EndSample(_cmdName);
            ExecuteBuffer(cmd);
            _context.Submit();
        }

        private void ExecuteBuffer(CommandBuffer cmd)
        {
            _context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        private void DrawVisibleGeometry(CommandBuffer cmd)
        {
            cmd.SetGlobalVector(ScreenParamsId, new Vector4(
                    1.0f / _rtWidth,
                    1.0f / _rtHeight,
                    _rtWidth,
                    _rtHeight
                )
            );
            ExecuteBuffer(cmd);

            {
                _extensionsCollection.RenderEvent(ToonRenderingEvent.BeforeOpaque);

                using (new ProfilingScope(cmd, NamedProfilingSampler.Get(ToonRpPassId.OpaqueGeometry)))
                {
                    ExecuteBuffer(cmd);
                    DrawGeometry(RenderQueueRange.opaque);
                }

                ExecuteBuffer(cmd);

                _extensionsCollection.RenderEvent(ToonRenderingEvent.AfterOpaque);
            }

            _extensionsCollection.RenderEvent(ToonRenderingEvent.BeforeSkybox);
            _context.DrawSkybox(_camera);
            _extensionsCollection.RenderEvent(ToonRenderingEvent.AfterSkybox);

            {
                _extensionsCollection.RenderEvent(ToonRenderingEvent.BeforeTransparent);

                using (new ProfilingScope(cmd, NamedProfilingSampler.Get(ToonRpPassId.TransparentGeometry)))
                {
                    ExecuteBuffer(cmd);
                    DrawGeometry(RenderQueueRange.transparent);
                }

                ExecuteBuffer(cmd);

                _extensionsCollection.RenderEvent(ToonRenderingEvent.AfterTransparent);
            }
        }

        private void DrawGeometry(RenderQueueRange renderQueueRange)
        {
            var sortingSettings = new SortingSettings(_camera)
            {
                criteria = SortingCriteria.CommonOpaque,
            };
            var drawingSettings = new DrawingSettings(ShaderTagIds[0], sortingSettings)
            {
                enableDynamicBatching = _settings.UseDynamicBatching,
                perObjectData = PerObjectData.LightProbe,
            };

            for (int i = 0; i < ShaderTagIds.Length; i++)
            {
                drawingSettings.SetShaderPassName(i, ShaderTagIds[i]);
            }

            var filteringSettings = new FilteringSettings(renderQueueRange);

            _context.DrawRenderers(_cullingResults, ref drawingSettings, ref filteringSettings);
        }

        partial void DrawGizmosPreImageEffects();
        partial void DrawGizmosPostImageEffects();

        partial void DrawUnsupportedShaders();
    }
}