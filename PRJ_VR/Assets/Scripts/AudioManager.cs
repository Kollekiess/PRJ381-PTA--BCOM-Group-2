using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central Audio Manager to configure, load, and play sound effects, 3D spatial audio,
/// and looping environmental ambience throughout the project.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [System.Serializable]
    public class Sound
    {
        [Tooltip("Unique identifier used to play this sound (e.g. 'BirdChirp', 'LessonText').")]
        public string name;

        [Tooltip("Audio clip assigned to this sound. Can be swapped with any custom audio file.")]
        public AudioClip clip;

        [Range(0f, 1f)]
        public float volume = 0.8f;

        [Range(0.1f, 3f)]
        public float pitch = 1f;

        [Range(0f, 0.4f)]
        [Tooltip("Random pitch variation applied on each playback for natural variety.")]
        public float pitchVariation = 0.05f;

        [Tooltip("If true, loops continuously until stopped (e.g. wind ambience).")]
        public bool loop = false;

        [Tooltip("If true, plays in 3D world space at the source position with distance attenuation.")]
        public bool is3D = false;

        public float minDistance = 1.5f;
        public float maxDistance = 35f;

        [HideInInspector]
        public AudioSource source;
    }

    [Header("Sound Library")]
    [Tooltip("List of sounds configured in the project. You can add, edit, or swap clips at any time.")]
    public List<Sound> sounds = new List<Sound>();

    [Header("Global Volumes")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Range(0f, 1f)] public float ambientVolume = 0.65f;

    [Header("Auto-Start Ambience")]
    public bool playWindOnStart = true;

    // Fast name-to-sound lookup
    private readonly Dictionary<string, Sound> soundDictionary = new Dictionary<string, Sound>(StringComparer.OrdinalIgnoreCase);

    // 2D SFX audio source pool for overlapping one-shots
    private readonly List<AudioSource> sfxSourcePool = new List<AudioSource>();
    private const int SFX_POOL_SIZE = 8;
    private int nextPoolIndex = 0;

    // Dedicated 3D positional one-shot source pool
    private readonly List<AudioSource> positionalSourcePool = new List<AudioSource>();
    private const int POSITIONAL_POOL_SIZE = 8;
    private int nextPositionalIndex = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Auto-populate default library if empty
        InitializeDefaultSoundsIfEmpty();

        // Build dictionary and attach sources
        BuildSoundDictionary();
        CreateAudioSourcePools();
    }

    private void Start()
    {
        if (playWindOnStart)
        {
            PlayWindAmbience();
        }
    }

    private void BuildSoundDictionary()
    {
        soundDictionary.Clear();
        foreach (var s in sounds)
        {
            if (string.IsNullOrEmpty(s.name)) continue;

            if (soundDictionary.ContainsKey(s.name))
            {
                Debug.LogWarning($"[AudioManager] Duplicate sound name detected: '{s.name}'. Skipping duplicate.");
                continue;
            }

            soundDictionary.Add(s.name, s);

            // If sound is looping or has its own dedicated source
            if (s.loop)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.clip = s.clip;
                src.volume = s.volume * masterVolume * ambientVolume;
                src.pitch = s.pitch;
                src.loop = true;
                src.spatialBlend = s.is3D ? 1f : 0f;
                src.playOnAwake = false;
                s.source = src;
            }
        }
    }

    private void CreateAudioSourcePools()
    {
        // 2D SFX pool
        for (int i = 0; i < SFX_POOL_SIZE; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            sfxSourcePool.Add(src);
        }

        // 3D Positional pool
        for (int i = 0; i < POSITIONAL_POOL_SIZE; i++)
        {
            var go = new GameObject($"3D_AudioSource_{i}");
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1f; // Full 3D
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 1.5f;
            src.maxDistance = 35f;
            positionalSourcePool.Add(src);
        }
    }

    // --- PLAYBACK METHODS ---

    /// <summary>
    /// Plays a sound by name as a 2D one-shot.
    /// </summary>
    public void Play(string soundName)
    {
        if (!soundDictionary.TryGetValue(soundName, out Sound s))
        {
            Debug.LogWarning($"[AudioManager] Sound '{soundName}' not found in library.");
            return;
        }

        if (s.clip == null) return;

        AudioSource src = GetAvailable2DSource();
        float pitchOffset = s.pitchVariation > 0 ? UnityEngine.Random.Range(-s.pitchVariation, s.pitchVariation) : 0f;
        src.pitch = Mathf.Clamp(s.pitch + pitchOffset, 0.2f, 3f);
        src.volume = s.volume * masterVolume * sfxVolume;
        src.PlayOneShot(s.clip);
    }

    /// <summary>
    /// Plays a sound by name in 3D world space at the given position.
    /// </summary>
    public void Play(string soundName, Vector3 worldPosition)
    {
        if (!soundDictionary.TryGetValue(soundName, out Sound s))
        {
            Debug.LogWarning($"[AudioManager] Sound '{soundName}' not found in library.");
            return;
        }

        if (s.clip == null) return;

        AudioSource src = GetAvailable3DSource();
        src.transform.position = worldPosition;
        src.minDistance = s.minDistance;
        src.maxDistance = s.maxDistance;
        float pitchOffset = s.pitchVariation > 0 ? UnityEngine.Random.Range(-s.pitchVariation, s.pitchVariation) : 0f;
        src.pitch = Mathf.Clamp(s.pitch + pitchOffset, 0.2f, 3f);
        src.volume = s.volume * masterVolume * sfxVolume;
        src.PlayOneShot(s.clip);
    }

    /// <summary>
    /// Plays a looping sound (e.g. ambient wind).
    /// </summary>
    public void PlayLoop(string soundName)
    {
        if (!soundDictionary.TryGetValue(soundName, out Sound s))
        {
            Debug.LogWarning($"[AudioManager] Sound '{soundName}' not found in library.");
            return;
        }

        if (s.source != null && !s.source.isPlaying)
        {
            s.source.volume = s.volume * masterVolume * ambientVolume;
            s.source.Play();
        }
    }

    /// <summary>
    /// Stops a looping or active sound.
    /// </summary>
    public void Stop(string soundName)
    {
        if (soundDictionary.TryGetValue(soundName, out Sound s))
        {
            if (s.source != null && s.source.isPlaying)
            {
                s.source.Stop();
            }
        }
    }

    public void StopAll()
    {
        foreach (var s in sounds)
        {
            if (s.source != null) s.source.Stop();
        }
        foreach (var src in sfxSourcePool) src.Stop();
        foreach (var src in positionalSourcePool) src.Stop();
    }

    private AudioSource GetAvailable2DSource()
    {
        var src = sfxSourcePool[nextPoolIndex];
        nextPoolIndex = (nextPoolIndex + 1) % sfxSourcePool.Count;
        return src;
    }

    private AudioSource GetAvailable3DSource()
    {
        var src = positionalSourcePool[nextPositionalIndex];
        nextPositionalIndex = (nextPositionalIndex + 1) % positionalSourcePool.Count;
        return src;
    }

    // --- CONVENIENCE SPECIALIZED SHORTCUTS ---

    public void PlayBirdChirp(Vector3 position)
    {
        Play("BirdChirp", position);
    }

    public void PlayBirdFlap(Vector3 position)
    {
        Play("BirdFlap", position);
    }

    public void PlayWindAmbience()
    {
        PlayLoop("WindAmbience");
    }

    public void PlayLessonTextTyping()
    {
        Play("LessonText");
    }

    public void PlayBreadToss(Vector3 position)
    {
        Play("BreadToss", position);
    }

    public void PlayBreadImpact(Vector3 position)
    {
        Play("BreadImpact", position);
    }

    public void PlayCompletedLesson()
    {
        Play("CompletedLesson");
    }

    public void PlayTeacherTestPassed()
    {
        Play("TeacherTestPassed");
    }

    // --- AUTO-POPULATION OF DEFAULT SOUNDS ---

    private void InitializeDefaultSoundsIfEmpty()
    {
        if (sounds != null && sounds.Count > 0) return;

        sounds = new List<Sound>
        {
            new Sound { name = "BirdChirp", clip = LoadClip("Bird_Chirp"), volume = 0.75f, pitch = 1.0f, pitchVariation = 0.08f, is3D = true, minDistance = 2f, maxDistance = 35f },
            new Sound { name = "BirdFlap", clip = LoadClip("Bird_Flap"), volume = 0.70f, pitch = 1.0f, pitchVariation = 0.06f, is3D = true, minDistance = 1.5f, maxDistance = 30f },
            new Sound { name = "WindAmbience", clip = LoadClip("Wind_Ambience"), volume = 0.50f, pitch = 1.0f, loop = true, is3D = false },
            new Sound { name = "LessonText", clip = LoadClip("Lesson_Text_Typing"), volume = 0.60f, pitch = 1.0f, pitchVariation = 0.05f, is3D = false },
            new Sound { name = "BreadToss", clip = LoadClip("Bread_Toss"), volume = 0.75f, pitch = 1.0f, pitchVariation = 0.04f, is3D = true, minDistance = 1f, maxDistance = 25f },
            new Sound { name = "BreadImpact", clip = LoadClip("Bread_Impact"), volume = 0.80f, pitch = 1.0f, pitchVariation = 0.05f, is3D = true, minDistance = 1f, maxDistance = 25f },
            new Sound { name = "CompletedLesson", clip = LoadClip("Completed_Lesson_Chime"), volume = 0.85f, pitch = 1.0f, is3D = false },
            new Sound { name = "TeacherTestPassed", clip = LoadClip("Teacher_Test_Fanfare"), volume = 0.90f, pitch = 1.0f, is3D = false }
        };
    }

    private AudioClip LoadClip(string baseName)
    {
#if UNITY_EDITOR
        string path = $"Assets/Audio/{baseName}.wav";
        var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip != null) return clip;
#endif
        return Resources.Load<AudioClip>($"Audio/{baseName}");
    }
}
