using System.Collections.Generic;
using UnityEngine;

namespace BlockShooter
{
    /// <summary>
    /// Root of a tutorial block on the canvas, for example Tutorial_1 or Tutorial_2.
    /// It owns the ordered step children.
    /// </summary>
    public class TutorialGroupView : MonoBehaviour
    {
        [SerializeField] private int _level = 1;
        [SerializeField] private bool _showOnce = true;
        [SerializeField] private List<TutorialStepView> _steps = new List<TutorialStepView>();

        public int Level => _level;
        public bool ShowOnce => _showOnce;
        public IReadOnlyList<TutorialStepView> Steps => _steps;

        // Note: Steps are explicitly defined in the inspector. We do NOT auto-scan
        // child GameObjects at runtime — the manager will use the serialized
        // `_steps` list when present. `RefreshStepsFromChildren()` remains a
        // helper for manual population but is no longer called automatically.

        public void RefreshStepsFromChildren()
        {
            _steps.Clear();

            var childSteps = GetComponentsInChildren<TutorialStepView>(true);
            foreach (var step in childSteps)
            {
                if (step != null && step.transform != transform)
                {
                    _steps.Add(step);
                }
            }

            _steps.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            HideAllSteps();
            gameObject.SetActive(false);
        }

        public TutorialStepView GetStep(int index)
        {
            if (index < 0 || index >= _steps.Count)
            {
                return null;
            }

            return _steps[index];
        }

        public void HideAllSteps()
        {
            for (int i = 0; i < _steps.Count; i++)
            {
                if (_steps[i] != null)
                {
                    _steps[i].Hide();
                }
            }
        }
    }
}