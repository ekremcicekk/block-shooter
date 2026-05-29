using UnityEngine;

namespace EKStudio.Audio
{
    /// <summary>
    /// Static helper class for easy audio playback
    /// </summary>
    public static class AudioHelper
    {
        /// <summary>
        /// Quick method to play a sound effect
        /// </summary>
        public static void PlaySound(string soundName)
        {
            AudioController.Instance.PlaySound(soundName);
        }

        /// <summary>
        /// Quick method to play music
        /// </summary>
        public static void PlayMusic(string musicName, bool loop = true)
        {
            AudioController.Instance.PlayMusic(musicName, loop);
        }

        /// <summary>
        /// Stop current music
        /// </summary>
        public static void StopMusic()
        {
            AudioController.Instance.StopMusic();
        }

        /// <summary>
        /// Stop all sounds
        /// </summary>
        public static void StopAllSounds()
        {
            AudioController.Instance.StopAllSounds();
        }

        /// <summary>
        /// Pause music
        /// </summary>
        public static void PauseMusic()
        {
            AudioController.Instance.PauseMusic();
        }

        /// <summary>
        /// Resume music
        /// </summary>
        public static void ResumeMusic()
        {
            AudioController.Instance.ResumeMusic();
        }

        /// <summary>
        /// Set master volume (0-1)
        /// </summary>
        public static void SetMasterVolume(float volume)
        {
            AudioController.Instance.MasterVolume = volume;
        }

        /// <summary>
        /// Set music volume (0-1)
        /// </summary>
        public static void SetMusicVolume(float volume)
        {
            AudioController.Instance.MusicVolume = volume;
        }

        /// <summary>
        /// Set SFX volume (0-1)
        /// </summary>
        public static void SetSfxVolume(float volume)
        {
            AudioController.Instance.SfxVolume = volume;
        }

        /// <summary>
        /// Toggle master mute
        /// </summary>
        public static void ToggleMasterMute()
        {
            AudioController.Instance.IsMasterMuted = !AudioController.Instance.IsMasterMuted;
        }

        /// <summary>
        /// Toggle music mute
        /// </summary>
        public static void ToggleMusicMute()
        {
            AudioController.Instance.IsMusicMuted = !AudioController.Instance.IsMusicMuted;
        }

        /// <summary>
        /// Toggle SFX mute
        /// </summary>
        public static void ToggleSfxMute()
        {
            AudioController.Instance.IsSfxMuted = !AudioController.Instance.IsSfxMuted;
        }
    }
}

