using UnityEngine;
using TMPro;
using DG.Tweening;

namespace BlockShooter
{
    public class ComboUI : MonoBehaviour
    {
        [Header("UI")]
        public GameObject panel;
        public TextMeshProUGUI comboText;

        private Tween _hideTween;

        private void OnEnable()
        {
            ScoreManager.OnComboChanged += ShowCombo;
        }

        private void OnDisable()
        {
            ScoreManager.OnComboChanged -= ShowCombo;
        }

        private void Start()
        {
            panel?.SetActive(false);
        }

        private void ShowCombo(int combo)
        {
            if (combo < 3) { Hide(); return; }

            panel?.SetActive(true);
            if (comboText != null)
                comboText.text = $"x{combo} COMBO!";

            panel.transform.DOPunchScale(Vector3.one * 0.3f, 0.2f, 3, 0.5f).SetUpdate(true);

            _hideTween?.Kill();
            _hideTween = DOVirtual.DelayedCall(1.5f, Hide).SetUpdate(true);
        }

        private void Hide()
        {
            panel?.SetActive(false);
        }
    }
}
