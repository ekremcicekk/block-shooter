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

        [Header("Mystery Animator")]
        [Tooltip("Animator for the mystery visual (e.g. Myster object)")]
        public Animator animator;

        private ShooterBlock _block;

        public void PlayShake()
        {
            if (animator != null)
            {
                animator.SetTrigger("ShooterShake");
            }
        }

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

            bool shouldShowBase = !isMystery && !_block.IsFrozen;
            if (baseVisual != null) baseVisual.SetActive(shouldShowBase);
        }

        public void Reveal()
        {
            if (_block == null) return;
            _block.isMystery = false;

            if (mysteryVisual != null) mysteryVisual.SetActive(false);

            bool shouldShowBase = !_block.IsFrozen;
            if (baseVisual != null) baseVisual.SetActive(shouldShowBase);

            _block.RevealFromFeature();

            if (revealParticle != null)
            {
                ParticleSystem psToPlay = revealParticle;
                
                // Check if it's a project asset prefab (not currently in the scene hierarchy)
                bool isPrefabAsset = !revealParticle.gameObject.scene.IsValid();
                
                if (isPrefabAsset)
                {
                    // Instantiate particle at runtime to play it safely
                    Vector3 spawnPos = transform.position + new Vector3(0f, 0.4f, 0f);
                    psToPlay = Instantiate(revealParticle, spawnPos, Quaternion.identity);
                    Destroy(psToPlay.gameObject, psToPlay.main.duration + psToPlay.main.startLifetime.constantMax);
                }
                else
                {
                    // If it is child of Myster, unparent it to root so it isn't deactivated when Myster turns off
                    if (mysteryVisual != null && psToPlay.transform.IsChildOf(mysteryVisual.transform))
                    {
                        psToPlay.transform.SetParent(transform, true);
                    }
                    
                    psToPlay.gameObject.SetActive(true);
                    Vector3 localPos = psToPlay.transform.localPosition;
                    localPos.y = 0.4f;
                    psToPlay.transform.localPosition = localPos;
                }
                
                psToPlay.Play();
            }
        }
    }
}
