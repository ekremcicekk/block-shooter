using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EKStudio.Audio
{
    /// <summary>
    /// Main audio controller for managing all sounds and music in the game
    /// </summary>
    public class AudioController : MonoBehaviour
    {
        private static AudioController _instance;
        public static AudioController Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject audioObject = new GameObject("AudioController");
                    _instance = audioObject.AddComponent<AudioController>();
                    DontDestroyOnLoad(audioObject);
                }
                return _instance;
            }
        }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource secondSfxSource;

        [Header("Audio Data")]
        [SerializeField] private AudioData audioData;

        private Dictionary<string, AudioClip> audioClipsCache = new Dictionary<string, AudioClip>();
        
        // Volume settings
        private float _masterVolume = 1f;
        private float _musicVolume = 1f;
        private float _sfxVolume = 1f;
        
        // Mute settings
        private bool _isMasterMuted = false;
        private bool _isMusicMuted = false;
        private bool _isSfxMuted = false;

        private const string MASTER_VOLUME_KEY = "EK_MasterVolume";
        private const string MUSIC_VOLUME_KEY = "EK_MusicVolume";
        private const string SFX_VOLUME_KEY = "EK_SfxVolume";
        private const string MASTER_MUTE_KEY = "EK_MasterMute";
        private const string MUSIC_MUTE_KEY = "EK_MusicMute";
        private const string SFX_MUTE_KEY = "EK_SfxMute";

        public float MasterVolume
        {
            get => _masterVolume;
            set
            {
                _masterVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(MASTER_VOLUME_KEY, _masterVolume);
                UpdateVolumes();
            }
        }

        public float MusicVolume
        {
            get => _musicVolume;
            set
            {
                _musicVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, _musicVolume);
                UpdateVolumes();
            }
        }

        public float SfxVolume
        {
            get => _sfxVolume;
            set
            {
                _sfxVolume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(SFX_VOLUME_KEY, _sfxVolume);
                UpdateVolumes();
            }
        }

        public bool IsMasterMuted
        {
            get => _isMasterMuted;
            set
            {
                _isMasterMuted = value;
                PlayerPrefs.SetInt(MASTER_MUTE_KEY, value ? 1 : 0);
                UpdateVolumes();
            }
        }

        public bool IsMusicMuted
        {
            get => _isMusicMuted;
            set
            {
                _isMusicMuted = value;
                PlayerPrefs.SetInt(MUSIC_MUTE_KEY, value ? 1 : 0);
                UpdateVolumes();
            }
        }

        public bool IsSfxMuted
        {
            get => _isSfxMuted;
            set
            {
                _isSfxMuted = value;
                PlayerPrefs.SetInt(SFX_MUTE_KEY, value ? 1 : 0);
                UpdateVolumes();
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            InitializeAudioSources();
            LoadSettings();
        }

        private void InitializeAudioSources()
        {
            if (musicSource == null)
            {
                GameObject musicObj = new GameObject("MusicSource");
                musicObj.transform.SetParent(transform);
                musicSource = musicObj.AddComponent<AudioSource>();
                musicSource.loop = true;
                musicSource.playOnAwake = false;
            }

            if (sfxSource == null)
            {
                GameObject sfxObj = new GameObject("SfxSource");
                sfxObj.transform.SetParent(transform);
                sfxSource = sfxObj.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
            }

            if (secondSfxSource == null)
            {
                GameObject secondSfxObj = new GameObject("SecondSfxSource");
                secondSfxObj.transform.SetParent(transform);
                secondSfxSource = secondSfxObj.AddComponent<AudioSource>();
                secondSfxSource.playOnAwake = false;
            }
        }

        private void LoadSettings()
        {
            _masterVolume = PlayerPrefs.GetFloat(MASTER_VOLUME_KEY, 1f);
            _musicVolume = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, 1f);
            _sfxVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 1f);
            
            _isMasterMuted = PlayerPrefs.GetInt(MASTER_MUTE_KEY, 0) == 1;
            _isMusicMuted = PlayerPrefs.GetInt(MUSIC_MUTE_KEY, 0) == 1;
            _isSfxMuted = PlayerPrefs.GetInt(SFX_MUTE_KEY, 0) == 1;
            
            UpdateVolumes();
        }

        private void UpdateVolumes()
        {
            float masterMultiplier = _isMasterMuted ? 0f : _masterVolume;
            
            if (musicSource != null)
                musicSource.volume = masterMultiplier * (_isMusicMuted ? 0f : _musicVolume);
            
            if (sfxSource != null)
                sfxSource.volume = masterMultiplier * (_isSfxMuted ? 0f : _sfxVolume);
            
            if (secondSfxSource != null)
                secondSfxSource.volume = masterMultiplier * (_isSfxMuted ? 0f : _sfxVolume);
        }

        /// <summary>
        /// Play a sound effect by name
        /// </summary>
        public void PlaySound(string soundName, bool useSecondSource = false)
        {
            AudioClip clip = GetAudioClip(soundName);
            if (clip != null)
            {
                if (useSecondSource && secondSfxSource != null)
                    secondSfxSource.PlayOneShot(clip);
                else if (sfxSource != null)
                    sfxSource.PlayOneShot(clip);
            }
            else
            {
                Debug.LogWarning($"Audio clip not found: {soundName}");
            }
        }

        /// <summary>
        /// Play music by name
        /// </summary>
        public void PlayMusic(string musicName, bool loop = true)
        {
            AudioClip clip = GetAudioClip(musicName);
            if (clip != null && musicSource != null)
            {
                musicSource.clip = clip;
                musicSource.loop = loop;
                musicSource.Play();
            }
            else
            {
                Debug.LogWarning($"Music clip not found: {musicName}");
            }
        }

        /// <summary>
        /// Stop current music
        /// </summary>
        public void StopMusic()
        {
            if (musicSource != null)
                musicSource.Stop();
        }

        /// <summary>
        /// Stop all sound effects
        /// </summary>
        public void StopAllSounds()
        {
            if (sfxSource != null)
                sfxSource.Stop();
            if (secondSfxSource != null)
                secondSfxSource.Stop();
        }

        /// <summary>
        /// Pause music
        /// </summary>
        public void PauseMusic()
        {
            if (musicSource != null)
                musicSource.Pause();
        }

        /// <summary>
        /// Resume music
        /// </summary>
        public void ResumeMusic()
        {
            if (musicSource != null)
                musicSource.UnPause();
        }

        private AudioClip GetAudioClip(string clipName)
        {
            // Check cache first
            if (audioClipsCache.ContainsKey(clipName))
                return audioClipsCache[clipName];

            // Try to load from AudioData
            if (audioData != null)
            {
                AudioClip clip = audioData.GetClip(clipName);
                if (clip != null)
                {
                    audioClipsCache[clipName] = clip;
                    return clip;
                }
            }

            // Try to load from Resources
            AudioClip resourceClip = Resources.Load<AudioClip>(clipName);
            if (resourceClip != null)
            {
                audioClipsCache[clipName] = resourceClip;
                return resourceClip;
            }

            return null;
        }

        /// <summary>
        /// Set audio data reference
        /// </summary>
        public void SetAudioData(AudioData data)
        {
            audioData = data;
            audioClipsCache.Clear();
        }

        /// <summary>
        /// Reset all audio settings to default
        /// </summary>
        public void ResetToDefaults()
        {
            MasterVolume = 1f;
            MusicVolume = 1f;
            SfxVolume = 1f;
            IsMasterMuted = false;
            IsMusicMuted = false;
            IsSfxMuted = false;
        }
    }
}

