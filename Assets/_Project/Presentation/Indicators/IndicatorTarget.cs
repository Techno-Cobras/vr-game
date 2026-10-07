using System;
using UnityEngine;

namespace VrGame.Presentation.Indicators
{
    public sealed class IndicatorTarget : MonoBehaviour
    {
        [SerializeField]
        private Transform anchor;

        private bool unavailableNotified;

        public event Action<IndicatorTarget> Unavailable;

        public Transform Anchor => anchor != null ? anchor : transform;

        private void OnEnable()
        {
            unavailableNotified = false;
        }

        private void OnDisable()
        {
            NotifyUnavailable();
        }

        private void OnDestroy()
        {
            NotifyUnavailable();
        }

        private void NotifyUnavailable()
        {
            if (unavailableNotified)
                return;

            unavailableNotified = true;
            Unavailable?.Invoke(this);
        }
    }
}
