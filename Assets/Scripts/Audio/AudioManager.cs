using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    private const string MusicVolumeKey = "audio.musicVolume";
    private const string SfxVolumeKey = "audio.sfxVolume";
    private const string MusicMutedKey = "audio.musicMuted";
    private const string SfxMutedKey = "audio.sfxMuted";

    // Each track in the playlist gets this much total time in the spotlight before the next
    // crossfade begins; the last MusicCrossfadeSeconds of that window overlaps with the next
    // track's fade-in rather than cutting abruptly.
    private const float MusicSegmentSeconds = 120f;
    private const float MusicCrossfadeSeconds = 2f;

    // The hand-off from the bird filler into the third ("wholesome") track gets its own slower,
    // gentler treatment: a longer blend, then birds linger at a quiet sustained level for a while
    // longer (rather than disappearing right as the next track starts) before finally fading out
    // - so the wholesome track's opening is still accompanied by birdsong instead of a clean cut.
    // Kept low (not the more audible 35% an earlier version used): birds and the wholesome track
    // play SIMULTANEOUSLY for the whole blend+tail, and their volumes are deliberately kept
    // complementary (see CrossfadeBirdsIntoMain) so the combined loudness never exceeds a single
    // track's normal volume - but a higher sustain fraction still reads as "loud" simply because
    // it's competing with the other track's full volume for so long.
    private const float BirdsToMainBlendSeconds = 6f;
    private const float BirdsToMainTailSeconds = 6f;
    private const float BirdsToMainTailVolumeFraction = 0.15f;

    // Per-clip trim on top of the file-level loudness matching, so the relative balance between
    // tracks can be tuned by editing a constant instead of re-processing the source WAV files each
    // time. Birds' chirps read as louder than their measured level suggests (sharp transients grab
    // the ear); the wholesome track is the one meant to carry the loop, so it gets a boost.
    private const float BirdsVolumeMultiplier = 0.5f;
    private const float MainLoopVolumeMultiplier = 1.4f;

    [SerializeField] private float sfxVolume = 0.6f;
    [SerializeField] private float musicVolume = 0.12f;
    [SerializeField] private AudioClip musicClipOverride;
    // fal.ai-generated tracks (see docs/art-design-document.md's sibling audio pipeline), cycled
    // forever as guitar -> birds -> ambience -> birds -> guitar -> birds -> ... (built below in
    // BuildMusicPlaylist as a repeating [guitar, birds, ambience, birds] sequence) - a symmetric
    // back-and-forth between all three rather than a hard cut straight back to guitar every lap.
    [SerializeField] private AudioClip guitarIntroClip;
    [SerializeField] private AudioClip birdsFillerClip;

    private AudioSource sfxSource;
    // Two sources so a crossfade can have both the outgoing and incoming track playing
    // simultaneously for MusicCrossfadeSeconds - a single AudioSource can't overlap itself.
    private AudioSource musicSourceA;
    private AudioSource musicSourceB;
    private List<AudioClip> musicPlaylist;

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
        musicSourceA = gameObject.AddComponent<AudioSource>();
        musicSourceB = gameObject.AddComponent<AudioSource>();
        // Each track is shorter than a 2-minute segment, so it loops in place to fill its slot
        // rather than falling silent partway through.
        musicSourceA.loop = true;
        musicSourceB.loop = true;

        BuildClips();
        BuildMusicPlaylist();

        StartCoroutine(RunMusicPlaylist());
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // musicClipOverride is a real generated track (see docs/audio.md) wired in the Inspector;
    // falls back to the procedural pad loop if nothing has been assigned yet.
    private AudioClip MainLoopClip => musicClipOverride != null ? musicClipOverride : ambientLoopClip;

    private float ClipVolumeMultiplier(AudioClip clip)
    {
        if (clip == birdsFillerClip)
        {
            return BirdsVolumeMultiplier;
        }

        if (clip == MainLoopClip)
        {
            return MainLoopVolumeMultiplier;
        }

        return 1f;
    }

    private void BuildMusicPlaylist()
    {
        musicPlaylist = new List<AudioClip>();

        if (guitarIntroClip != null)
        {
            musicPlaylist.Add(guitarIntroClip);
        }

        if (birdsFillerClip != null)
        {
            musicPlaylist.Add(birdsFillerClip);
        }

        if (MainLoopClip != null)
        {
            musicPlaylist.Add(MainLoopClip);
        }

        if (birdsFillerClip != null)
        {
            musicPlaylist.Add(birdsFillerClip);
        }

        // Nothing configured beyond the fallback ambience - just the one track, no playlist to
        // cycle through.
        if (musicPlaylist.Count == 0 && MainLoopClip != null)
        {
            musicPlaylist.Add(MainLoopClip);
        }
    }

    private IEnumerator RunMusicPlaylist()
    {
        if (musicPlaylist.Count == 0)
        {
            yield break;
        }

        AudioSource current = musicSourceA;
        AudioSource next = musicSourceB;
        int index = 0;

        current.clip = musicPlaylist[index];
        current.volume = EffectiveMusicVolume() * ClipVolumeMultiplier(current.clip);
        current.Play();

        if (musicPlaylist.Count == 1)
        {
            yield break;
        }

        while (true)
        {
            AudioClip outgoingClip = current.clip;
            int nextIndex = (index + 1) % musicPlaylist.Count;
            AudioClip incomingClip = musicPlaylist[nextIndex];
            bool isBirdsIntoMain = outgoingClip == birdsFillerClip && incomingClip == MainLoopClip;
            float blendSeconds = isBirdsIntoMain ? BirdsToMainBlendSeconds : MusicCrossfadeSeconds;

            yield return new WaitForSeconds(Mathf.Max(0f, MusicSegmentSeconds - blendSeconds));

            index = nextIndex;
            next.clip = incomingClip;
            next.volume = 0f;
            next.Play();

            if (isBirdsIntoMain)
            {
                yield return StartCoroutine(CrossfadeBirdsIntoMain(current, next));
            }
            else
            {
                yield return StartCoroutine(CrossfadeLinear(current, next, blendSeconds));
            }

            current.Stop();
            current.volume = EffectiveMusicVolume() * ClipVolumeMultiplier(current.clip);

            (current, next) = (next, current);
        }
    }

    private IEnumerator CrossfadeLinear(AudioSource outgoing, AudioSource incoming, float duration)
    {
        float outgoingMultiplier = ClipVolumeMultiplier(outgoing.clip);
        float incomingMultiplier = ClipVolumeMultiplier(incoming.clip);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Reads EffectiveMusicVolume() every frame rather than caching it, so a volume change
            // or mute toggle mid-crossfade is picked up immediately instead of the fade finishing
            // at a stale level.
            float effectiveVolume = EffectiveMusicVolume();
            outgoing.volume = (1f - t) * effectiveVolume * outgoingMultiplier;
            incoming.volume = t * effectiveVolume * incomingMultiplier;
            yield return null;
        }

        outgoing.volume = 0f;
        incoming.volume = EffectiveMusicVolume() * incomingMultiplier;
    }

    // Two phases: first a slow smoothstep blend where birds only dips to a quiet sustain level
    // (not silence) while the wholesome track rises to fill in the rest - both audible together
    // right as it begins - then birds lingers at that quiet level for a further tail before
    // finally fading out, with the wholesome track filling back in to full as it does.
    //
    // birds+main are kept complementary at every instant (both endpoints of each phase sum to
    // 1.0, driven by the same interpolation parameter) so their COMBINED volume is always exactly
    // 1.0x - never louder than either track playing alone. An earlier version let main ramp to a
    // full 1.0 while birds was still at a real volume, so the two would briefly play loudly
    // together and the overlap itself read as "too loud", independent of either track's own level.
    private IEnumerator CrossfadeBirdsIntoMain(AudioSource birds, AudioSource main)
    {
        // birds+main summing to exactly 1.0 (see the class comment above) still holds before the
        // per-clip multipliers are applied - each side gets its own trim afterward, same as every
        // other volume assignment in this file.
        float elapsed = 0f;
        while (elapsed < BirdsToMainBlendSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / BirdsToMainBlendSeconds);
            float smoothT = t * t * (3f - 2f * t);
            float effectiveVolume = EffectiveMusicVolume();
            birds.volume = Mathf.Lerp(1f, BirdsToMainTailVolumeFraction, smoothT) * effectiveVolume * BirdsVolumeMultiplier;
            main.volume = Mathf.Lerp(0f, 1f - BirdsToMainTailVolumeFraction, smoothT) * effectiveVolume * MainLoopVolumeMultiplier;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < BirdsToMainTailSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / BirdsToMainTailSeconds);
            float effectiveVolume = EffectiveMusicVolume();
            birds.volume = Mathf.Lerp(BirdsToMainTailVolumeFraction, 0f, t) * effectiveVolume * BirdsVolumeMultiplier;
            main.volume = Mathf.Lerp(1f - BirdsToMainTailVolumeFraction, 1f, t) * effectiveVolume * MainLoopVolumeMultiplier;
            yield return null;
        }

        birds.volume = 0f;
        main.volume = EffectiveMusicVolume() * MainLoopVolumeMultiplier;
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

        ApplyMusicVolumeToSources();

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

        ApplyMusicVolumeToSources();

        PlayerPrefs.SetInt(MusicMutedKey, musicMuted ? 1 : 0);
        PlayerPrefs.Save();
    }

    // A source that isn't currently playing just ignores the new volume, and a source mid-crossfade
    // gets overwritten with the correct blended ratio on the coroutine's very next frame - so it's
    // safe to just push the flat effective volume onto both without tracking which one is "live".
    private void ApplyMusicVolumeToSources()
    {
        if (musicSourceA != null)
        {
            musicSourceA.volume = EffectiveMusicVolume() * ClipVolumeMultiplier(musicSourceA.clip);
        }

        if (musicSourceB != null)
        {
            musicSourceB.volume = EffectiveMusicVolume() * ClipVolumeMultiplier(musicSourceB.clip);
        }
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
