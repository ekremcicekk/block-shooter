using System.Collections;
using UnityEngine;

namespace BlockShooter
{
    public class RainbowBooster : BoosterBase
    {
        protected override void OnActivate()
        {
            ShooterGrid.Instance?.SetRainbowMode(true);
            StartCoroutine(RainbowTimer());
        }

        private IEnumerator RainbowTimer()
        {
            yield return new WaitForSeconds(data.duration);
            ShooterGrid.Instance?.SetRainbowMode(false);
            EndBooster();
        }

        protected override void OnDeactivate()
        {
            ShooterGrid.Instance?.SetRainbowMode(false);
        }
    }
}
