using UnityEngine;
using UnityEngine.InputSystem;

namespace VrGame.Bootstrap
{
    /// <summary>
    /// Единая точка управления жизненным циклом ввода стартовой VR-сцены.
    /// Игровое состояние здесь не хранится.
    /// </summary>
    public sealed class VrBootstrap : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;

        private bool isPaused;
        private bool hasFocus = true;

        public InputActionAsset InputActions => inputActions;

        private void OnEnable()
        {
            ApplyInputState();
        }

        private void OnDisable()
        {
            inputActions?.Disable();
        }

        private void OnApplicationPause(bool paused)
        {
            isPaused = paused;
            ApplyInputState();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            this.hasFocus = hasFocus;
            ApplyInputState();
        }

        private void ApplyInputState()
        {
            if (!isActiveAndEnabled || isPaused || !hasFocus)
                inputActions?.Disable();
            else
                inputActions?.Enable();
        }
    }
}
