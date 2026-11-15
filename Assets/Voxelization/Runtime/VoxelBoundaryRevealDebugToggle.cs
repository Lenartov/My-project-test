using UnityEngine;
using UnityEngine.InputSystem;

namespace Voxelization
{
    [DisallowMultipleComponent]
    public sealed class VoxelBoundaryRevealDebugToggle : MonoBehaviour
    {
        [SerializeField] private VoxelBoundaryRevealGroup revealGroup;

        private bool isAnimating;
        private float startProgress;
        private float targetProgress;
        private float elapsed;

        private void Awake()
        {
            if (revealGroup == null)
            {
                Debug.LogError("Assign a VoxelBoundaryRevealGroup to the debug toggle.", this);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            isAnimating = false;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                startProgress = revealGroup.RevealProgress;
                targetProgress = isAnimating ? 1f - targetProgress : (startProgress >= 0.5f ? 0f : 1f);
                elapsed = 0f;
                // Stop the group's built-in playback before driving its progress externally.
                revealGroup.SetProgress(startProgress);
                isAnimating = true;
            }

            if (isAnimating)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / revealGroup.RevealDuration);
                revealGroup.SetProgress(Mathf.Lerp(startProgress, targetProgress, progress));
                if (progress >= 1f)
                {
                    isAnimating = false;
                }
            }
        }
    }
}
