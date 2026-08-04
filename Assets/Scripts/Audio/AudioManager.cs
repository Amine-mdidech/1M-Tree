using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private float sfxVolume = 0.6f;
    [SerializeField] private float musicVolume = 0.12f;
    [SerializeField] private AudioClip musicClipOverride;

    private AudioSource sfxSource;
    private AudioSource musicSource;

    private AudioClip tapClip;
    private AudioClip plantClip;
    private AudioClip unlockClip;
    private AudioClip milestoneClip;
    private AudioClip boostClip;
    private AudioClip errorClip;
    private AudioClip researchStartClip;
    private AudioClip ambientLoopClip;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        sfxSource = gameObject.AddComponent<AudioSource>();
        musicSource = gameObject.AddComponent<AudioSource>();

        BuildClips();

        // musicClipOverride is a real generated track (see docs/audio.md) wired in the Inspector;
        // falls back to the procedural pad loop if nothing has been assigned yet.
        musicSource.clip = musicClipOverride != null ? musicClipOverride : ambientLoopClip;
        musicSource.loop = true;
        musicSource.volume = musicVolume;
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

        sfxSource.PlayOneShot(clip, sfxVolume);
    }
}
