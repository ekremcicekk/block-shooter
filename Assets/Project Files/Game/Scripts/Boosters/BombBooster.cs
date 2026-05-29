using UnityEngine;
using DG.Tweening;

namespace BlockShooter
{
    public class BombBooster : BoosterBase
    {
        [Header("Bomb Settings")]
        public ParticleSystem explosionPrefab;
        public int destroyRadius = 2; // number of columns to clear

        protected override void OnActivate()
        {
            // Destroy all visible blocks in FireRange
            if (ConveyorBelt.Instance != null)
                ConveyorBelt.Instance.DestroyAllInRange();

            if (explosionPrefab != null)
            {
                var particle = Instantiate(explosionPrefab, FireRange.Instance?.transform.position ?? Vector3.zero, Quaternion.identity);
                particle.Play();
                Destroy(particle.gameObject, 2f);
            }

            Camera.main?.DOShakePosition(0.3f, 0.2f, 10, 90);
            EndBooster();
        }
    }
}
