using UnityEngine;

namespace BlockShooter
{
    /// <summary>
    /// Represents a single visual step inside a tutorial group.
    /// Put this on Tutorial_1/Step_1, Tutorial_2/Step_1, etc.
    /// </summary>
    public class TutorialStepView : MonoBehaviour
    {
        [SerializeField] private TutorialTarget _target;

        public TutorialTarget Target => _target;

        private void OnValidate()
        {
            if (_target == null)
            {
                _target = GetComponentInChildren<TutorialTarget>(true);
            }
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}