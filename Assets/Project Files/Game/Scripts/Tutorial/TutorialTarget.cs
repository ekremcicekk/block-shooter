using UnityEngine;
using System;
using System.Collections.Generic;

namespace BlockShooter
{
    /// <summary>
    /// Marks a UI or world object as a tutorial target.
    /// Attach this to booster buttons, shooter blocks, or custom scene anchors.
    /// </summary>
    public class TutorialTarget : MonoBehaviour
    {
        private static readonly Dictionary<string, TutorialTarget> RegisteredTargets = new Dictionary<string, TutorialTarget>(StringComparer.OrdinalIgnoreCase);

        public static event Action<TutorialTarget> TargetRegistered;
        public static event Action<TutorialTarget> TargetUnregistered;

        [Header("Optional Overrides")]
        [SerializeField] private string _targetId;
        [SerializeField] private RectTransform _uiTarget;
        [SerializeField] private Transform _worldTarget;
        [SerializeField] private GameObject _highlightObject;

        public string TargetId => _targetId;
        public RectTransform UiTarget => _uiTarget != null ? _uiTarget : GetComponent<RectTransform>();
        public Transform WorldTarget => _worldTarget != null ? _worldTarget : transform;
        public GameObject HighlightObject => _highlightObject;

        public static bool TryGetRegistered(string targetId, out TutorialTarget target)
        {
            target = null;
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return false;
            }

            return RegisteredTargets.TryGetValue(targetId, out target) && target != null;
        }

        private void OnEnable()
        {
            RegisterTarget();
        }

        private void OnDisable()
        {
            UnregisterTarget();
        }

        private void OnValidate()
        {
            if (_uiTarget == null)
            {
                _uiTarget = GetComponent<RectTransform>();
            }

            if (_worldTarget == null)
            {
                _worldTarget = transform;
            }
        }

        private void RegisterTarget()
        {
            if (string.IsNullOrWhiteSpace(_targetId))
            {
                return;
            }

            RegisteredTargets[_targetId] = this;
            TargetRegistered?.Invoke(this);
        }

        private void UnregisterTarget()
        {
            if (string.IsNullOrWhiteSpace(_targetId))
            {
                return;
            }

            if (RegisteredTargets.TryGetValue(_targetId, out var registered) && registered == this)
            {
                RegisteredTargets.Remove(_targetId);
            }

            TargetUnregistered?.Invoke(this);
        }

        public bool Matches(Transform other)
        {
            if (other == null) return false;
            if (other == transform) return true;
            return other.IsChildOf(transform) || transform.IsChildOf(other);
        }

        public Vector3 GetWorldPosition()
        {
            return WorldTarget != null ? WorldTarget.position : transform.position;
        }

        public void SetHighlight(bool active)
        {
            if (_highlightObject != null)
            {
                _highlightObject.SetActive(active);
            }
        }
    }
}