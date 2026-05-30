using System.Collections;
using UnityEngine;
using DG.Tweening;

namespace BlockShooter
{
    /// <summary>
    /// Manages the three boosters. No child objects needed — attach to [Managers].
    ///
    /// ExtraSlot  — adds one more firing slot for the rest of the level.
    /// FreePick   — for `freePickData.duration` seconds, ALL grid blocks become selectable.
    /// ColorBlast — enters selection mode; player taps a slotted block;
    ///              that block fires instantly at every matching-color block in FireRange.
    /// </summary>
    public class BoosterManager : MonoBehaviour
    {
        public static BoosterManager Instance { get; private set; }

        [Header("Booster Config (ScriptableObjects — optional)")]
        public BoosterData extraSlotData;
        public BoosterData freePickData;
        public BoosterData colorBlastData;

        [Header("Initial unlock reward")]
        public int initialBoosterCount = 2;

        // ColorBlast awaits a tap on a slotted block
        public bool IsAwaitingColorBlastTarget { get; private set; }

        private Coroutine _freePickCoroutine;

        // ── Unity ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start() => CheckUnlocks();

        // ── Public API ────────────────────────────────────────────────────────

        public bool IsBoosterUnlocked(BoosterType type)
        {
            int unlockLevel = type switch
            {
                BoosterType.ExtraSlot  => extraSlotData  != null ? extraSlotData.unlockLevel  : GameManager.Instance.config.extraSlotUnlockLevel,
                BoosterType.FreePick   => freePickData   != null ? freePickData.unlockLevel   : GameManager.Instance.config.freePickUnlockLevel,
                BoosterType.ColorBlast => colorBlastData != null ? colorBlastData.unlockLevel : GameManager.Instance.config.colorBlastUnlockLevel,
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
                case BoosterType.ExtraSlot:  ActivateExtraSlot();  break;
                case BoosterType.FreePick:   ActivateFreePick();   break;
                case BoosterType.ColorBlast: ActivateColorBlast(); break;
            }
            return true;
        }

        // ── ExtraSlot ─────────────────────────────────────────────────────────
        // Adds one more firing slot. Permanent for the level.

        private void ActivateExtraSlot()
        {
            SlotSystem.Instance?.AddExtraSlot();
            Camera.main?.DOShakePosition(0.15f, 0.08f, 5, 90);
        }

        // ── FreePick ──────────────────────────────────────────────────────────
        // For a limited time, all grid blocks become selectable regardless of row.
        // After the player picks a block (or time runs out), normal rules resume.

        private void ActivateFreePick()
        {
            if (_freePickCoroutine != null)
            {
                StopCoroutine(_freePickCoroutine);
                ShooterGrid.Instance?.SetFreePickMode(false);
            }

            float duration = freePickData != null ? freePickData.duration : 8f;
            _freePickCoroutine = StartCoroutine(FreePickRoutine(duration));
        }

        private IEnumerator FreePickRoutine(float duration)
        {
            ShooterGrid.Instance?.SetFreePickMode(true);

            // End early if the player picks a block (watch for a slotted event)
            float elapsed = 0f;
            int slotCountBefore = SlotSystem.Instance != null ? SlotSystem.Instance.GetSlottedBlocks().Count : 0;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                // If a new block was slotted, the pick was made — end early
                int slotCountNow = SlotSystem.Instance != null ? SlotSystem.Instance.GetSlottedBlocks().Count : 0;
                if (slotCountNow > slotCountBefore) break;

                yield return null;
            }

            ShooterGrid.Instance?.SetFreePickMode(false);
            _freePickCoroutine = null;
        }

        // ── ColorBlast ────────────────────────────────────────────────────────
        // Enters selection mode. Player must tap a slotted block.
        // That block fires at every matching-color block in FireRange simultaneously.

        private void ActivateColorBlast()
        {
            if (SlotSystem.Instance == null) return;
            var slotted = SlotSystem.Instance.GetSlottedBlocks();
            if (slotted.Count == 0) return;

            IsAwaitingColorBlastTarget = true;

            // Highlight slotted blocks so player knows to tap one
            foreach (var b in slotted)
                b.transform.DOPunchScale(Vector3.one * 0.2f, 0.4f, 4, 0.5f);

            StartCoroutine(WaitForColorBlastTarget(slotted));
        }

        private IEnumerator WaitForColorBlastTarget(System.Collections.Generic.List<ShooterBlock> candidates)
        {
            float timeout = colorBlastData != null ? colorBlastData.duration : 8f;
            float elapsed = 0f;

            while (elapsed < timeout && IsAwaitingColorBlastTarget)
            {
                elapsed += Time.deltaTime;

                if (Input.GetMouseButtonDown(0))
                {
                    var hit = RaycastBlock();
                    if (hit != null && candidates.Contains(hit))
                    {
                        IsAwaitingColorBlastTarget = false;
                        hit.FireColorBlast();
                        yield break;
                    }
                }
                yield return null;
            }

            IsAwaitingColorBlastTarget = false;
        }

        private ShooterBlock RaycastBlock()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                return hit.collider.GetComponent<ShooterBlock>();
            return null;
        }

        // ── Unlock rewards ────────────────────────────────────────────────────

        private void CheckUnlocks()
        {
            int level = SaveManager.CurrentLevel;
            TryGiveInitial(BoosterType.ExtraSlot,  extraSlotData  != null ? extraSlotData.unlockLevel  : GameManager.Instance.config.extraSlotUnlockLevel,  level);
            TryGiveInitial(BoosterType.FreePick,   freePickData   != null ? freePickData.unlockLevel   : GameManager.Instance.config.freePickUnlockLevel,   level);
            TryGiveInitial(BoosterType.ColorBlast, colorBlastData != null ? colorBlastData.unlockLevel : GameManager.Instance.config.colorBlastUnlockLevel, level);
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
            SaveManager.AddBooster(BoosterType.ExtraSlot, 3);
            SaveManager.AddBooster(BoosterType.FreePick, 3);
            SaveManager.AddBooster(BoosterType.ColorBlast, 3);
        }
#endif
    }
}
