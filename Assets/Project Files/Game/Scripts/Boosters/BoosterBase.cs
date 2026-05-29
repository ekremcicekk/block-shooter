using UnityEngine;

namespace BlockShooter
{
    public abstract class BoosterBase : MonoBehaviour
    {
        public BoosterData data;
        protected bool _isActive;

        public bool IsUnlocked => SaveManager.CurrentLevel >= data.unlockLevel;
        public int RemainingUses => SaveManager.GetBoosterCount(data.boosterType);

        public bool TryActivate()
        {
            if (!IsUnlocked || _isActive) return false;
            if (!SaveManager.UseBooster(data.boosterType)) return false;

            _isActive = true;
            OnActivate();
            return true;
        }

        protected abstract void OnActivate();

        protected void EndBooster()
        {
            _isActive = false;
            OnDeactivate();
        }

        protected virtual void OnDeactivate() { }
    }
}
