﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using JetBrains.Annotations;

namespace DELTation.ToonRP.Extensions
{
    public sealed class ToonRenderingExtensionsCollection : IToonRenderingExtensionSettingsStorage
    {
        private static readonly int EventsCount;
        [ItemCanBeNull]
        private readonly List<IToonRenderingExtension>[] _extensions = new List<IToonRenderingExtension>[EventsCount];
        private readonly List<IToonRenderingExtension>[] _filteredExtensions =
            new List<IToonRenderingExtension>[EventsCount];
        private readonly Dictionary<IToonRenderingExtension, ToonRenderingExtensionAsset> _sourceAssets = new();
        private ToonRenderingExtensionContext _context;

        private bool _initialized;

        static ToonRenderingExtensionsCollection() => EventsCount = Enum.GetValues(typeof(ToonRenderingEvent)).Length;

        public TSettings GetSettings<TSettings>(IToonRenderingExtension extension)
        {
            if (extension == null)
            {
                throw new ArgumentNullException(nameof(extension));
            }

            if (!_sourceAssets.TryGetValue(extension, out ToonRenderingExtensionAsset sourceAsset))
            {
                throw new ArgumentException(
                    $"The provided extension of type {extension.GetType()} is not part of this settings provider.",
                    nameof(extension)
                );
            }

            if (sourceAsset is not ToonRenderingExtensionAsset<TSettings> castedSourceAsset)
            {
                throw new ArgumentException(
                    $"The provided extension of type {extension.GetType()} is linked to an asset, but it does not store settings of type {typeof(TSettings)}.",
                    nameof(extension)
                );
            }

            return castedSourceAsset.Settings;
        }

        public void Update(in ToonRenderingExtensionSettings settings)
        {
            CheckForReset();

            if (_initialized)
            {
                return;
            }

            _sourceAssets.Clear();

            foreach (List<IToonRenderingExtension> extensions in _extensions)
            {
                extensions?.Clear();
            }

            if (settings.Extensions != null)
            {
                foreach (ToonRenderingExtensionAsset extensionAsset in settings.Extensions)
                {
                    if (extensionAsset == null)
                    {
                        continue;
                    }

                    IToonRenderingExtension renderingExtension = extensionAsset.CreateExtension();
                    AddExtension(extensionAsset.Event, renderingExtension);
                    _sourceAssets[renderingExtension] = extensionAsset;
                }
            }

            _initialized = true;
        }

        [Conditional("UNITY_EDITOR")]
        private void CheckForReset()
        {
            if (!_initialized)
            {
                return;
            }

            foreach (ToonRenderingExtensionAsset usedAsset in _sourceAssets.Values)
            {
                if (usedAsset != null)
                {
                    continue;
                }

                _initialized = false;
                return;
            }

            for (int index = 0; index < _extensions.Length; index++)
            {
                var @event = (ToonRenderingEvent) index;
                List<IToonRenderingExtension> extensions = _extensions[index];
                if (extensions == null)
                {
                    continue;
                }

                foreach (IToonRenderingExtension extension in extensions)
                {
                    if (_sourceAssets[extension].Event == @event)
                    {
                        continue;
                    }

                    _initialized = false;
                    return;
                }
            }
        }

        private void AddExtension(ToonRenderingEvent @event, IToonRenderingExtension extension)
        {
            List<IToonRenderingExtension> extensions = GetOrCreateExtensionList(@event);
            extensions.Add(extension);
        }

        public void RenderEvent(ToonRenderingEvent @event)
        {
            List<IToonRenderingExtension> extensions = _filteredExtensions[(int) @event];
            if (extensions == null)
            {
                return;
            }

            foreach (IToonRenderingExtension extension in extensions)
            {
                extension.Render();
            }
        }

        public void Setup(in ToonRenderingExtensionContext context)
        {
            _context = context;

            foreach (List<IToonRenderingExtension> extensions in _filteredExtensions)
            {
                extensions?.Clear();
            }

            for (int index = 0; index < _extensions.Length; index++)
            {
                List<IToonRenderingExtension> extensions = _extensions[index];
                if (extensions == null)
                {
                    continue;
                }

                foreach (IToonRenderingExtension extension in extensions)
                {
                    if (!extension.ShouldRender(_context))
                    {
                        continue;
                    }

                    extension.Setup(_context, this);
                    _filteredExtensions[index] ??= new List<IToonRenderingExtension>();
                    _filteredExtensions[index].Add(extension);
                }
            }
        }

        public void Cleanup()
        {
            foreach (List<IToonRenderingExtension> extensions in _filteredExtensions)
            {
                if (extensions == null)
                {
                    continue;
                }

                foreach (IToonRenderingExtension extension in extensions)
                {
                    extension.Cleanup();
                }
            }
        }

        [NotNull]
        private List<IToonRenderingExtension> GetOrCreateExtensionList(ToonRenderingEvent @event) =>
            _extensions[(int) @event] ??= new List<IToonRenderingExtension>();
    }
}