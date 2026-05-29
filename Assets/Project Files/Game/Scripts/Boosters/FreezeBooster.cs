using System.Collections;
using UnityEngine;
using DG.Tweening;

namespace BlockShooter
{
    public class FreezeBooster : BoosterBase
    {
        [Header("Freeze VFX")]
        public ParticleSystem freezeParticle;

        protected override void OnActivate()
        {
            ConveyorBelt.Instance?.SetFrozen(true);

            if (freezeParticle != null)
                freezeParticle.Play();

            // Slow time scale effect
            DOTween.To(() => Time.timeScale, x => Time.timeScale = x, 0.2f, 0.2f)
                .SetUpdate(true);

            StartCoroutine(FreezeTimer());
        }

        private IEnumerator FreezeTimer()
        {
            yield return new WaitForSecondsRealtime(data.duration);
            ConveyorBelt.Instance?.SetFrozen(false);

            DOTween.To(() => Time.timeScale, x => Time.timeScale = x, 1f, 0.3f)
                .SetUpdate(true);

            if (freezeParticle != null)
                freezeParticle.Stop();

            EndBooster();
        }

        protected override void OnDeactivate()
        {
            ConveyorBelt.Instance?.SetFrozen(false);
            Time.timeScale = 1f;
        }
    }
}
