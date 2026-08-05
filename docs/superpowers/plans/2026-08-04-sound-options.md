# Sound Options Panel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a player-facing Settings panel (opened from a new HUD icon) with independent Music and SFX volume sliders + mute toggles, persisted via `PlayerPrefs` and wired into `AudioManager`.

**Architecture:** `AudioManager` (`Assets/Scripts/Audio/AudioManager.cs`) gains a real volume/mute API backed by `PlayerPrefs`, replacing its two hardcoded volume fields. A new `SettingsUI` screen (UIDocument + UXML/USS, mirroring the existing `ProfileUI`/`Profile.uxml` pattern exactly) is registered with `ScreenNavigator` and opened from a new speaker-icon button in the HUD action cluster. The `Settings` scene GameObject is added by hand-editing the force-text `SampleScene.unity`, mirroring the existing `ProfileUI` GameObject block.

**Tech Stack:** Unity 6 (UI Toolkit / UIElements), C#, NUnit (`Grove.Tests` EditMode tests), `PlayerPrefs`.

## Global Constraints

- Follow the existing per-panel pattern exactly: `UIDocument`-backed `MonoBehaviour` with `SetOpen(bool)` driving `PanelAnimator.Open/Close`, registered with `ScreenNavigator`. See `Assets/Scripts/UI/ProfileUI.cs` as the reference implementation.
- Sound settings persist via `PlayerPrefs` only — never via `PlayerSaveData`/`save.json` (must survive the Demo panel's "reset save" debug action).
- All new pixel-art icons go through `PixelArtUtility` (`Assets/Scripts/UI/PixelArtUtility.cs`), matching the existing `DrawGearGlyph`/`DrawBookGlyph` style: simple filled primitives, no external image assets.
- New UXML/USS files must import `Assets/UI/Theme/Theme.uss` first, then their own local stylesheet, and use `--space-*`/`--radius-*`/`--border-*`/`--font-size-*`/`--color-*` custom properties from `Theme.uss` wherever the pattern already does (see `Profile.uss`).
- `AudioManager.Instance?.PlayTap()` fires on button clicks (close button, mute toggles) but **not** on slider drag (`RegisterValueChangedCallback` fires continuously while dragging — would spam the tap sound).
- The Unity Editor may be open on this project while these tasks run (it was open at plan-writing time). New/changed asset files must ship with hand-authored `.meta` files carrying fixed GUIDs (matching the exact format of sibling `.meta` files in this project) so Unity does not need to invent GUIDs on re-import and any references in `SampleScene.unity` stay valid immediately.
- Every fileID used in `SampleScene.unity` edits below is a fresh unique integer, verified against every existing `--- !u!NNN &ID` anchor in the file (checked via `grep -oE '&[0-9]+' Assets/Scenes/SampleScene.unity`) at plan-writing time. Do not reuse or renumber them.

---

### Task 1: AudioManager volume/mute API + persistence

**Files:**
- Modify: `Assets/Scripts/Audio/AudioManager.cs`
- Test: `Assets/Scripts/Tests/AudioManagerTests.cs` (new)

**Interfaces:**
- Produces on `AudioManager`: `float MusicVolume { get; }`, `float SfxVolume { get; }`, `bool MusicMuted { get; }`, `bool SfxMuted { get; }`, `void SetMusicVolume(float volume)`, `void SetSfxVolume(float volume)`, `void SetMusicMuted(bool muted)`, `void SetSfxMuted(bool muted)`. These are consumed by `SettingsUI` in Task 3.
- `PlayerPrefs` keys (string constants, not otherwise exposed): `"audio.musicVolume"`, `"audio.sfxVolume"`, `"audio.musicMuted"`, `"audio.sfxMuted"`.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Scripts/Tests/AudioManagerTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

public class AudioManagerTests
{
    private const string MusicVolumeKey = "audio.musicVolume";
    private const string SfxVolumeKey = "audio.sfxVolume";
    private const string MusicMutedKey = "audio.musicMuted";
    private const string SfxMutedKey = "audio.sfxMuted";

    private GameObject go;
    private AudioManager manager;

    [SetUp]
    public void SetUp()
    {
        PlayerPrefs.DeleteKey(MusicVolumeKey);
        PlayerPrefs.DeleteKey(SfxVolumeKey);
        PlayerPrefs.DeleteKey(MusicMutedKey);
        PlayerPrefs.DeleteKey(SfxMutedKey);

        go = new GameObject("TestAudioManager");
        manager = go.AddComponent<AudioManager>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(go);

        PlayerPrefs.DeleteKey(MusicVolumeKey);
        PlayerPrefs.DeleteKey(SfxVolumeKey);
        PlayerPrefs.DeleteKey(MusicMutedKey);
        PlayerPrefs.DeleteKey(SfxMutedKey);
    }

    [Test]
    public void SetMusicVolume_UpdatesMusicVolume()
    {
        manager.SetMusicVolume(0.4f);

        Assert.AreEqual(0.4f, manager.MusicVolume, 0.0001f);
    }

    [Test]
    public void SetMusicVolume_ClampsAboveOne()
    {
        manager.SetMusicVolume(2.5f);

        Assert.AreEqual(1f, manager.MusicVolume, 0.0001f);
    }

    [Test]
    public void SetMusicVolume_ClampsBelowZero()
    {
        manager.SetMusicVolume(-1f);

        Assert.AreEqual(0f, manager.MusicVolume, 0.0001f);
    }

    [Test]
    public void SetSfxVolume_UpdatesSfxVolume()
    {
        manager.SetSfxVolume(0.7f);

        Assert.AreEqual(0.7f, manager.SfxVolume, 0.0001f);
    }

    [Test]
    public void SetMusicMuted_SetsMusicMutedTrue()
    {
        manager.SetMusicMuted(true);

        Assert.IsTrue(manager.MusicMuted);
    }

    [Test]
    public void SetMusicVolume_PositiveValue_ClearsMusicMuted()
    {
        manager.SetMusicMuted(true);

        manager.SetMusicVolume(0.5f);

        Assert.IsFalse(manager.MusicMuted);
    }

    [Test]
    public void SetSfxVolume_PositiveValue_ClearsSfxMuted()
    {
        manager.SetSfxMuted(true);

        manager.SetSfxVolume(0.5f);

        Assert.IsFalse(manager.SfxMuted);
    }

    [Test]
    public void SetMusicVolume_ZeroValue_DoesNotClearMute()
    {
        manager.SetMusicMuted(true);

        manager.SetMusicVolume(0f);

        Assert.IsTrue(manager.MusicMuted);
    }

    [Test]
    public void SetMusicVolume_PersistsToPlayerPrefs()
    {
        manager.SetMusicVolume(0.33f);

        Assert.AreEqual(0.33f, PlayerPrefs.GetFloat(MusicVolumeKey), 0.0001f);
    }

    [Test]
    public void SetMusicMuted_PersistsToPlayerPrefs()
    {
        manager.SetMusicMuted(true);

        Assert.AreEqual(1, PlayerPrefs.GetInt(MusicMutedKey));
    }

    [Test]
    public void Awake_LoadsPersistedVolumeFromPlayerPrefs()
    {
        Object.DestroyImmediate(go);
        PlayerPrefs.SetFloat(SfxVolumeKey, 0.81f);

        go = new GameObject("ReloadedAudioManager");
        manager = go.AddComponent<AudioManager>();

        Assert.AreEqual(0.81f, manager.SfxVolume, 0.0001f);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

In the Unity Editor, open **Window > General > Test Runner**, select the **EditMode** tab, and run `AudioManagerTests`.
Expected: compile error or FAIL — `AudioManager` has no `MusicVolume`/`SfxVolume`/`MusicMuted`/`SfxMuted`/`SetMusicVolume`/`SetSfxVolume`/`SetMusicMuted`/`SetSfxMuted` members yet.

- [ ] **Step 3: Implement the volume/mute API**

Replace the full contents of `Assets/Scripts/Audio/AudioManager.cs` with:

```csharp
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    private const string MusicVolumeKey = "audio.musicVolume";
    private const string SfxVolumeKey = "audio.sfxVolume";
    private const string MusicMutedKey = "audio.musicMuted";
    private const string SfxMutedKey = "audio.sfxMuted";

    [SerializeField] private float sfxVolume = 0.6f;
    [SerializeField] private float musicVolume = 0.12f;
    [SerializeField] private AudioClip musicClipOverride;

    private AudioSource sfxSource;
    private AudioSource musicSource;

    private float currentMusicVolume;
    private float currentSfxVolume;
    private bool musicMuted;
    private bool sfxMuted;

    private AudioClip tapClip;
    private AudioClip plantClip;
    private AudioClip unlockClip;
    private AudioClip milestoneClip;
    private AudioClip boostClip;
    private AudioClip errorClip;
    private AudioClip researchStartClip;
    private AudioClip ambientLoopClip;

    public float MusicVolume => currentMusicVolume;
    public float SfxVolume => currentSfxVolume;
    public bool MusicMuted => musicMuted;
    public bool SfxMuted => sfxMuted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        currentMusicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, musicVolume);
        currentSfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, sfxVolume);
        musicMuted = PlayerPrefs.GetInt(MusicMutedKey, 0) == 1;
        sfxMuted = PlayerPrefs.GetInt(SfxMutedKey, 0) == 1;

        sfxSource = gameObject.AddComponent<AudioSource>();
        musicSource = gameObject.AddComponent<AudioSource>();

        BuildClips();

        // musicClipOverride is a real generated track (see docs/audio.md) wired in the Inspector;
        // falls back to the procedural pad loop if nothing has been assigned yet.
        musicSource.clip = musicClipOverride != null ? musicClipOverride : ambientLoopClip;
        musicSource.loop = true;
        musicSource.volume = EffectiveMusicVolume();
        musicSource.Play();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void BuildClips()
    {
        tapClip = AudioSynthUtility.CreateSequence("sfx_tap", AudioSynthUtility.Timbre.Soft, 0.45f,
            new AudioSynthUtility.Note(880f, 0.05f));

        plantClip = AudioSynthUtility.CreateSweep("sfx_plant", 220f, 420f, 0.12f, AudioSynthUtility.Timbre.Soft, 0.55f);

        unlockClip = AudioSynthUtility.CreateSequence("sfx_unlock", AudioSynthUtility.Timbre.Bell, 0.4f,
            new AudioSynthUtility.Note(523.25f, 0.09f),
            new AudioSynthUtility.Note(659.25f, 0.09f),
            new AudioSynthUtility.Note(784.0f, 0.09f),
            new AudioSynthUtility.Note(1046.5f, 0.18f));

        milestoneClip = AudioSynthUtility.CreateSequence("sfx_milestone", AudioSynthUtility.Timbre.Bell, 0.5f,
            new AudioSynthUtility.Note(523.25f, 0.1f),
            new AudioSynthUtility.Note(659.25f, 0.1f),
            new AudioSynthUtility.Note(784.0f, 0.1f),
            new AudioSynthUtility.Note(1568.0f, 0.34f));

        boostClip = AudioSynthUtility.CreateSequence("sfx_boost", AudioSynthUtility.Timbre.Bell, 0.45f,
            new AudioSynthUtility.Note(659.25f, 0.07f),
            new AudioSynthUtility.Note(880.0f, 0.12f));

        errorClip = AudioSynthUtility.CreateSequence("sfx_error", AudioSynthUtility.Timbre.Warm, 0.4f,
            new AudioSynthUtility.Note(220f, 0.09f),
            new AudioSynthUtility.Note(174.6f, 0.16f));

        researchStartClip = AudioSynthUtility.CreateSequence("sfx_research_start", AudioSynthUtility.Timbre.Soft, 0.4f,
            new AudioSynthUtility.Note(440f, 0.07f),
            new AudioSynthUtility.Note(554.37f, 0.1f));

        ambientLoopClip = AudioSynthUtility.CreateAmbientLoop("music_ambient_loop", 8f, 1f);
    }

    public void SetMusicVolume(float volume)
    {
        currentMusicVolume = Mathf.Clamp01(volume);
        if (currentMusicVolume > 0f)
        {
            musicMuted = false;
        }

        if (musicSource != null)
        {
            musicSource.volume = EffectiveMusicVolume();
        }

        PlayerPrefs.SetFloat(MusicVolumeKey, currentMusicVolume);
        PlayerPrefs.SetInt(MusicMutedKey, musicMuted ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetSfxVolume(float volume)
    {
        currentSfxVolume = Mathf.Clamp01(volume);
        if (currentSfxVolume > 0f)
        {
            sfxMuted = false;
        }

        PlayerPrefs.SetFloat(SfxVolumeKey, currentSfxVolume);
        PlayerPrefs.SetInt(SfxMutedKey, sfxMuted ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetMusicMuted(bool muted)
    {
        musicMuted = muted;

        if (musicSource != null)
        {
            musicSource.volume = EffectiveMusicVolume();
        }

        PlayerPrefs.SetInt(MusicMutedKey, musicMuted ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetSfxMuted(bool muted)
    {
        sfxMuted = muted;
        PlayerPrefs.SetInt(SfxMutedKey, sfxMuted ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void PlayTap()
    {
        PlaySfx(tapClip);
    }

    public void PlayPlant()
    {
        PlaySfx(plantClip);
    }

    public void PlayUnlock()
    {
        PlaySfx(unlockClip);
    }

    public void PlayMilestone()
    {
        PlaySfx(milestoneClip);
    }

    public void PlayBoost()
    {
        PlaySfx(boostClip);
    }

    public void PlayError()
    {
        PlaySfx(errorClip);
    }

    public void PlayResearchStart()
    {
        PlaySfx(researchStartClip);
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null || sfxSource == null)
        {
            return;
        }

        sfxSource.PlayOneShot(clip, EffectiveSfxVolume());
    }

    private float EffectiveMusicVolume()
    {
        return musicMuted ? 0f : currentMusicVolume;
    }

    private float EffectiveSfxVolume()
    {
        return sfxMuted ? 0f : currentSfxVolume;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

In the Test Runner (EditMode tab), run `AudioManagerTests` again.
Expected: all 11 tests PASS (green).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Audio/AudioManager.cs Assets/Scripts/Tests/AudioManagerTests.cs
git commit -m "feat: add volume/mute API and PlayerPrefs persistence to AudioManager"
```

---

### Task 2: Speaker pixel-art glyph

**Files:**
- Modify: `Assets/Scripts/UI/PixelArtUtility.cs`

**Interfaces:**
- Produces: `public static void DrawSpeakerGlyph(PixelArtUtility.PixelBuffer canvas)`, usable with `PixelArtUtility.CreateGlyphSprite(int size, System.Action<PixelBuffer> drawGlyph)` exactly like the existing `DrawGearGlyph`. Consumed by `HudUI` in Task 5.

- [ ] **Step 1: Add the glyph method**

In `Assets/Scripts/UI/PixelArtUtility.cs`, immediately after the closing brace of `DrawGearGlyph` (the method ending right before `public static Sprite CreateVegetationSprite(...)`), insert:

```csharp

    public static void DrawSpeakerGlyph(PixelBuffer canvas)
    {
        int size = canvas.Width;
        Color body = new Color(0.92f, 0.92f, 0.95f);
        Color wave = new Color(0.55f, 0.85f, 0.95f);

        FillRect(canvas, new RectInt((int)(size * 0.14f), (int)(size * 0.38f), (int)(size * 0.16f), (int)(size * 0.24f)), body);
        FillSpeakerCone(canvas, size, body);
        FillCircle(canvas, new Vector2(size * 0.62f, size * 0.5f), size * 0.14f, Color.clear, wave, Mathf.Max(2, size / 24));
        FillCircle(canvas, new Vector2(size * 0.62f, size * 0.5f), size * 0.26f, Color.clear, wave, Mathf.Max(2, size / 24));
    }

    private static void FillSpeakerCone(PixelBuffer canvas, int size, Color fill)
    {
        int xMin = (int)(size * 0.3f);
        int xMax = (int)(size * 0.5f);
        int centerY = (int)(size * 0.5f);
        int halfHeightAtLeft = (int)(size * 0.12f);
        int halfHeightAtRight = (int)(size * 0.28f);

        for (int x = xMin; x <= xMax; x++)
        {
            float t = (x - xMin) / (float)Mathf.Max(1, xMax - xMin);
            int halfHeight = Mathf.RoundToInt(Mathf.Lerp(halfHeightAtLeft, halfHeightAtRight, t));

            for (int y = centerY - halfHeight; y <= centerY + halfHeight; y++)
            {
                canvas.Blend(x, y, fill);
            }
        }
    }
```

`FillCircle` with `Color.clear` fill and a `border` color draws a ring (see its use in `DrawStarGlyph`), which is how the two "sound wave" arcs are rendered here.

- [ ] **Step 2: Verify it compiles**

In the Unity Editor, wait for script compilation to finish (bottom-right spinner) and confirm the Console shows no new errors referencing `PixelArtUtility.cs`.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/PixelArtUtility.cs
git commit -m "feat: add speaker pixel-art glyph for the settings icon"
```

---

### Task 3: Settings panel — UXML/USS/PanelSettings + SettingsUI script

**Files:**
- Create: `Assets/UI/Settings.meta`
- Create: `Assets/UI/Settings/Settings.uxml`
- Create: `Assets/UI/Settings/Settings.uxml.meta`
- Create: `Assets/UI/Settings/Settings.uss`
- Create: `Assets/UI/Settings/Settings.uss.meta`
- Create: `Assets/UI/Settings/SettingsPanelSettings.asset`
- Create: `Assets/UI/Settings/SettingsPanelSettings.asset.meta`
- Create: `Assets/Scripts/UI/SettingsUI.cs`
- Create: `Assets/Scripts/UI/SettingsUI.cs.meta`

**Interfaces:**
- Consumes: `AudioManager.Instance` API from Task 1 (`MusicVolume`, `SfxVolume`, `MusicMuted`, `SfxMuted`, `SetMusicVolume`, `SetSfxVolume`, `SetMusicMuted`, `SetSfxMuted`). `PanelAnimator.Open(VisualElement, VisualElement)` / `.Close(...)` (`Assets/Scripts/UI/PanelAnimator.cs`, unchanged).
- Produces: `public class SettingsUI : MonoBehaviour` with `public void SetOpen(bool visible)` and `public static bool IsOpen`, a `[SerializeField] private ScreenNavigator screenNavigator` field — same shape as `ProfileUI`. Consumed by `ScreenNavigator` (Task 4) and the scene wiring (Task 6). The C# script's GUID is `b4aee221433643779c44435b27d29b88` (baked into its `.meta` below) — Task 6's scene edit references this GUID.
- The `SettingsPanelSettings.asset` GUID is `f869f5afa1224a45a2cb05c94f95b1e6`. The `Settings.uxml` GUID is `02291b3701d14f309dcc49d0676132aa`. Task 6's scene edit references both.

- [ ] **Step 1: Create the folder meta**

Create `Assets/UI/Settings.meta`:

```
fileFormatVersion: 2
guid: 1efa7f0950c54e37bbd72f03a7ee5869
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
```

- [ ] **Step 2: Create Settings.uxml**

Create `Assets/UI/Settings/Settings.uxml`:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" editor-extension-mode="False">
    <ui:Style src="project://database/Assets/UI/Theme/Theme.uss" />
    <ui:Style src="project://database/Assets/UI/Settings/Settings.uss" />
    <ui:VisualElement name="settings-root" class="settings-root theme-scope">
        <ui:VisualElement name="settings-frame" class="settings-frame">
            <ui:VisualElement name="settings-header" class="settings-header">
                <ui:Label name="settings-title" text="Settings" class="settings-title" />
                <ui:Button name="close-button" text="X" class="settings-close-button" />
            </ui:VisualElement>
            <ui:VisualElement name="settings-content" class="settings-content">
                <ui:VisualElement class="sound-row">
                    <ui:Label text="Music" class="sound-row-label" />
                    <ui:Slider name="music-volume-slider" low-value="0" high-value="1" class="sound-row-slider" />
                    <ui:Button name="music-mute-button" text="Mute" class="mute-button" />
                </ui:VisualElement>
                <ui:VisualElement class="sound-row">
                    <ui:Label text="Sound Effects" class="sound-row-label" />
                    <ui:Slider name="sfx-volume-slider" low-value="0" high-value="1" class="sound-row-slider" />
                    <ui:Button name="sfx-mute-button" text="Mute" class="mute-button" />
                </ui:VisualElement>
            </ui:VisualElement>
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 3: Create Settings.uxml.meta**

Create `Assets/UI/Settings/Settings.uxml.meta`:

```
fileFormatVersion: 2
guid: 02291b3701d14f309dcc49d0676132aa
ScriptedImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 2
  userData: 
  assetBundleName: 
  assetBundleVariant: 
  script: {fileID: 13804, guid: 0000000000000000e000000000000000, type: 0}
```

- [ ] **Step 4: Create Settings.uss**

Create `Assets/UI/Settings/Settings.uss`:

```css
.settings-root {
    width: 100%;
    height: 100%;
    background-color: rgba(6, 8, 14, 0.85);
    padding: var(--space-sm);
    align-items: center;
    justify-content: center;
}

.settings-frame {
    width: 100%;
    height: 100%;
    max-width: 600px;
    -unity-background-scale-mode: scale-and-crop;
    border-radius: var(--radius-lg);
    border-top-width: var(--border-thick);
    border-left-width: var(--border-thick);
    border-right-width: var(--border-shadow);
    border-bottom-width: var(--border-shadow);
    border-top-color: var(--color-gold-light);
    border-left-color: var(--color-gold-light);
    border-right-color: var(--color-shadow);
    border-bottom-color: var(--color-shadow);
    padding: var(--space-xs);
    transition-property: opacity, scale;
    transition-duration: 0.18s;
    transition-timing-function: ease-out;
}

.settings-header {
    flex-direction: row;
    justify-content: space-between;
    align-items: center;
    background-color: rgb(35, 45, 58);
    border-radius: var(--radius-md);
    border-width: var(--border-thin);
    border-color: rgb(20, 26, 34);
    padding: var(--space-xs) var(--space-sm);
    margin-bottom: var(--space-xs);
    flex-shrink: 0;
}

.settings-title {
    font-size: var(--font-size-lg);
    color: rgb(230, 238, 245);
    -unity-font-style: bold;
    -unity-text-outline-width: 1;
    -unity-text-outline-color: var(--color-ink);
}

.settings-close-button {
    width: 32px;
    height: 32px;
    border-radius: var(--radius-pill);
    border-top-width: var(--border-thin);
    border-left-width: var(--border-thin);
    border-right-width: var(--border-thick);
    border-bottom-width: var(--border-thick);
    border-top-color: var(--color-danger-light);
    border-left-color: var(--color-danger-light);
    border-right-color: var(--color-shadow);
    border-bottom-color: var(--color-shadow);
    background-color: var(--color-danger);
    color: var(--text-on-dark);
    font-size: var(--font-size-sm);
    -unity-font-style: bold;
}

.settings-close-button:active {
    scale: 0.92 0.92;
}

.settings-content {
    flex-grow: 1;
    -unity-background-scale-mode: scale-and-crop;
    border-radius: var(--radius-md);
    border-width: var(--border-thin);
    border-color: rgb(70, 90, 110);
    padding: var(--space-lg);
}

.sound-row {
    flex-direction: row;
    align-items: center;
    background-color: var(--color-ink);
    border-radius: var(--radius-md);
    border-top-width: var(--border-thin);
    border-left-width: var(--border-thin);
    border-right-width: var(--border-shadow);
    border-bottom-width: var(--border-shadow);
    border-top-color: var(--color-gold-light);
    border-left-color: var(--color-gold-light);
    border-right-color: var(--color-shadow);
    border-bottom-color: var(--color-shadow);
    padding: var(--space-sm);
    margin-bottom: var(--space-sm);
}

.sound-row-label {
    width: 110px;
    flex-shrink: 0;
    font-size: var(--font-size-sm);
    color: var(--text-on-dark);
    -unity-font-style: bold;
}

.sound-row-slider {
    flex-grow: 1;
    margin-right: var(--space-sm);
}

.mute-button {
    width: 72px;
    height: 32px;
    flex-shrink: 0;
    border-radius: var(--radius-md);
    border-top-width: var(--border-thin);
    border-left-width: var(--border-thin);
    border-right-width: var(--border-thick);
    border-bottom-width: var(--border-thick);
    border-top-color: var(--color-success-light);
    border-left-color: var(--color-success-light);
    border-right-color: var(--color-shadow);
    border-bottom-color: var(--color-shadow);
    background-color: var(--color-success);
    color: var(--text-on-dark);
    font-size: var(--font-size-xs);
    -unity-font-style: bold;
}

.mute-button:active {
    scale: 0.92 0.92;
}

.mute-button--active {
    border-top-color: var(--color-danger-light);
    border-left-color: var(--color-danger-light);
    background-color: var(--color-danger);
}
```

- [ ] **Step 5: Create Settings.uss.meta**

Create `Assets/UI/Settings/Settings.uss.meta`:

```
fileFormatVersion: 2
guid: fdfcde13bf3b4827b6c0738eede8810a
ScriptedImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 2
  userData: 
  assetBundleName: 
  assetBundleVariant: 
  script: {fileID: 12385, guid: 0000000000000000e000000000000000, type: 0}
  disableValidation: 0
  unsupportedSelectorAction: 0
```

- [ ] **Step 6: Create SettingsPanelSettings.asset**

Create `Assets/UI/Settings/SettingsPanelSettings.asset` (identical to `Assets/UI/Profile/ProfilePanelSettings.asset` except `m_Name`):

```
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 19101, guid: 0000000000000000e000000000000000, type: 0}
  m_Name: SettingsPanelSettings
  m_EditorClassIdentifier: UnityEngine.dll::UnityEngine.UIElements.PanelSettings
  themeUss: {fileID: -4733365628477956816, guid: 5d3c19791e63c4131b12dd47ae94be66, type: 3}
  m_DisableNoThemeWarning: 0
  m_TargetTexture: {fileID: 0}
  m_RenderMode: 0
  m_ColliderUpdateMode: 0
  m_ColliderIsTrigger: 1
  m_ScaleMode: 1
  m_ReferenceSpritePixelsPerUnit: 100
  m_PixelsPerUnit: 100
  m_Scale: 1
  m_ReferenceDpi: 96
  m_FallbackDpi: 96
  m_ReferenceResolution: {x: 1920, y: 1080}
  m_ScreenMatchMode: 0
  m_Match: 0
  m_SortingOrder: 0
  m_TargetDisplay: 0
  m_BindingLogLevel: 0
  m_ClearDepthStencil: 1
  m_ClearColor: 0
  m_ColorClearValue: {r: 0, g: 0, b: 0, a: 0}
  m_VertexBudget: 0
  m_TextureSlotCount: 8
  m_DynamicAtlasSettings:
    m_MinAtlasSize: 64
    m_MaxAtlasSize: 4096
    m_MaxSubTextureSize: 64
    m_ActiveFilters: 31
  m_AtlasBlitShader: {fileID: 9101, guid: 0000000000000000f000000000000000, type: 0}
  m_DefaultShader: {fileID: 9100, guid: 0000000000000000f000000000000000, type: 0}
  m_RuntimeGaussianBlurShader: {fileID: 20300, guid: 0000000000000000f000000000000000, type: 0}
  m_RuntimeColorEffectShader: {fileID: 20301, guid: 0000000000000000f000000000000000, type: 0}
  m_SDFShader: {fileID: 19011, guid: 0000000000000000f000000000000000, type: 0}
  m_BitmapShader: {fileID: 9001, guid: 0000000000000000f000000000000000, type: 0}
  m_SpriteShader: {fileID: 19012, guid: 0000000000000000f000000000000000, type: 0}
  m_ICUDataAsset: {fileID: 20204, guid: 0000000000000000f000000000000000, type: 0}
  forceGammaRendering: 0
  textSettings: {fileID: 0}
```

- [ ] **Step 7: Create SettingsPanelSettings.asset.meta**

Create `Assets/UI/Settings/SettingsPanelSettings.asset.meta`:

```
fileFormatVersion: 2
guid: f869f5afa1224a45a2cb05c94f95b1e6
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
```

- [ ] **Step 8: Create SettingsUI.cs**

Create `Assets/Scripts/UI/SettingsUI.cs`:

```csharp
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class SettingsUI : MonoBehaviour
{
    [SerializeField] private ScreenNavigator screenNavigator;

    private UIDocument uiDocument;
    private VisualElement settingsRoot;
    private VisualElement settingsFrame;
    private Slider musicVolumeSlider;
    private Slider sfxVolumeSlider;
    private Button musicMuteButton;
    private Button sfxMuteButton;

    public static bool IsOpen { get; private set; }

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        VisualElement root = uiDocument.rootVisualElement;
        settingsRoot = root.Q<VisualElement>("settings-root");

        settingsFrame = root.Q<VisualElement>("settings-frame");
        settingsFrame.style.backgroundImage = new StyleBackground(
            PixelArtUtility.CreateStoneTexture(256, 256, new Color(0.16f, 0.2f, 0.27f)));

        VisualElement settingsContent = root.Q<VisualElement>("settings-content");
        settingsContent.style.backgroundImage = new StyleBackground(
            PixelArtUtility.CreateStoneTexture(256, 256, new Color(0.88f, 0.91f, 0.94f)));

        musicVolumeSlider = root.Q<Slider>("music-volume-slider");
        sfxVolumeSlider = root.Q<Slider>("sfx-volume-slider");
        musicMuteButton = root.Q<Button>("music-mute-button");
        sfxMuteButton = root.Q<Button>("sfx-mute-button");

        musicVolumeSlider.RegisterValueChangedCallback(OnMusicVolumeChanged);
        sfxVolumeSlider.RegisterValueChangedCallback(OnSfxVolumeChanged);
        musicMuteButton.clicked += OnMusicMuteButtonClicked;
        sfxMuteButton.clicked += OnSfxMuteButtonClicked;

        root.Q<Button>("close-button").clicked += OnCloseButtonClicked;

        // Direct hide (not SetOpen/PanelAnimator): this is the initial startup state, not a
        // user-triggered close, so it must be instant with zero flash - no 180ms animated delay.
        settingsRoot.style.display = DisplayStyle.None;
    }

    private void OnDisable()
    {
        IsOpen = false;
    }

    public void SetOpen(bool visible)
    {
        if (visible)
        {
            Refresh();
            PanelAnimator.Open(settingsRoot, settingsFrame);
        }
        else
        {
            PanelAnimator.Close(settingsRoot, settingsFrame);
        }

        IsOpen = visible;
    }

    private void OnCloseButtonClicked()
    {
        AudioManager.Instance?.PlayTap();

        if (screenNavigator != null)
        {
            screenNavigator.GoHome();
        }
        else
        {
            SetOpen(false);
        }
    }

    private void OnMusicVolumeChanged(ChangeEvent<float> evt)
    {
        if (AudioManager.Instance == null)
        {
            return;
        }

        AudioManager.Instance.SetMusicVolume(evt.newValue);
        RefreshMuteButton(musicMuteButton, AudioManager.Instance.MusicMuted);
    }

    private void OnSfxVolumeChanged(ChangeEvent<float> evt)
    {
        if (AudioManager.Instance == null)
        {
            return;
        }

        AudioManager.Instance.SetSfxVolume(evt.newValue);
        RefreshMuteButton(sfxMuteButton, AudioManager.Instance.SfxMuted);
    }

    private void OnMusicMuteButtonClicked()
    {
        AudioManager.Instance?.PlayTap();

        if (AudioManager.Instance == null)
        {
            return;
        }

        AudioManager.Instance.SetMusicMuted(!AudioManager.Instance.MusicMuted);
        RefreshMuteButton(musicMuteButton, AudioManager.Instance.MusicMuted);
    }

    private void OnSfxMuteButtonClicked()
    {
        AudioManager.Instance?.PlayTap();

        if (AudioManager.Instance == null)
        {
            return;
        }

        AudioManager.Instance.SetSfxMuted(!AudioManager.Instance.SfxMuted);
        RefreshMuteButton(sfxMuteButton, AudioManager.Instance.SfxMuted);
    }

    private void Refresh()
    {
        if (AudioManager.Instance == null)
        {
            return;
        }

        musicVolumeSlider.SetValueWithoutNotify(AudioManager.Instance.MusicVolume);
        sfxVolumeSlider.SetValueWithoutNotify(AudioManager.Instance.SfxVolume);
        RefreshMuteButton(musicMuteButton, AudioManager.Instance.MusicMuted);
        RefreshMuteButton(sfxMuteButton, AudioManager.Instance.SfxMuted);
    }

    private static void RefreshMuteButton(Button button, bool muted)
    {
        button.text = muted ? "Unmute" : "Mute";

        if (muted)
        {
            button.AddToClassList("mute-button--active");
        }
        else
        {
            button.RemoveFromClassList("mute-button--active");
        }
    }
}
```

- [ ] **Step 9: Create SettingsUI.cs.meta**

Create `Assets/Scripts/UI/SettingsUI.cs.meta`:

```
fileFormatVersion: 2
guid: b4aee221433643779c44435b27d29b88
```

- [ ] **Step 10: Verify it compiles**

In the Unity Editor, wait for script compilation to finish and confirm the Console shows no new errors referencing `SettingsUI.cs`, `Settings.uxml`, or `Settings.uss`. (The panel is not reachable in-game yet — that's Task 6 — so there is nothing to click yet.)

- [ ] **Step 11: Commit**

```bash
git add Assets/UI/Settings.meta Assets/UI/Settings/ Assets/Scripts/UI/SettingsUI.cs Assets/Scripts/UI/SettingsUI.cs.meta
git commit -m "feat: add Settings panel UI (UXML/USS/PanelSettings + SettingsUI script)"
```

---

### Task 4: ScreenNavigator wiring

**Files:**
- Modify: `Assets/Scripts/UI/ScreenNavigator.cs`

**Interfaces:**
- Consumes: `SettingsUI` type and its `SetOpen(bool)` method from Task 3.
- Produces: `public void OpenSettings()` on `ScreenNavigator`, consumed by `HudUI` in Task 5 and referenced by name in the scene (already wired to the `ScreenNavigator` GameObject, which Task 6 also edits).

- [ ] **Step 1: Add the settingsUI field**

In `Assets/Scripts/UI/ScreenNavigator.cs`, change:

```csharp
    [SerializeField] private HudUI hudUI;
    [SerializeField] private CodexUI codexUI;
    [SerializeField] private LaboratoryUI laboratoryUI;
    [SerializeField] private ProfileUI profileUI;
    [SerializeField] private DemoUI demoUI;
```

to:

```csharp
    [SerializeField] private HudUI hudUI;
    [SerializeField] private CodexUI codexUI;
    [SerializeField] private LaboratoryUI laboratoryUI;
    [SerializeField] private ProfileUI profileUI;
    [SerializeField] private DemoUI demoUI;
    [SerializeField] private SettingsUI settingsUI;
```

- [ ] **Step 2: Add OpenSettings()**

Add this method right after `OpenProfile()`:

```csharp
    public void OpenSettings()
    {
        CloseOtherPanels(settingsUI);
        if (settingsUI != null) settingsUI.SetOpen(true);
        SetHomeToolbarVisible(false);
    }
```

- [ ] **Step 3: Close settingsUI in CloseOtherPanels**

In `CloseOtherPanels`, change:

```csharp
        if (demoUI != null && !ReferenceEquals(demoUI, except))
        {
            demoUI.SetOpen(false);
        }
    }
```

to:

```csharp
        if (demoUI != null && !ReferenceEquals(demoUI, except))
        {
            demoUI.SetOpen(false);
        }

        if (settingsUI != null && !ReferenceEquals(settingsUI, except))
        {
            settingsUI.SetOpen(false);
        }
    }
```

- [ ] **Step 4: Verify it compiles**

In the Unity Editor, confirm the Console shows no new errors referencing `ScreenNavigator.cs`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/ScreenNavigator.cs
git commit -m "feat: register Settings panel with ScreenNavigator"
```

---

### Task 5: HUD entry point

**Files:**
- Modify: `Assets/UI/Hud/Hud.uxml`
- Modify: `Assets/Scripts/UI/HudUI.cs`

**Interfaces:**
- Consumes: `PixelArtUtility.DrawSpeakerGlyph` (Task 2), `SettingsUI` type (Task 3), `ScreenNavigator.OpenSettings()` (Task 4).
- Produces: a `settings-button` element in the compiled `Hud.uxml`, and a `[SerializeField] private SettingsUI settingsUI` field on `HudUI`, both referenced by the scene edit in Task 6.

- [ ] **Step 1: Add the button to Hud.uxml**

In `Assets/UI/Hud/Hud.uxml`, change:

```xml
            <ui:Button name="lab-button" class="icon-button" />
            <ui:Button name="codex-button" class="icon-button" />
            <ui:Button name="profile-button" class="icon-button" />
        </ui:VisualElement>
```

to:

```xml
            <ui:Button name="lab-button" class="icon-button" />
            <ui:Button name="codex-button" class="icon-button" />
            <ui:Button name="profile-button" class="icon-button" />
            <ui:Button name="settings-button" class="icon-button" />
        </ui:VisualElement>
```

- [ ] **Step 2: Add the settingsUI field to HudUI**

In `Assets/Scripts/UI/HudUI.cs`, change:

```csharp
    [SerializeField] private ScreenNavigator screenNavigator;
    [SerializeField] private CodexUI codexUI;
    [SerializeField] private LaboratoryUI laboratoryUI;
    [SerializeField] private ProfileUI profileUI;
    [SerializeField] private DemoUI demoUI;
    [SerializeField] private TreePlanter treePlanter;
    [SerializeField] private MilestoneManager milestoneManager;
```

to:

```csharp
    [SerializeField] private ScreenNavigator screenNavigator;
    [SerializeField] private CodexUI codexUI;
    [SerializeField] private LaboratoryUI laboratoryUI;
    [SerializeField] private ProfileUI profileUI;
    [SerializeField] private DemoUI demoUI;
    [SerializeField] private SettingsUI settingsUI;
    [SerializeField] private TreePlanter treePlanter;
    [SerializeField] private MilestoneManager milestoneManager;
```

- [ ] **Step 3: Wire the button in OnEnable**

In `Assets/Scripts/UI/HudUI.cs`, change:

```csharp
        Button profileButton = root.Q<Button>("profile-button");
        profileButton.clicked += OnProfileButtonClicked;
        profileButton.style.backgroundColor = new Color(0.73f, 0.54f, 0.24f);
        profileButton.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateGlyphSprite(IconSize, PixelArtUtility.DrawPersonGlyph));

        Button demoButton = root.Q<Button>("demo-button");
```

to:

```csharp
        Button profileButton = root.Q<Button>("profile-button");
        profileButton.clicked += OnProfileButtonClicked;
        profileButton.style.backgroundColor = new Color(0.73f, 0.54f, 0.24f);
        profileButton.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateGlyphSprite(IconSize, PixelArtUtility.DrawPersonGlyph));

        Button settingsButton = root.Q<Button>("settings-button");
        settingsButton.clicked += OnSettingsButtonClicked;
        settingsButton.style.backgroundColor = new Color(0.3f, 0.45f, 0.6f);
        settingsButton.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateGlyphSprite(IconSize, PixelArtUtility.DrawSpeakerGlyph));

        Button demoButton = root.Q<Button>("demo-button");
```

- [ ] **Step 4: Add the click handler**

In `Assets/Scripts/UI/HudUI.cs`, change:

```csharp
    private void OnProfileButtonClicked()
    {
        AudioManager.Instance?.PlayTap();

        if (screenNavigator != null)
        {
            screenNavigator.OpenProfile();
        }
        else if (profileUI != null)
        {
            profileUI.SetOpen(true);
        }
    }

    private void OnDemoButtonClicked()
```

to:

```csharp
    private void OnProfileButtonClicked()
    {
        AudioManager.Instance?.PlayTap();

        if (screenNavigator != null)
        {
            screenNavigator.OpenProfile();
        }
        else if (profileUI != null)
        {
            profileUI.SetOpen(true);
        }
    }

    private void OnSettingsButtonClicked()
    {
        AudioManager.Instance?.PlayTap();

        if (screenNavigator != null)
        {
            screenNavigator.OpenSettings();
        }
        else if (settingsUI != null)
        {
            settingsUI.SetOpen(true);
        }
    }

    private void OnDemoButtonClicked()
```

- [ ] **Step 5: Verify it compiles**

In the Unity Editor, confirm the Console shows no new errors referencing `HudUI.cs` or `Hud.uxml`.

- [ ] **Step 6: Commit**

```bash
git add Assets/UI/Hud/Hud.uxml Assets/Scripts/UI/HudUI.cs
git commit -m "feat: add settings button to HUD action cluster"
```

---

### Task 6: Scene wiring

**Files:**
- Modify: `Assets/Scenes/SampleScene.unity`

**Interfaces:**
- Consumes: `SettingsUI.cs` GUID `b4aee221433643779c44435b27d29b88` (Task 3), `SettingsPanelSettings.asset` GUID `f869f5afa1224a45a2cb05c94f95b1e6` (Task 3), `Settings.uxml` GUID `02291b3701d14f309dcc49d0676132aa` (Task 3), `settingsUI` fields on `ScreenNavigator` (Task 4) and `HudUI` (Task 5).
- New scene fileIDs used (none collide with any existing `--- !u!NNN &ID` anchor in the file): GameObject `786579303`, `SettingsUI` MonoBehaviour `219540831`, `UIDocument` MonoBehaviour `126855092`, `Transform` `896233790`.

- [ ] **Step 1: Insert the new GameObject block**

In `Assets/Scenes/SampleScene.unity`, find the end of the `ResearchManager` `Transform` block (fileID `2125939472`) — it's immediately followed by the `SceneRoots` block. Change:

```yaml
  m_Children: []
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!1660057539 &9223372036854775807
SceneRoots:
```

to:

```yaml
  m_Children: []
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!1 &786579303
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 896233790}
  - component: {fileID: 126855092}
  - component: {fileID: 219540831}
  m_Layer: 0
  m_Name: SettingsUI
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!114 &219540831
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 786579303}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: b4aee221433643779c44435b27d29b88, type: 3}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::SettingsUI
  screenNavigator: {fileID: 2005323408}
--- !u!114 &126855092
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 786579303}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 19102, guid: 0000000000000000e000000000000000, type: 0}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.dll::UnityEngine.UIElements.UIDocument
  m_PanelSettings: {fileID: 11400000, guid: f869f5afa1224a45a2cb05c94f95b1e6, type: 2}
  m_ParentUI: {fileID: 0}
  sourceAsset: {fileID: 9197481963319205126, guid: 02291b3701d14f309dcc49d0676132aa, type: 3}
  m_SortingOrder: 1
  m_Position: 0
  m_WorldSpaceSizeMode: 1
  m_WorldSpaceWidth: 1920
  m_WorldSpaceHeight: 1080
  m_PivotReferenceSize: 0
  m_Pivot: 0
  m_WorldSpaceCollider: {fileID: 0}
--- !u!4 &896233790
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 786579303}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!1660057539 &9223372036854775807
SceneRoots:
```

(`sourceAsset`'s fileID `9197481963319205126` is the fixed internal ID Unity assigns to a UXML file's root `VisualTreeAsset` — every other panel's UIDocument in this scene references that same constant, only the `guid` differs per UXML file.)

- [ ] **Step 2: Register the new Transform as a scene root**

In the same file, find the `SceneRoots` block's `m_Roots` list and change:

```yaml
  - {fileID: 3828347}
  - {fileID: 2005323409}
  - {fileID: 1644131832}
```

to:

```yaml
  - {fileID: 3828347}
  - {fileID: 2005323409}
  - {fileID: 1644131832}
  - {fileID: 896233790}
```

- [ ] **Step 3: Wire settingsUI on ScreenNavigator**

Find the `ScreenNavigator` `MonoBehaviour` block (fileID `2005323408`) and change:

```yaml
  m_EditorClassIdentifier: Grove.Runtime::ScreenNavigator
  hudUI: {fileID: 853456666}
  codexUI: {fileID: 2109674190}
  laboratoryUI: {fileID: 184606783}
  profileUI: {fileID: 576196791}
  demoUI: {fileID: 3828345}
```

to:

```yaml
  m_EditorClassIdentifier: Grove.Runtime::ScreenNavigator
  hudUI: {fileID: 853456666}
  codexUI: {fileID: 2109674190}
  laboratoryUI: {fileID: 184606783}
  profileUI: {fileID: 576196791}
  demoUI: {fileID: 3828345}
  settingsUI: {fileID: 219540831}
```

- [ ] **Step 4: Wire settingsUI on HudUI**

Find the `HudUI` `MonoBehaviour` block (fileID `853456666`) and change:

```yaml
  m_EditorClassIdentifier: Assembly-CSharp::HudUI
  screenNavigator: {fileID: 2005323408}
  codexUI: {fileID: 2109674190}
  laboratoryUI: {fileID: 184606783}
  profileUI: {fileID: 576196791}
  demoUI: {fileID: 3828345}
  treePlanter: {fileID: 34543761}
  milestoneManager: {fileID: 150096813}
```

to:

```yaml
  m_EditorClassIdentifier: Assembly-CSharp::HudUI
  screenNavigator: {fileID: 2005323408}
  codexUI: {fileID: 2109674190}
  laboratoryUI: {fileID: 184606783}
  profileUI: {fileID: 576196791}
  demoUI: {fileID: 3828345}
  settingsUI: {fileID: 219540831}
  treePlanter: {fileID: 34543761}
  milestoneManager: {fileID: 150096813}
```

- [ ] **Step 5: Verify in the Editor**

Switch to the Unity Editor. It should detect the changed `SampleScene.unity` on disk; if prompted, do **not** discard — let it reload. Open the Scene hierarchy and confirm a `SettingsUI` GameObject now exists with `UIDocument` and `SettingsUI` components, and that its `Screen Navigator` field shows `ScreenNavigator` (not `None`). Select `ScreenNavigator` and `HudUI` in the hierarchy and confirm their `Settings UI` fields show `SettingsUI` (not `None`).

- [ ] **Step 6: Commit**

```bash
git add Assets/Scenes/SampleScene.unity
git commit -m "feat: wire Settings panel into the scene"
```

---

### Task 7: Manual verification

**Files:** none (no code changes — this task is a playthrough checklist).

- [ ] **Step 1: Play the game and open Settings**

In the Unity Editor, enter Play mode. Click the new speaker icon in the HUD action cluster (bottom-right, after the profile icon).
Expected: the Settings panel opens with the same pop-in animation as Profile/Codex, showing a "Music" row and a "Sound Effects" row, each with a slider and a "Mute" button.

- [ ] **Step 2: Verify sliders control volume**

Drag the Music slider to a low value, then a high value, while music is audible.
Expected: audible music volume changes accordingly, in real time.

Trigger an SFX (e.g. plant a tree, or click the mute buttons themselves once volume is audible) while dragging the Sound Effects slider between plants.
Expected: SFX volume changes accordingly.

- [ ] **Step 3: Verify mute toggles**

Click the Music "Mute" button.
Expected: music stops immediately, button text changes to "Unmute" and turns red (danger-colored).

Click "Unmute".
Expected: music resumes at the slider's last volume, button reverts to green "Mute".

Repeat for the Sound Effects mute button using a triggered SFX (e.g. plant a tree) to confirm silence/restore.

- [ ] **Step 4: Verify slider un-mutes automatically**

Mute Music, then drag the Music slider to a nonzero value.
Expected: the Music mute button automatically reverts to "Mute" (green) and music becomes audible again.

- [ ] **Step 5: Verify panel close/reopen and other panels**

Click the Settings panel's close (X) button.
Expected: returns to the home HUD toolbar, same as closing Profile/Codex.

Reopen Settings.
Expected: slider positions and mute button states match what was left before closing.

Open Codex or Profile from the HUD, then open Settings.
Expected: the previously open panel closes (same mutual-exclusion behavior as every other panel pair).

- [ ] **Step 6: Verify persistence across a Play session restart**

With Music muted and SFX volume set to something distinctive (e.g. far left), stop Play mode, then enter Play mode again.
Expected: Music is still muted and SFX volume is still at the value you set — confirming `PlayerPrefs` persistence survives across sessions (a Play-mode stop/start round-trip is sufficient to prove this, since `PlayerPrefs` values are written to disk on every `SetX` call in Task 1, not just on quit).

- [ ] **Step 7: Verify sound settings survive a debug save reset**

Open the Demo panel (dev gear icon, or press F1) and click "Reset Save". Restart Play mode.
Expected: game progress (gems, planted trees, etc.) is reset, but the Music/SFX volume and mute state from Step 6 are unaffected — confirming sound settings are stored separately from `PlayerSaveData`/`save.json`.

---

## Self-Review

**Spec coverage:**
- Music volume/mute + SFX volume/mute → Task 1 (`AudioManager`), Task 3 (`SettingsUI`/UXML/USS). ✓
- HUD entry point → Task 2 (glyph), Task 5 (button + handler). ✓
- Slider-drag auto-unmutes → `SetMusicVolume`/`SetSfxVolume` clear `musicMuted`/`sfxMuted` when volume > 0 (Task 1), covered by `SetMusicVolume_PositiveValue_ClearsMusicMuted` / `SetSfxVolume_PositiveValue_ClearsSfxMuted` tests, and manually re-verified in Task 7 Step 4. ✓
- `PlayerPrefs` persistence, decoupled from `save.json` → Task 1 (`PlayerPrefs` keys, no `PlayerSaveData` changes anywhere in this plan), manually verified in Task 7 Steps 6–7. ✓
- Scene wiring without manual Editor drag-and-drop → Task 6 (direct YAML edit with pre-verified non-colliding fileIDs). ✓
- Tap sound on buttons but not slider drag → `SettingsUI.OnMusicVolumeChanged`/`OnSfxVolumeChanged` never call `PlayTap()`; mute buttons and close button do. ✓

**Placeholder scan:** no TODO/TBD/"add appropriate" phrasing; every step has literal file contents or exact before/after code blocks.

**Type consistency:** `AudioManager.MusicVolume`/`SfxVolume`/`MusicMuted`/`SfxMuted`/`SetMusicVolume`/`SetSfxVolume`/`SetMusicMuted`/`SetSfxMuted` (Task 1) match every call site in `SettingsUI` (Task 3) and the test file. `SettingsUI.SetOpen(bool)` (Task 3) matches `ScreenNavigator.OpenSettings()`'s call (Task 4) and `HudUI.OnSettingsButtonClicked()`'s fallback call (Task 5). `PixelArtUtility.DrawSpeakerGlyph` (Task 2) matches its use in `HudUI` (Task 5). GUIDs/fileIDs introduced in Task 3/4/5 (`b4aee221433643779c44435b27d29b88`, `f869f5afa1224a45a2cb05c94f95b1e6`, `02291b3701d14f309dcc49d0676132aa`, `219540831`, `126855092`, `786579303`, `896233790`) are each used consistently everywhere they're referenced in Task 6.
