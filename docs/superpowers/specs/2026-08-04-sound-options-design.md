# Sound Options Panel — Design

## Purpose

Let the player control audio from an in-game options screen: independent volume
and mute for Music and Sound Effects, opened from a new HUD button, persisted
across sessions.

## Current state

`AudioManager` (`Assets/Scripts/Audio/AudioManager.cs`) is a singleton with two
`AudioSource`s:

- `sfxSource` — plays every one-shot effect (tap, plant, unlock, milestone,
  boost, error, research-start) at a single hardcoded `sfxVolume` (0.6).
- `musicSource` — loops the ambient/music track at a hardcoded `musicVolume`
  (0.12).

There is no player-facing settings UI. The HUD's gear-icon button opens
`DemoUI`, a dev-only debug/cheat panel (unlock species, add gems, reset save,
etc.) — unrelated to player settings and out of scope here.

UI screens follow one consistent pattern (see `ProfileUI.cs` / `Profile.uxml`
/ `Profile.uss`): a `UIDocument`-backed `MonoBehaviour` with `SetOpen(bool)`
driving `PanelAnimator.Open/Close`, registered with `ScreenNavigator` so
opening one screen closes the others and hides the home toolbar.

Game progress persists to `Application.persistentDataPath/save.json` via
`SaveManager`/`PlayerSaveData`. `DemoUI` has a debug "reset save" action that
deletes this file.

## Scope

**In:** Music volume + mute, SFX volume + mute, a Settings panel reachable
from the HUD, persistence that survives app restarts and survives the debug
"reset save" action.

**Out:** master volume tier, per-effect volume (e.g. splitting SFX further
into UI vs. game-event vs. ambience channels), any non-audio settings
(graphics, controls, etc.).

## Design

### Entry point

A new speaker-icon button is added to the HUD action cluster
(`Assets/UI/Hud/Hud.uxml`), alongside the existing Lab/Codex/Profile buttons,
styled the same way (colored background + pixel-art glyph). Tapping it calls
a new `ScreenNavigator.OpenSettings()`, which follows the exact same
"close other panels, open this one, hide home toolbar" flow as
`OpenProfile()`/`OpenCodex()`.

A new `DrawSpeakerGlyph` method is added to `PixelArtUtility.cs`, following
the style of the existing `DrawGearGlyph`/`DrawBookGlyph` methods (simple
filled-primitive pixel art: a speaker body + cone, using `FillRect`/
`FillTriangleUp`/`FillTriangleDown`).

### Settings panel

New files, mirroring the Profile panel's structure exactly:

- `Assets/UI/Settings/Settings.uxml`
- `Assets/UI/Settings/Settings.uss`
- `Assets/UI/Settings/SettingsPanelSettings.asset`
- `Assets/Scripts/UI/SettingsUI.cs`

Layout: `settings-root` > `settings-frame` (header with title + X close
button, matching `profile-header`/`profile-close-button`) > `settings-content`
with two rows:

- **Music** — a `ui:Slider` (range 0–1) + a mute toggle button
- **Sound Effects** — a `ui:Slider` (range 0–1) + a mute toggle button

`SettingsUI.cs` mirrors `ProfileUI.cs`'s shape: `Awake` caches the
`UIDocument`, `OnEnable` queries elements and wires callbacks, `SetOpen`
drives `PanelAnimator`, `OnCloseButtonClicked` calls
`screenNavigator.GoHome()`.

On open (`Refresh()`), sliders and mute-button visual state are initialized
from `AudioManager.Instance`'s current values (so re-opening the panel shows
whatever was last set, including values loaded from `PlayerPrefs` at startup).

Behavior:

- Dragging a slider sets that channel's volume via `AudioManager` and writes
  through to `PlayerPrefs` immediately (no separate "Save" button — matches
  how every other value in this game auto-persists).
- Dragging a slider while that channel is muted automatically un-mutes it
  (a slider showing a nonzero value with no sound is confusing).
- Clicking a mute button toggles that channel's muted state; the slider stays
  at its last value (so un-muting restores the prior volume) and the mute
  button's icon/style reflects the muted/unmuted state.
- `AudioManager.Instance?.PlayTap()` fires on the close button and mute
  toggles, matching the tap-feedback convention used by every other panel's
  buttons — but not on slider drag, to avoid spamming the tap sound while
  dragging.

### AudioManager changes

`AudioManager.cs`'s hardcoded `sfxVolume`/`musicVolume` fields become the
in-memory defaults, overridden at `Awake` by any persisted `PlayerPrefs`
values, before music starts playing. New public API:

```csharp
float MusicVolume { get; }
float SfxVolume { get; }
bool MusicMuted { get; }
bool SfxMuted { get; }

void SetMusicVolume(float volume);
void SetSfxVolume(float volume);
void SetMusicMuted(bool muted);
void SetSfxMuted(bool muted);
```

Effective volume = `storedVolume * (muted ? 0f : 1f)`, applied to
`musicSource.volume` immediately on change, and passed as the volume
argument to `sfxSource.PlayOneShot(clip, effectiveSfxVolume)` for every
`Play*()` call. Each setter persists its value to `PlayerPrefs` and calls
`PlayerPrefs.Save()`.

### Persistence

`PlayerPrefs` keys: `audio.musicVolume`, `audio.sfxVolume`,
`audio.musicMuted`, `audio.sfxMuted`. Deliberately **not** part of
`PlayerSaveData`/`save.json` — sound preferences are a device/player
preference, not game progress, so they must survive the Demo panel's
"reset save" debug action and aren't coupled to `SaveManager`'s load/save
timing.

### Scene wiring

`SampleScene.unity` is force-text serialized. A new GameObject
(`UIDocument` + `SettingsUI` + the new `SettingsPanelSettings` asset) is
added by editing the scene YAML directly, mirroring the existing
`ProfileUI` GameObject block, and `ScreenNavigator`/`HudUI`'s new
`SerializeField` references are wired the same way. No manual Unity Editor
steps are required.

## Testing

Unity has no headless UI-interaction test harness set up in this project
(`Grove.Tests.csproj` covers game-logic unit tests, not UI). Verification is
manual: open the game in the Editor, open Settings from the HUD, drag each
slider and confirm audible volume change, toggle each mute and confirm
silence/restore, close and reopen the panel and confirm state persisted,
stop and re-enter Play mode (or restart the Editor) and confirm values
persisted via `PlayerPrefs`.
