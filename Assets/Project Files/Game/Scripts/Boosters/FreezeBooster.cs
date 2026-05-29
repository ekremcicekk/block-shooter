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
            SetConveyorsFrozen(true);

            if (freezeParticle != null)
                freezeParticle.Play();

            DOTween.To(() => Time.timeScale, x => Time.timeScale = x, 0.2f, 0.2f)
                .SetUpdate(true);

            StartCoroutine(FreezeTimer());
        }

        private IEnumerator FreezeTimer()
        {
            yield return new WaitForSecondsRealtime(data.duration);
            SetConveyorsFrozen(false);

            DOTween.To(() => Time.timeScale, x => Time.timeScale = x, 1f, 0.3f)
                .SetUpdate(true);

            if (freezeParticle != null)
                freezeParticle.Stop();

            EndBooster();
        }

        protected override void OnDeactivate()
        {
            SetConveyorsFrozen(false);
            Time.timeScale = 1f;
        }

        private static void SetConveyorsFrozen(bool frozen)
        {
            ConveyorBelt.Instance?.SetFrozen(frozen);
            foreach (var ctrl in Object.FindObjectsByType<ConveyorPathController>(FindObjectsSortMode.None))
                ctrl.IsFrozen = frozen;
        }
    }
}
