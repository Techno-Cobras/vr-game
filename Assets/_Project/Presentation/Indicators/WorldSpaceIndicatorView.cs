using System;
using UnityEngine;
using UnityEngine.UI;

namespace VrGame.Presentation.Indicators
{
    public sealed class WorldSpaceIndicatorView : MonoBehaviour
    {
        [SerializeField]
        private Text label;

        [SerializeField]
        private Image background;

        private Transform viewer;
        private Transform targetAnchor;
        private Vector3 targetOffset;
        private bool targetUnavailableNotified;

        public event Action<WorldSpaceIndicatorView> TargetUnavailable;

        public string Label => label != null ? label.text : string.Empty;

        public Color BackgroundColor => background != null ? background.color : default;

        public Transform TargetAnchor => targetAnchor;

        public void Bind(IndicatorTarget target, Vector3 localOffset, Transform viewerTransform)
        {
            targetAnchor = target.Anchor;
            targetOffset = localOffset;
            targetUnavailableNotified = false;
            viewer = viewerTransform;
            gameObject.SetActive(true);
            RefreshPlacement();
            RefreshBillboard();
        }

        public void SetViewer(Transform viewerTransform)
        {
            viewer = viewerTransform;
            RefreshBillboard();
        }

        public void Release(Transform poolRoot)
        {
            viewer = null;
            targetAnchor = null;
            targetOffset = default;
            targetUnavailableNotified = true;
            if (transform.parent != poolRoot)
                transform.SetParent(poolRoot, false);
            gameObject.SetActive(false);
        }

        public void RefreshPlacement()
        {
            if (targetAnchor != null)
                transform.position = targetAnchor.TransformPoint(targetOffset);
        }

        public void RefreshBillboard()
        {
            if (viewer == null || !gameObject.activeInHierarchy)
                return;

            var direction = transform.position - viewer.position;
            if (direction.sqrMagnitude > 0.000001f)
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private void LateUpdate()
        {
            if (targetAnchor == null || !targetAnchor.gameObject.activeInHierarchy)
            {
                if (!targetUnavailableNotified)
                {
                    targetUnavailableNotified = true;
                    TargetUnavailable?.Invoke(this);
                }

                return;
            }

            RefreshPlacement();
            RefreshBillboard();
        }
    }
}
