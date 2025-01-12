﻿using UnityEngine;

namespace DELTation.ToonRP.Shadows
{
    [ExecuteAlways]
    public sealed class BlobShadowRenderer : MonoBehaviour
    {
        [SerializeField] [Min(0f)] private float _radius = 0.5f;

        public float Radius => _radius;

        public Vector3 Position => transform.position;

        private void OnEnable()
        {
            BlobShadowsManager.OnRendererEnabled(this);
        }

        private void OnDisable()
        {
            BlobShadowsManager.OnRendererDisabled(this);
        }
    }
}