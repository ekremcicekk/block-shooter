using UnityEngine;

namespace EKStudio.Audio
{
    /// <summary>
    /// Data class for storing audio clip information
    /// </summary>
    [System.Serializable]
    public class AudioClipData
    {
        [Tooltip("Unique name for this sound")]
        public string soundName;
        
        [Tooltip("Audio clip reference")]
        public AudioClip clip;
        
        [Tooltip("Type/category of this sound")]
        public SoundType soundType = SoundType.Gameplay;
        
        [Tooltip("Description of this sound")]
        [TextArea(2, 4)]
        public string description;

        public AudioClipData(string name, AudioClip audioClip, SoundType type = SoundType.Gameplay)
        {
            soundName = name;
            clip = audioClip;
            soundType = type;
        }
    }
}

