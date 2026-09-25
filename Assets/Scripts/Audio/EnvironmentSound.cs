using UnityEngine;

/// <summary>
/// Plays environment/ambient sounds with various playback options.
/// Can be used for background ambience, area-specific sounds, or triggered audio.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class EnvironmentSound : MonoBehaviour
{
    [Header("Audio Clips")]
    [Tooltip("Sound clips to play. If multiple, one will be chosen randomly.")]
    public AudioClip[] audioClips;

    [Header("Playback Settings")]
    [Tooltip("Play automatically when the scene starts")]
    public bool playOnStart = true;

    [Tooltip("Loop the sound continuously")]
    public bool loop = true;

    [Tooltip("Volume of the sound")]
    [Range(0f, 1f)]
    public float volume = 1f;

    [Tooltip("Randomize volume slightly for variation")]
    [Range(0f, 0.5f)]
    public float volumeVariation = 0f;

    [Tooltip("Pitch of the sound")]
    [Range(0.1f, 3f)]
    public float pitch = 1f;

    [Tooltip("Randomize pitch slightly for variation")]
    [Range(0f, 0.5f)]
    public float pitchVariation = 0f;

    [Header("Spatial Settings")]
    [Tooltip("0 = 2D (heard everywhere), 1 = 3D (positional audio)")]
    [Range(0f, 1f)]
    public float spatialBlend = 0f;

    [Tooltip("Minimum distance before sound starts to fade")]
    public float minDistance = 1f;

    [Tooltip("Maximum distance where sound can be heard")]
    public float maxDistance = 50f;

    [Header("Fade Settings")]
    [Tooltip("Fade in duration when sound starts")]
    [Range(0f, 10f)]
    public float fadeInDuration = 0f;

    [Tooltip("Fade out duration when sound stops")]
    [Range(0f, 10f)]
    public float fadeOutDuration = 0f;

    [Header("Interval Playback (Non-Looping)")]
    [Tooltip("For non-looping sounds: play at random intervals")]
    public bool playAtIntervals = false;

    [Tooltip("Minimum time between plays")]
    public float minInterval = 5f;

    [Tooltip("Maximum time between plays")]
    public float maxInterval = 15f;

    [Header("Trigger Settings")]
    [Tooltip("Only play when player enters trigger area")]
    public bool useTriggerZone = false;

    [Tooltip("Tag to check for trigger activation")]
    public string triggerTag = "Player";

    // Private variables
    private AudioSource audioSource;
    private float targetVolume;
    private bool isFading = false;
    private bool isInsideTrigger = false;
    private float intervalTimer = 0f;
    private float nextPlayTime = 0f;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        SetupAudioSource();
    }

    private void Start()
    {
        if (playOnStart && !useTriggerZone)
        {
            Play();
        }

        if (playAtIntervals && !loop)
        {
            nextPlayTime = Random.Range(minInterval, maxInterval);
        }
    }

    private void Update()
    {
        // Handle interval playback
        if (playAtIntervals && !loop && !useTriggerZone)
        {
            intervalTimer += Time.deltaTime;
            if (intervalTimer >= nextPlayTime)
            {
                PlayOneShot();
                intervalTimer = 0f;
                nextPlayTime = Random.Range(minInterval, maxInterval);
            }
        }

        // Handle trigger zone interval playback
        if (playAtIntervals && !loop && useTriggerZone && isInsideTrigger)
        {
            intervalTimer += Time.deltaTime;
            if (intervalTimer >= nextPlayTime)
            {
                PlayOneShot();
                intervalTimer = 0f;
                nextPlayTime = Random.Range(minInterval, maxInterval);
            }
        }
    }

    private void SetupAudioSource()
    {
        audioSource.playOnAwake = false;
        audioSource.loop = loop;
        audioSource.spatialBlend = spatialBlend;
        audioSource.minDistance = minDistance;
        audioSource.maxDistance = maxDistance;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
    }

    /// <summary>
    /// Play the environment sound
    /// </summary>
    public void Play()
    {
        if (audioClips.Length == 0)
        {
            Debug.LogWarning($"[EnvironmentSound] No audio clips assigned to {gameObject.name}");
            return;
        }

        // Select random clip if multiple
        audioSource.clip = audioClips[Random.Range(0, audioClips.Length)];

        // Apply volume and pitch with variation
        targetVolume = volume + Random.Range(-volumeVariation, volumeVariation);
        targetVolume = Mathf.Clamp01(targetVolume);

        audioSource.pitch = pitch + Random.Range(-pitchVariation, pitchVariation);
        audioSource.pitch = Mathf.Clamp(audioSource.pitch, 0.1f, 3f);

        if (fadeInDuration > 0)
        {
            audioSource.volume = 0f;
            audioSource.Play();
            StartCoroutine(FadeIn());
        }
        else
        {
            audioSource.volume = targetVolume;
            audioSource.Play();
        }
    }

    /// <summary>
    /// Play a one-shot sound (doesn't interrupt current playback)
    /// </summary>
    public void PlayOneShot()
    {
        if (audioClips.Length == 0) return;

        AudioClip clip = audioClips[Random.Range(0, audioClips.Length)];
        float vol = volume + Random.Range(-volumeVariation, volumeVariation);
        vol = Mathf.Clamp01(vol);

        audioSource.PlayOneShot(clip, vol);
    }

    /// <summary>
    /// Stop the environment sound
    /// </summary>
    public void Stop()
    {
        if (fadeOutDuration > 0 && audioSource.isPlaying)
        {
            StartCoroutine(FadeOut());
        }
        else
        {
            audioSource.Stop();
        }
    }

    /// <summary>
    /// Set the volume (0-1)
    /// </summary>
    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);
        targetVolume = volume;
        if (!isFading)
        {
            audioSource.volume = volume;
        }
    }

    /// <summary>
    /// Pause the sound
    /// </summary>
    public void Pause()
    {
        audioSource.Pause();
    }

    /// <summary>
    /// Resume the sound
    /// </summary>
    public void Resume()
    {
        audioSource.UnPause();
    }

    private System.Collections.IEnumerator FadeIn()
    {
        isFading = true;
        float elapsed = 0f;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(0f, targetVolume, elapsed / fadeInDuration);
            yield return null;
        }

        audioSource.volume = targetVolume;
        isFading = false;
    }

    private System.Collections.IEnumerator FadeOut()
    {
        isFading = true;
        float startVolume = audioSource.volume;
        float elapsed = 0f;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeOutDuration);
            yield return null;
        }

        audioSource.volume = 0f;
        audioSource.Stop();
        isFading = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!useTriggerZone) return;

        if (other.CompareTag(triggerTag))
        {
            isInsideTrigger = true;

            if (loop)
            {
                Play();
            }
            else if (playAtIntervals)
            {
                intervalTimer = 0f;
                nextPlayTime = Random.Range(0f, minInterval); // Play soon after entering
            }
            else
            {
                PlayOneShot();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!useTriggerZone) return;

        if (other.CompareTag(triggerTag))
        {
            isInsideTrigger = false;

            if (loop)
            {
                Stop();
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (spatialBlend > 0)
        {
            // Draw min distance sphere
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, minDistance);

            // Draw max distance sphere
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, maxDistance);
        }

        // Draw speaker icon
        Gizmos.color = Color.cyan;
        Gizmos.DrawIcon(transform.position, "AudioSource Gizmo", true);
    }

    // Public getters
    public bool IsPlaying => audioSource != null && audioSource.isPlaying;
    public bool IsFading => isFading;
}
