using UnityEngine;
using DG.Tweening;

namespace EKStudio
{
    public enum PunchScaleAxis { X, Y, Z, All } // Defines which axis will be affected

    public class PunchScaleAnimation : MonoBehaviour
    {
        [Header("Punch Settings")]
        public Vector3 PunchStrength = new Vector3(2f, 2f, 2f); // The strength of the punch effect
        public float Duration = 0.2f; // Duration of the animation
        public float Delay = 0f; // Delay before starting animation
        public int Vibrato = 10; // Number of vibrato oscillations
        public float Elasticity = 1f; // Elasticity level of the punch effect
        public PunchScaleAxis ScaleAxis = PunchScaleAxis.All; // Determines which axis will be scaled

        public Ease EaseType = Ease.OutQuad; // Ease type for the animation
        public int Loops = 1; // Number of times the animation will loop
        public string ID; // Optional DOTween animation ID

        private Vector3 originalScale;
        private Tween currentTween; // Stores the active tween animation

        private void Awake()
        {
            originalScale = transform.localScale;
        }

        private void OnEnable()
        {
            ApplyPunchScale(); // Restart animation when GameObject is activated
        }

        private void OnDisable()
        {
            // Ensure the animation stops when the object is deactivated
            currentTween?.Kill();
            transform.localScale = originalScale; // Reset scale
        }

        void ApplyPunchScale()
        {
            Vector3 punchVector = Vector3.zero;

            // Apply punch strength based on the selected axis
            if (ScaleAxis == PunchScaleAxis.All || ScaleAxis == PunchScaleAxis.X)
            {
                punchVector.x = PunchStrength.x;
            }
            if (ScaleAxis == PunchScaleAxis.All || ScaleAxis == PunchScaleAxis.Y)
            {
                punchVector.y = PunchStrength.y;
            }
            if (ScaleAxis == PunchScaleAxis.All || ScaleAxis == PunchScaleAxis.Z)
            {
                punchVector.z = PunchStrength.z;
            }

            // Apply the PunchScale animation with delay
            currentTween = transform.DOPunchScale(punchVector, Duration, Vibrato, Elasticity)
                .SetEase(EaseType)
                .SetDelay(Delay) // Added delay before animation starts
                .SetLoops(Loops)
                .SetId(ID);
        }
    }
}
