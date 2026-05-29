using System.Collections;
using UnityEngine;
using DG.Tweening;

namespace BlockShooter
{
    /// <summary>
    /// Self-contained booster manager.
    /// No child objects needed — all three booster types are handled here.
    /// Attach this to the [Managers] GameObject alongside GameManager.
    ///
    /// Each booster is configured via a BoosterData ScriptableObject (Assets/Data/).
    /// Optional VFX ParticleSystems can be assigned; if null they are silently skipped.
    /// </summary>
    public class BoosterManager : MonoBehaviour
    {
        public static BoosterManager Instance { get; private set; }

        [Header("Booster Config (ScriptableObjects)")]
        public BoosterData bombData;
        public BoosterData rainbowData;
        public BoosterData freezeData;

        [Header("VFX (optional)")]
        public ParticleSystem explosionPrefab;
        public ParticleSystem freezeParticlePrefab;

        [Header("Initial unlock reward")]
        [Tooltip("How many uses the player receives the first time a booster unlocks")]
        public int initialBoosterCount = 2;

        private bool _rainbowActive;
        private bool _freezeActive;
        private Coroutine _rainbowCoroutine;
        private Coroutine _freezeCoroutine;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start() => CheckUnlocks();

        // ── Public API (used by BoosterButtonUI) ───────────────────────────────

        public bool IsBoosterUnlocked(BoosterType type)
        {
            int unlockLevel = type switch
            {
                BoosterType.Bomb    => bombData   != null ? bombData.unlockLevel   : GameManager.Instance.config.bombBoosterUnlockLevel,
                BoosterType.Rainbow => rainbowData != null ? rainbowData.unlockLevel : GameManager.Instance.config.rainbowBoosterUnlockLevel,
                BoosterType.Freeze  => freezeData  != null ? freezeData.unlockLevel  : GameManager.Instance.config.freezeBoosterUnlockLevel,
                _ => 999
            };
            return SaveManager.CurrentLevel >= unlockLevel;
        }

        public bool ActivateBooster(BoosterType type)
        {
            if (!IsBoosterUnlocked(type)) return false;
            if (!SaveManager.UseBooster(type)) return false;

            switch (type)
            {
                case BoosterType.Bomb:    ActivateBomb();    break;
                case BoosterType.Rainbow: ActivateRainbow(); break;
                case BoosterType.Freeze:  ActivateFreeze();  break;
            }
            return true;
        }

        // ── Bomb ───────────────────────────────────────────────────────────────

        private void ActivateBomb()
        {
            // Destroy all conveyor blocks currently in the fire range
            foreach (var ctrl in FindObjectsByType<ConveyorPathController>(FindObjectsSortMode.None))
                ctrl.DestroyBlocksInFireRange();

            // Legacy 2D belt support
            ConveyorBelt.Instance?.DestroyAllInRange();

            // VFX
            if (explosionPrefab != null)
            {
                var pos = FireRange.Instance != null ? FireRange.Instance.transform.position : Vector3.zero;
                var fx = Instantiate(explosionPrefab, pos, Quaternion.identity);
                fx.Play();
                Destroy(fx.gameObject, 2f);
            }

            Camera.main?.DOShakePosition(0.3f, 0.2f, 10, 90);
        }

        // ── Rainbow ────────────────────────────────────────────────────────────

        private void ActivateRainbow()
        {
            if (_rainbowActive)
            {
                if (_rainbowCoroutine != null) StopCoroutine(_rainbowCoroutine);
                ShooterGrid.Instance?.SetRainbowMode(false);
            }
            _rainbowActive = true;
            ShooterGrid.Instance?.SetRainbowMode(true);
            float duration = rainbowData != null ? rainbowData.duration : 5f;
            _rainbowCoroutine = StartCoroutine(RainbowTimer(duration));
        }

        private IEnumerator RainbowTimer(float duration)
        {
            yield return new WaitForSeconds(duration);
            _rainbowActive = false;
            ShooterGrid.Instance?.SetRainbowMode(false);
        }

        // ── Freeze ─────────────────────────────────────────────────────────────

        private void ActivateFreeze()
        {
            if (_freezeActive)
            {
                if (_freezeCoroutine != null) StopCoroutine(_freezeCoroutine);
                EndFreeze();
            }

            _freezeActive = true;
            SetConveyorsFrozen(true);

            if (freezeParticlePrefab != null)
            {
                var fx = Instantiate(freezeParticlePrefab, transform.position, Quaternion.identity);
                fx.Play();
                Destroy(fx.gameObject, (freezeData != null ? freezeData.duration : 5f) + 1f);
            }

            DOTween.To(() => Time.timeScale, x => Time.timeScale = x, 0.2f, 0.2f).SetUpdate(true);

            float dur = freezeData != null ? freezeData.duration : 5f;
            _freezeCoroutine = StartCoroutine(FreezeTimer(dur));
        }

        private IEnumerator FreezeTimer(float duration)
        {
            yield return new WaitForSecondsRealtime(duration);
            EndFreeze();
        }

        private void EndFreeze()
        {
            _freezeActive = false;
            SetConveyorsFrozen(false);
            DOTween.To(() => Time.timeScale, x => Time.timeScale = x, 1f, 0.3f).SetUpdate(true);
        }

        private static void SetConveyorsFrozen(bool frozen)
        {
            ConveyorBelt.Instance?.SetFrozen(frozen);
            foreach (var ctrl in FindObjectsByType<ConveyorPathController>(FindObjectsSortMode.None))
                ctrl.IsFrozen = frozen;
        }

        // ── Unlock rewards ─────────────────────────────────────────────────────

        private void CheckUnlocks()
        {
            int level = SaveManager.CurrentLevel;
            TryGiveInitial(BoosterType.Bomb,    bombData    != null ? bombData.unlockLevel    : GameManager.Instance.config.bombBoosterUnlockLevel,    level);
            TryGiveInitial(BoosterType.Rainbow, rainbowData != null ? rainbowData.unlockLevel : GameManager.Instance.config.rainbowBoosterUnlockLevel, level);
            TryGiveInitial(BoosterType.Freeze,  freezeData  != null ? freezeData.unlockLevel  : GameManager.Instance.config.freezeBoosterUnlockLevel,  level);
        }

        private void TryGiveInitial(BoosterType type, int unlockLevel, int currentLevel)
        {
            string key = $"BoosterSeen_{type}";
            if (currentLevel >= unlockLevel && PlayerPrefs.GetInt(key, 0) == 0)
            {
                SaveManager.AddBooster(type, initialBoosterCount);
                PlayerPrefs.SetInt(key, 1);
                PlayerPrefs.Save();
                BoosterUnlockUI.Instance?.ShowUnlock(type);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Give All Boosters (Debug)")]
        private void GiveAllBoosters()
        {
            SaveManager.AddBooster(BoosterType.Bomb, 3);
            SaveManager.AddBooster(BoosterType.Rainbow, 3);
            SaveManager.AddBooster(BoosterType.Freeze, 3);
        }
#endif
    }
}
