# Audio Module

## ⚙️ About

<aside>
The Audio Module is responsible for playing sounds and music in your game.

**Sounds** are stored in the AudioData ScriptableObject and played using AudioController.
**Music** is played using AudioController's music system.
**Volume** and mute settings are managed through AudioController and SettingsManager.

</aside>

## 📄 Components

**AudioController.cs** - Main script responsible for sound playback, volume control and AudioSource management.

**AudioData.cs** - ScriptableObject database that stores references to audio files.

**AudioManager.cs** - Bridge between EventManager and AudioController for backward compatibility.

**SettingsManager.cs** - UI manager for sound/music mute controls.

## 🪄 Usage

## How to play sound

### Basic way (Recommended)

Use the AudioHelper static methods for easy sound playback:

```csharp
using EKStudio.Audio;

public class Test : MonoBehaviour
{
    public void PlayWinSound()
    {
        AudioHelper.PlaySound("WinSound");
    }
    
    public void PlayMusic()
    {
        AudioHelper.PlayMusic("BackgroundMusic", loop: true);
    }
}
```

### EventManager way (Backward Compatible)

Your existing code will continue to work without changes:

```csharp
// These work exactly as before
EventManager.Broadcast(GameEvent.OnPlaySound, "WinSound", 0);
EventManager.Broadcast(GameEvent.OnPlaySound, "MatchSound", 0);
EventManager.Broadcast(GameEvent.OnSoundStart, "BackgroundMusic");
EventManager.Broadcast(GameEvent.OnSoundStop);
```

### Direct AudioController way

```csharp
using EKStudio.Audio;

public class Test : MonoBehaviour
{
    public void PlaySound()
    {
        AudioController.Instance.PlaySound("WinSound");
        AudioController.Instance.PlayMusic("BackgroundMusic", loop: true);
    }
}
```

## How to control volume

### Using AudioHelper (Recommended)

```csharp
using EKStudio.Audio;

// Set volumes (0-1 range)
AudioHelper.SetMasterVolume(0.8f);
AudioHelper.SetMusicVolume(0.6f);
AudioHelper.SetSfxVolume(1.0f);

// Toggle mute
AudioHelper.ToggleMasterMute();
AudioHelper.ToggleMusicMute();
AudioHelper.ToggleSfxMute();
```

### Using AudioController directly

```csharp
using EKStudio.Audio;

// Set volumes
AudioController.Instance.MasterVolume = 0.8f;
AudioController.Instance.MusicVolume = 0.6f;
AudioController.Instance.SfxVolume = 1.0f;

// Toggle mute
AudioController.Instance.IsMasterMuted = true;
AudioController.Instance.IsMusicMuted = false;
AudioController.Instance.IsSfxMuted = false;
```

## How to stop sounds

```csharp
using EKStudio.Audio;

// Stop all sounds
AudioHelper.StopAllSounds();

// Stop music only
AudioHelper.StopMusic();

// Pause/Resume music
AudioHelper.PauseMusic();
AudioHelper.ResumeMusic();
```

## 🛠️ Setup

### 1. Create AudioData

1. Open **Tools > Audio Settings** in Unity
2. Click "Create AudioData" button
3. Save the AudioData asset in your project (e.g., `Assets/YourGame/Data/AudioData.asset`)

### 2. Add Sounds

**Method 1: Auto Import**
1. Place your sound files in `Assets/YourGame_Files/Game/Audio/Sounds/`
2. In Audio Settings window, click "Import from Default Path"
3. All sounds will be imported automatically

**Method 2: Manual Add**
1. In Audio Settings window, expand "Add New Sound" section
2. Enter sound name, select AudioClip, choose type
3. Click "Add Sound"

### 3. Setup AudioManager

1. Find your **Manager** GameObject in the scene
2. Select the AudioManager component
3. Assign your AudioData asset to the "Audio Data" field
4. Remove any AudioSource components from the AudioManager GameObject (AudioController manages its own)

### 4. Test the System

```csharp
// Test in code
AudioHelper.PlaySound("WinSound");
AudioHelper.PlayMusic("BackgroundMusic");
```

## 🎵 Sound Types

- **UI** - Button clicks, menu sounds
- **Gameplay** - Game mechanics sounds  
- **Ambient** - Background environmental sounds
- **Music** - Music tracks
- **Special** - Special effect sounds
- **Other** - Other sound types

## 🎛️ Audio Settings Window

Access via: **Tools > Audio Settings**

### Features:
- 📊 **Statistics** - View sound count by type
- ➕ **Add Sounds** - Add new sounds manually
- 📁 **Import** - Auto-import from folders
- 🔍 **Search & Filter** - Find sounds easily
- 📍 **Location** - Click sound names to locate in project
- 🎨 **Modern UI** - Clean and intuitive interface

## 🔧 Troubleshooting

### Sounds not playing?

**Checklist:**
1. ✅ AudioData created and assigned to AudioManager?
2. ✅ Sounds added to AudioData?
3. ✅ AudioManager GameObject has AudioData assigned?
4. ✅ AudioSource components removed from AudioManager GameObject?
5. ✅ Sound names spelled correctly?

### Volume not working?

**Solution:** AudioController automatically creates when first sound is played. Make sure SettingsManager is using the new audio system.

### Settings not saving?

**Solution:** Volume and mute settings are automatically saved using PlayerPrefs. They persist between game sessions.

## 💡 Best Practices

1. **Use AudioHelper** - Simplest API for most use cases
2. **Organize by Type** - Use SoundType to categorize sounds
3. **Use Descriptive Names** - Make sound names clear and searchable
4. **Find in Project** - Click on sound names to locate them in project
5. **Keep AudioData Updated** - Import new sounds regularly
6. **Test in Editor** - Use the Audio Settings window to manage sounds

## 📁 File Structure

```
EKStudioCore/Audio/
├── Core/
│   ├── AudioController.cs      # Main audio controller
│   └── AudioHelper.cs          # Static helper methods
├── Data/
│   ├── AudioData.cs            # ScriptableObject for audio clips
│   ├── AudioClipData.cs        # Audio clip data class
│   └── SoundType.cs            # Sound type enum
└── Editor/
    ├── AudioSettingsWindow.cs  # Editor window
    └── AudioDataEditor.cs      # Custom inspector
```

## 🔄 Integration

The audio system is fully integrated with existing EventManager-based audio systems.

**Existing code continues to work seamlessly:**
```csharp
EventManager.Broadcast(GameEvent.OnPlaySound, "WinSound", 0);
EventManager.Broadcast(GameEvent.OnSoundStart, "BackgroundMusic");
EventManager.Broadcast(GameEvent.OnSoundStop);
```

**Settings integration:**
- SettingsManager automatically uses AudioController
- No additional setup required
- Existing UI buttons work with new audio system

---
