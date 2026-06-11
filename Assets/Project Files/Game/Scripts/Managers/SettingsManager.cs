using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EKStudio
{
    public class SettingsManager : MonoBehaviour
    {
        [Header("Toggles")]
        public Button soundButton;
        public Button hapticButton;

        private GameObject soundOn;
        private GameObject soundOff;
        private GameObject hapticOn;
        private GameObject hapticOff;

        [Header("Scene Transitions")]
        public Button homeButton;
        public Button restartButton;

        private void Awake()
        {
            // Initialize vibration system
            Vibration.Init();

            // Dynamically resolve references if they are null
            ResolveReferences();

            // Register toggle listeners
            if (soundButton != null)
            {
                soundButton.onClick.RemoveListener(SoundChange);
                soundButton.onClick.AddListener(SoundChange);
            }
            if (hapticButton != null)
            {
                hapticButton.onClick.RemoveListener(HapticChange);
                hapticButton.onClick.AddListener(HapticChange);
            }

            // Register navigation button listeners
            if (homeButton != null)
            {
                homeButton.onClick.RemoveListener(GoHome);
                homeButton.onClick.AddListener(GoHome);
            }
            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(RestartLevel);
                restartButton.onClick.AddListener(RestartLevel);
            }

            // Default PlayerPrefs setup
            if (!PlayerPrefs.HasKey("IsHapticOpen"))
            {
                PlayerPrefs.SetInt("IsHapticOpen", 1);
            }
            if (!PlayerPrefs.HasKey("HapticButton"))
            {
                PlayerPrefs.SetInt("HapticButton", 0); // 0 = ON
            }
            if (!PlayerPrefs.HasKey("SoundButton"))
            {
                PlayerPrefs.SetInt("SoundButton", 0); // 0 = ON
            }

            AllChange();
        }

        private void ResolveReferences()
        {
            if (soundButton == null) soundButton = FindComponentInChildren<Button>("Sound_BTN");
            if (hapticButton == null) hapticButton = FindComponentInChildren<Button>("Vibrate_BTN");
            if (homeButton == null) homeButton = FindComponentInChildren<Button>("Home_BTN");
            if (restartButton == null) restartButton = FindComponentInChildren<Button>("Restart_BTN");

            if (soundOn == null) soundOn = FindGameObjectInChildren("Sound_On");
            if (soundOff == null) soundOff = FindGameObjectInChildren("Sound_Off");
            if (hapticOn == null) hapticOn = FindGameObjectInChildren("Vibrate_On");
            if (hapticOff == null) hapticOff = FindGameObjectInChildren("Vibrate_Off");
        }

        private T FindComponentInChildren<T>(string name) where T : Component
        {
            T[] components = GetComponentsInChildren<T>(true);
            foreach (var comp in components)
            {
                if (comp.name == name)
                    return comp;
            }
            return null;
        }

        private GameObject FindGameObjectInChildren(string name)
        {
            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            foreach (var t in transforms)
            {
                if (t.name == name)
                    return t.gameObject;
            }
            return null;
        }

        public void SoundChange()
        {
            // Toggle setting: if currently off, set to 0 (on), otherwise 1 (off)
            int currentVal = PlayerPrefs.GetInt("SoundButton", 0);
            int newVal = (currentVal == 0) ? 1 : 0;
            PlayerPrefs.SetInt("SoundButton", newVal);
            AllChange();
        }

        public void HapticChange()
        {
            // Toggle setting: if currently off, set to 0 (on), otherwise 1 (off)
            int currentVal = PlayerPrefs.GetInt("HapticButton", 0);
            int newVal = (currentVal == 0) ? 1 : 0;
            PlayerPrefs.SetInt("HapticButton", newVal);
            AllChange();

            // Provide immediate tactile feedback if haptics were turned ON
            if (newVal == 0)
            {
                Vibration.VibratePop();
            }
        }

        public void AllChange()
        {
            // Update sound states
            if (PlayerPrefs.GetInt("SoundButton", 0) == 0)
            {
                // Sound ON
                AudioListener.volume = 1f;
                if (EKStudio.Audio.AudioController.Instance != null)
                {
                    EKStudio.Audio.AudioController.Instance.IsMasterMuted = false;
                }
                if (soundOn != null) soundOn.SetActive(true);
                if (soundOff != null) soundOff.SetActive(false);
            }
            else
            {
                // Sound OFF
                AudioListener.volume = 0f;
                if (EKStudio.Audio.AudioController.Instance != null)
                {
                    EKStudio.Audio.AudioController.Instance.IsMasterMuted = true;
                }
                if (soundOn != null) soundOn.SetActive(false);
                if (soundOff != null) soundOff.SetActive(true);
            }

            // Update haptic states
            if (PlayerPrefs.GetInt("HapticButton", 0) == 0)
            {
                // Haptics ON
                PlayerPrefs.SetInt("IsHapticOpen", 1);
                if (hapticOn != null) hapticOn.SetActive(true);
                if (hapticOff != null) hapticOff.SetActive(false);
            }
            else
            {
                // Haptics OFF
                PlayerPrefs.SetInt("IsHapticOpen", 0);
                if (hapticOn != null) hapticOn.SetActive(false);
                if (hapticOff != null) hapticOff.SetActive(true);
            }
        }

        public void GoHome()
        {
            DG.Tweening.DOTween.KillAll();
            Time.timeScale = 1f;
            if (BlockShooter.LevelManager.Instance != null)
            {
                BlockShooter.LevelManager.Instance.LoadMainMenu();
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
            }
        }

        public void RestartLevel()
        {
            DG.Tweening.DOTween.KillAll();
            Time.timeScale = 1f;
            if (BlockShooter.LevelManager.Instance != null)
            {
                BlockShooter.LevelManager.Instance.RestartLevel();
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            }
        }
    }
}