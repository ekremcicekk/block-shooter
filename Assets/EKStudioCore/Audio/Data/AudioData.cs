using System.Collections.Generic;
using UnityEngine;

namespace EKStudio.Audio
{
    /// <summary>
    /// Simple ScriptableObject for storing audio clips
    /// </summary>
    [CreateAssetMenu(fileName = "AudioData", menuName = "EKStudio/Audio/Audio Data", order = 1)]
    public class AudioData : ScriptableObject
    {
        [Header("Audio Clips")]
        [Tooltip("List of all audio clips in the game")]
        public List<AudioClipData> audioClips = new List<AudioClipData>();

        private Dictionary<string, AudioClip> clipDictionary;

        /// <summary>
        /// Get audio clip by name
        /// </summary>
        public AudioClip GetClip(string soundName)
        {
            if (clipDictionary == null)
                BuildDictionary();

            if (clipDictionary != null && clipDictionary.ContainsKey(soundName))
                return clipDictionary[soundName];

            return null;
        }

        /// <summary>
        /// Check if a sound exists
        /// </summary>
        public bool HasSound(string soundName)
        {
            if (clipDictionary == null)
                BuildDictionary();

            return clipDictionary != null && clipDictionary.ContainsKey(soundName);
        }

        /// <summary>
        /// Add a new audio clip
        /// </summary>
        public void AddClip(AudioClipData clipData)
        {
            if (clipData == null || string.IsNullOrEmpty(clipData.soundName) || clipData.clip == null)
            {
                Debug.LogWarning("Invalid audio clip data!");
                return;
            }

            if (!audioClips.Exists(x => x.soundName == clipData.soundName))
            {
                audioClips.Add(clipData);
                BuildDictionary();
            }
            else
            {
                Debug.LogWarning($"Audio clip with name '{clipData.soundName}' already exists!");
            }
        }

        /// <summary>
        /// Remove an audio clip
        /// </summary>
        public void RemoveClip(string soundName)
        {
            audioClips.RemoveAll(x => x.soundName == soundName);
            BuildDictionary();
        }

        private void BuildDictionary()
        {
            clipDictionary = new Dictionary<string, AudioClip>();
            
            foreach (var clipData in audioClips)
            {
                if (clipData != null && clipData.clip != null && !string.IsNullOrEmpty(clipData.soundName))
                {
                    if (!clipDictionary.ContainsKey(clipData.soundName))
                        clipDictionary[clipData.soundName] = clipData.clip;
                    else
                        Debug.LogWarning($"Duplicate sound name found: {clipData.soundName}");
                }
            }
        }

        private void OnValidate()
        {
            BuildDictionary();
        }
    }
}

