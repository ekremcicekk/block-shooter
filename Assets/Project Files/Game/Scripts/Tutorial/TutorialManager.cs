using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlockShooter
{
    /// <summary>
    /// Simple tutorial runner that shows a tutorial root and can advance through
    /// ordered steps managed entirely from this component.
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        [Serializable]
        private class TutorialStepDefinition
        {
            [SerializeField] private GameObject _stepRoot;
            [SerializeField] private string _targetId;
            [SerializeField] private Vector3 _handOffset;

            public GameObject StepRoot => _stepRoot;
            public string TargetId => _targetId;
            public Vector3 HandOffset => _handOffset;
        }

        [Serializable]
        private class TutorialDefinition
        {
            [SerializeField] private int _level = 1;
            [SerializeField] private bool _showOnce = true;
            [SerializeField] private GameObject _tutorialRoot;
            [SerializeField] private bool _useSteps;
            [SerializeField] private string _targetId;
            [SerializeField] private Vector3 _handOffset;
            [SerializeField] private List<TutorialStepDefinition> _steps = new List<TutorialStepDefinition>();

            public int Level => _level;
            public bool ShowOnce => _showOnce;
            public GameObject TutorialRoot => _tutorialRoot;
            public bool UseSteps => _useSteps;
            public string TargetId => _targetId;
            public Vector3 HandOffset => _handOffset;
            public IReadOnlyList<TutorialStepDefinition> Steps => _steps;
        }

        [Header("Presentation")]
        [SerializeField] private TutorialCanvasController _canvasController;

        [Header("Tutorials")]
        [SerializeField] private List<TutorialDefinition> _tutorials = new List<TutorialDefinition>();

        [Header("Behavior")]
        [SerializeField] private bool _autoStartOnLevelLoad = true;

        private TutorialDefinition _activeTutorial;
        private TutorialStepDefinition _activeStep;
        private TutorialTarget _activeTarget;
        private int _activeStepIndex = -1;
        private bool _isRunning;

        public bool IsRunning => _isRunning;
        public TutorialTarget ActiveTarget => _activeTarget;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            LevelManager.OnLevelLoaded += HandleLevelLoaded;
            GameManager.OnStateChanged += HandleGameStateChanged;
            TutorialTarget.TargetRegistered += HandleTargetRegistered;
            TutorialTarget.TargetUnregistered += HandleTargetUnregistered;
        }

        private void OnDisable()
        {
            LevelManager.OnLevelLoaded -= HandleLevelLoaded;
            GameManager.OnStateChanged -= HandleGameStateChanged;
            TutorialTarget.TargetRegistered -= HandleTargetRegistered;
            TutorialTarget.TargetUnregistered -= HandleTargetUnregistered;
            StopActiveTutorial(hideCanvas: true);
        }

        private void Start()
        {
            if (_autoStartOnLevelLoad)
            {
                StartCoroutine(BootstrapRoutine());
            }
        }

        private IEnumerator BootstrapRoutine()
        {
            yield return null;
            yield return new WaitUntil(() => GameManager.Instance != null && LevelManager.Instance != null && LevelManager.Instance.CurrentLevelRoot != null);
            TryStartTutorialForCurrentLevel();
        }

        private void HandleLevelLoaded(LevelRoot _)
        {
            if (_autoStartOnLevelLoad)
            {
                TryStartTutorialForCurrentLevel();
            }
        }

        private void HandleGameStateChanged(GameState state)
        {
            if (state != GameState.Playing)
            {
                StopActiveTutorial(hideCanvas: true);
            }
        }

        private void HandleTargetRegistered(TutorialTarget target)
        {
            if (!_isRunning || _activeTutorial == null || target == null)
            {
                return;
            }

            string targetId = GetActiveTargetId();
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return;
            }

            if (!string.Equals(target.TargetId, targetId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            BindActiveTarget(target);
        }

        private void HandleTargetUnregistered(TutorialTarget target)
        {
            if (target == null || _activeTarget != target)
            {
                return;
            }

            _activeTarget = null;
            _canvasController?.HideHand();
        }

        public void TryStartTutorialForCurrentLevel()
        {
            if (_isRunning || _tutorials == null || _tutorials.Count == 0)
            {
                return;
            }

            int currentLevel = SaveManager.CurrentLevel;
            foreach (var tutorial in _tutorials)
            {
                if (tutorial == null || tutorial.Level != currentLevel)
                {
                    continue;
                }

                if (tutorial.TutorialRoot == null)
                {
                    continue;
                }

                if (IsTutorialCompleted(tutorial.Level))
                {
                    continue;
                }

                StartTutorial(tutorial);
                return;
            }
        }

        public bool TryHandleTargetClick(TutorialTarget clickedTarget)
        {
            if (!_isRunning)
            {
                return false;
            }

            if (_activeTutorial == null)
            {
                return false;
            }

            if (_activeTutorial.UseSteps)
            {
                if (_activeStep == null)
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(_activeStep.TargetId))
                {
                    if (clickedTarget != null && string.Equals(clickedTarget.TargetId, _activeStep.TargetId, StringComparison.OrdinalIgnoreCase))
                    {
                        AdvanceStep();
                        return false; // Correct target: let the click pass through to gameplay!
                    }

                    return true; // Wrong target: block click!
                }

                return true;
            }

            string targetId = _activeTutorial.TargetId;
            if (!string.IsNullOrWhiteSpace(targetId))
            {
                if (clickedTarget != null && string.Equals(clickedTarget.TargetId, targetId, StringComparison.OrdinalIgnoreCase))
                {
                    CompleteActiveTutorial();
                    return false; // Correct target: let the click pass through to gameplay!
                }

                return true; // Wrong target: block click!
            }

            return true;
        }

        public void CompleteActiveStep()
        {
            if (!_isRunning)
            {
                return;
            }

            AdvanceStep();
        }

        public void CompleteActiveTutorial()
        {
            if (!_isRunning)
            {
                return;
            }

            CompleteTutorial();
        }

        private void StartTutorial(TutorialDefinition tutorial)
        {
            _activeTutorial = tutorial;
            _isRunning = true;
            _activeStepIndex = -1;
            _activeStep = null;
            _activeTarget = null;

            _activeTutorial.TutorialRoot.SetActive(true);
            _canvasController?.Show();

            if (_activeTutorial.UseSteps && _activeTutorial.Steps != null && _activeTutorial.Steps.Count > 0)
            {
                AdvanceStep();
            }
            else
            {
                BindActiveRootTarget();
            }
        }

        private void AdvanceStep()
        {
            if (_activeStep != null)
            {
                if (_activeStep.StepRoot != null)
                {
                    _activeStep.StepRoot.SetActive(false);
                }

                _canvasController?.HideHand();
            }

            _activeTarget = null;
            _activeStepIndex++;

            if (_activeTutorial == null || !_activeTutorial.UseSteps || _activeTutorial.Steps == null || _activeStepIndex >= _activeTutorial.Steps.Count)
            {
                CompleteTutorial();
                return;
            }

            _activeStep = _activeTutorial.Steps[_activeStepIndex];
            if (_activeStep == null)
            {
                AdvanceStep();
                return;
            }

            if (_activeStep.StepRoot != null)
            {
                _activeStep.StepRoot.SetActive(true);
            }

            if (string.IsNullOrWhiteSpace(_activeStep.TargetId))
            {
                _canvasController?.HideHand();
                return;
            }

            if (TutorialTarget.TryGetRegistered(_activeStep.TargetId, out var target))
            {
                BindActiveTarget(target);
            }
            else
            {
                _canvasController?.HideHand();
            }
        }

        private void BindActiveRootTarget()
        {
            _activeTarget = null;

            if (_canvasController == null || _activeTutorial == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_activeTutorial.TargetId))
            {
                _canvasController.HideHand();
                return;
            }

            if (TutorialTarget.TryGetRegistered(_activeTutorial.TargetId, out var target))
            {
                _activeTarget = target;
                _canvasController.ShowHandFollowTarget(_activeTarget, _activeTutorial.HandOffset);
            }
            else
            {
                _canvasController.HideHand();
            }
        }

        private string GetActiveTargetId()
        {
            if (_activeTutorial == null)
            {
                return null;
            }

            if (_activeTutorial.UseSteps)
            {
                return _activeStep != null ? _activeStep.TargetId : null;
            }

            return _activeTutorial.TargetId;
        }

        private void BindActiveTarget(TutorialTarget target)
        {
            _activeTarget = target;

            if (_canvasController != null && _activeTarget != null)
            {
                _canvasController.ShowHandFollowTarget(_activeTarget, _activeStep != null ? _activeStep.HandOffset : Vector3.zero);
            }
        }

        private void CompleteTutorial()
        {
            if (_activeTutorial != null && _activeTutorial.ShowOnce)
            {
                MarkTutorialCompleted(_activeTutorial.Level);
            }

            if (_activeTutorial != null && _activeTutorial.TutorialRoot != null)
            {
                _activeTutorial.TutorialRoot.SetActive(false);
            }

            _activeTutorial = null;
            _activeTarget = null;
            _activeStep = null;
            _activeStepIndex = -1;
            _isRunning = false;

            if (_canvasController != null)
            {
                _canvasController.Hide();
            }
        }

        private void StopActiveTutorial(bool hideCanvas)
        {
            if (_activeTutorial != null && _activeTutorial.TutorialRoot != null)
            {
                _activeTutorial.TutorialRoot.SetActive(false);
            }

            _activeTutorial = null;
            _activeTarget = null;
            _activeStep = null;
            _activeStepIndex = -1;
            _isRunning = false;

            if (hideCanvas && _canvasController != null)
            {
                _canvasController.Hide();
            }
        }

        private bool IsTutorialCompleted(int level)
        {
            return PlayerPrefs.GetInt(GetCompletionKey(level), 0) == 1;
        }

        private void MarkTutorialCompleted(int level)
        {
            PlayerPrefs.SetInt(GetCompletionKey(level), 1);
            PlayerPrefs.Save();
        }

        private static string GetCompletionKey(int level) => $"TutorialCompleted_Level_{level}";
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(TutorialManager))]
    public class TutorialManagerEditor : UnityEditor.Editor
    {
        private UnityEditor.SerializedProperty _canvasControllerProp;
        private UnityEditor.SerializedProperty _autoStartOnLevelLoadProp;
        private UnityEditor.SerializedProperty _tutorialsProp;

        private void OnEnable()
        {
            _canvasControllerProp = serializedObject.FindProperty("_canvasController");
            _autoStartOnLevelLoadProp = serializedObject.FindProperty("_autoStartOnLevelLoad");
            _tutorialsProp = serializedObject.FindProperty("_tutorials");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Draw presentation & behavior settings
            UnityEditor.EditorGUILayout.LabelField("General Settings", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.EditorGUILayout.PropertyField(_canvasControllerProp);
            UnityEditor.EditorGUILayout.PropertyField(_autoStartOnLevelLoadProp);
            
            UnityEditor.EditorGUILayout.Space(10);
            
            // Draw Tutorials header with utility buttons
            UnityEditor.EditorGUILayout.BeginHorizontal();
            UnityEditor.EditorGUILayout.LabelField("Tutorials List", UnityEditor.EditorStyles.boldLabel);
            if (UnityEngine.GUILayout.Button("Collapse All", UnityEditor.EditorStyles.miniButton, UnityEngine.GUILayout.Width(80)))
            {
                SetAllExpanded(false);
            }
            if (UnityEngine.GUILayout.Button("Expand All", UnityEditor.EditorStyles.miniButton, UnityEngine.GUILayout.Width(80)))
            {
                SetAllExpanded(true);
            }
            if (UnityEngine.GUILayout.Button("+ Add Tutorial", UnityEditor.EditorStyles.miniButton, UnityEngine.GUILayout.Width(100)))
            {
                _tutorialsProp.arraySize++;
            }
            UnityEditor.EditorGUILayout.EndHorizontal();

            UnityEditor.EditorGUILayout.Space(5);

            // Draw list of tutorials
            for (int i = 0; i < _tutorialsProp.arraySize; i++)
            {
                UnityEditor.SerializedProperty tutorialProp = _tutorialsProp.GetArrayElementAtIndex(i);
                DrawTutorialElement(tutorialProp, i);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawTutorialElement(UnityEditor.SerializedProperty tutorialProp, int index)
        {
            var levelProp = tutorialProp.FindPropertyRelative("_level");
            var showOnceProp = tutorialProp.FindPropertyRelative("_showOnce");
            var tutorialRootProp = tutorialProp.FindPropertyRelative("_tutorialRoot");
            var useStepsProp = tutorialProp.FindPropertyRelative("_useSteps");
            var targetIdProp = tutorialProp.FindPropertyRelative("_targetId");
            var handOffsetProp = tutorialProp.FindPropertyRelative("_handOffset");
            var stepsProp = tutorialProp.FindPropertyRelative("_steps");

            // Header Style
            UnityEngine.GUIStyle headerStyle = new UnityEngine.GUIStyle(UnityEditor.EditorStyles.helpBox);
            headerStyle.margin = new UnityEngine.RectOffset(0, 0, 4, 4);

            UnityEditor.EditorGUILayout.BeginVertical(headerStyle);
            
            UnityEditor.EditorGUILayout.BeginHorizontal();
            
            // Get label showing Level and whether it uses steps
            string label = $"Tutorial [Level {levelProp.intValue}]" + (useStepsProp.boolValue ? $" ({stepsProp.arraySize} Steps)" : " (Single Target)");
            tutorialProp.isExpanded = UnityEditor.EditorGUILayout.Foldout(tutorialProp.isExpanded, label, true);
            
            UnityEngine.GUILayout.FlexibleSpace();
            
            // Delete button
            if (UnityEngine.GUILayout.Button("✕", UnityEditor.EditorStyles.miniButton, UnityEngine.GUILayout.Width(20)))
            {
                int oldSize = _tutorialsProp.arraySize;
                _tutorialsProp.DeleteArrayElementAtIndex(index);
                if (_tutorialsProp.arraySize == oldSize)
                {
                    _tutorialsProp.DeleteArrayElementAtIndex(index);
                }
                UnityEditor.EditorGUILayout.EndHorizontal();
                UnityEditor.EditorGUILayout.EndVertical();
                return;
            }
            UnityEditor.EditorGUILayout.EndHorizontal();

            if (tutorialProp.isExpanded)
            {
                UnityEditor.EditorGUI.indentLevel++;
                UnityEditor.EditorGUILayout.Space(4);
                
                UnityEditor.EditorGUILayout.PropertyField(levelProp);
                UnityEditor.EditorGUILayout.PropertyField(showOnceProp);
                UnityEditor.EditorGUILayout.PropertyField(tutorialRootProp);
                
                UnityEditor.EditorGUILayout.Space(4);
                UnityEditor.EditorGUILayout.PropertyField(useStepsProp);

                if (useStepsProp.boolValue)
                {
                    UnityEditor.EditorGUILayout.Space(6);
                    UnityEditor.EditorGUILayout.BeginHorizontal();
                    UnityEditor.EditorGUILayout.LabelField("Steps", UnityEditor.EditorStyles.boldLabel);
                    if (UnityEngine.GUILayout.Button("+ Add Step", UnityEditor.EditorStyles.miniButton, UnityEngine.GUILayout.Width(80)))
                    {
                        stepsProp.arraySize++;
                    }
                    UnityEditor.EditorGUILayout.EndHorizontal();

                    UnityEditor.EditorGUI.indentLevel++;
                    for (int s = 0; s < stepsProp.arraySize; s++)
                    {
                        UnityEditor.SerializedProperty stepProp = stepsProp.GetArrayElementAtIndex(s);
                        DrawStepElement(stepsProp, stepProp, s);
                    }
                    UnityEditor.EditorGUI.indentLevel--;
                }
                else
                {
                    UnityEditor.EditorGUILayout.PropertyField(targetIdProp);
                    UnityEditor.EditorGUILayout.PropertyField(handOffsetProp);
                }
                
                UnityEditor.EditorGUILayout.Space(4);
                UnityEditor.EditorGUI.indentLevel--;
            }

            UnityEditor.EditorGUILayout.EndVertical();
        }

        private void DrawStepElement(UnityEditor.SerializedProperty stepsProp, UnityEditor.SerializedProperty stepProp, int index)
        {
            var stepRootProp = stepProp.FindPropertyRelative("_stepRoot");
            var targetIdProp = stepProp.FindPropertyRelative("_targetId");
            var handOffsetProp = stepProp.FindPropertyRelative("_handOffset");

            UnityEditor.EditorGUILayout.BeginVertical(UnityEditor.EditorStyles.helpBox);
            UnityEditor.EditorGUILayout.BeginHorizontal();
            
            string stepLabel = $"Step {index + 1}" + (stepRootProp.objectReferenceValue != null ? $" ({stepRootProp.objectReferenceValue.name})" : "");
            stepProp.isExpanded = UnityEditor.EditorGUILayout.Foldout(stepProp.isExpanded, stepLabel, true);
            
            UnityEngine.GUILayout.FlexibleSpace();
            
            if (UnityEngine.GUILayout.Button("✕", UnityEditor.EditorStyles.miniButton, UnityEngine.GUILayout.Width(20)))
            {
                int oldSize = stepsProp.arraySize;
                stepsProp.DeleteArrayElementAtIndex(index);
                if (stepsProp.arraySize == oldSize)
                {
                    stepsProp.DeleteArrayElementAtIndex(index);
                }
                UnityEditor.EditorGUILayout.EndHorizontal();
                UnityEditor.EditorGUILayout.EndVertical();
                return;
            }
            UnityEditor.EditorGUILayout.EndHorizontal();

            if (stepProp.isExpanded)
            {
                UnityEditor.EditorGUI.indentLevel++;
                UnityEditor.EditorGUILayout.PropertyField(stepRootProp);
                UnityEditor.EditorGUILayout.PropertyField(targetIdProp);
                UnityEditor.EditorGUILayout.PropertyField(handOffsetProp);
                UnityEditor.EditorGUI.indentLevel--;
            }

            UnityEditor.EditorGUILayout.EndVertical();
        }

        private void SetAllExpanded(bool expanded)
        {
            for (int i = 0; i < _tutorialsProp.arraySize; i++)
            {
                UnityEditor.SerializedProperty tutorialProp = _tutorialsProp.GetArrayElementAtIndex(i);
                tutorialProp.isExpanded = expanded;
                
                var stepsProp = tutorialProp.FindPropertyRelative("_steps");
                if (stepsProp != null)
                {
                    for (int s = 0; s < stepsProp.arraySize; s++)
                    {
                        stepsProp.GetArrayElementAtIndex(s).isExpanded = expanded;
                    }
                }
            }
        }
    }
#endif
}
