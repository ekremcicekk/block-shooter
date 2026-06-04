using UnityEngine;

namespace BlockShooter
{
    /// <summary>
    /// A modular feature component for ShooterBlock. Handles toggling between the
    /// mystery visual ("Myster") and standard visual ("Base") on the standard block prefab.
    /// </summary>
    [RequireComponent(typeof(ShooterBlock))]
    public class MysteryBlockFeature : MonoBehaviour
    {
        [Header("Modular Visual Groups")]
        [Tooltip("The normal block visual group GameObject (Base)")]
        public GameObject baseVisual;

        [Tooltip("The question-mark visual group GameObject (Myster)")]
        public GameObject mysteryVisual;

        [Tooltip("Optional particle system to play when revealed")]
        public ParticleSystem revealParticle;

        private ShooterBlock _block;

        private void Awake()
        {
            _block = GetComponent<ShooterBlock>();

            // Auto-find children by name if not assigned in the inspector
            if (baseVisual == null)
            {
                var t = transform.Find("Base");
                if (t != null) baseVisual = t.gameObject;
            }
            if (mysteryVisual == null)
            {
                var t = transform.Find("Myster");
                if (t == null) t = transform.Find("Mystery");
                if (t != null) mysteryVisual = t.gameObject;
            }

            // Sync visual states on start based on mystery state
            SyncVisualStates();
        }

        private void SyncVisualStates()
        {
            if (_block == null) return;

            bool isMystery = _block.isMystery;
            if (mysteryVisual != null) mysteryVisual.SetActive(isMystery);
            if (baseVisual != null) baseVisual.SetActive(!isMystery);
        }

        public void Reveal()
        {
            if (_block == null) return;
            _block.isMystery = false;

            if (mysteryVisual != null) mysteryVisual.SetActive(false);
            if (baseVisual != null) baseVisual.SetActive(true);

            _block.RevealFromFeature();

            if (revealParticle != null)
            {
                revealParticle.Play();
            }
        }
    }
}
