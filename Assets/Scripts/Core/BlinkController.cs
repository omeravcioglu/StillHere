using UnityEngine;
using UnityEngine.UI;
using StillHere.Input;

namespace StillHere.Core
{
    /// <summary>
    /// Controls the blink mechanic with realistic eyelid visuals.
    /// Top and bottom lids close in an oval eye shape.
    /// </summary>
    public class BlinkController : MonoBehaviour
    {
        public static BlinkController Instance { get; private set; }

        [Header("Eyelid Visual")]
        [Tooltip("Material using the EyelidBlink shader.")]
        [SerializeField] private Material eyelidMaterial;

        [Tooltip("The RawImage displaying the eyelid effect.")]
        [SerializeField] private RawImage eyelidOverlay;

        [Header("Eye Shape")]
        [Tooltip("Width of the eye opening (0.5-0.8 typical).")]
        [Range(0.3f, 1f)]
        [SerializeField] private float eyeWidth = 0.65f;

        [Tooltip("Height of the eye opening when fully open.")]
        [Range(0.2f, 0.6f)]
        [SerializeField] private float eyeHeight = 0.32f;

        [Tooltip("Softness of the eyelid edges.")]
        [Range(0.01f, 0.2f)]
        [SerializeField] private float edgeSoftness = 0.06f;

        [Header("Natural Blink Timing")]
        [Tooltip("Duration for eyes to close (real blinks are ~75-100ms).")]
        [SerializeField] private float closeDuration = 0.08f;

        [Tooltip("Duration for eyes to open after a quick blink.")]
        [SerializeField] private float openDurationBrief = 0.12f;

        [Tooltip("Duration for eyes to open after holding closed.")]
        [SerializeField] private float openDurationExtended = 0.4f;

        [Tooltip("Minimum time eyes stay fully closed.")]
        [SerializeField] private float minClosedTime = 0.04f;

        [Header("Colors")]
        [SerializeField] private Color eyelidColor = new Color(0.08f, 0.02f, 0.01f, 1f);
        [SerializeField] private Color skinTint = new Color(0.15f, 0.08f, 0.06f, 1f);

        [Header("Automatic Blinks")]
        [SerializeField] private bool enableAutoBlinks = true;
        [SerializeField] private float autoBlinkInterval = 5f;
        [SerializeField] private float autoBlinkVariance = 2f;

        [Header("Audio")]
        [SerializeField] private AudioSource blinkAudioSource;
        [SerializeField] private AudioClip eyeCloseSound;
        [SerializeField] private AudioClip eyeOpenSound;
        [Range(0f, 0.2f)]
        [SerializeField] private float blinkSoundVolume = 0.05f;

        // Shader property IDs
        private static readonly int BlinkAmountID = Shader.PropertyToID("_BlinkAmount");
        private static readonly int EyeWidthID = Shader.PropertyToID("_EyeWidth");
        private static readonly int EyeHeightID = Shader.PropertyToID("_EyeHeight");
        private static readonly int EdgeSoftnessID = Shader.PropertyToID("_EdgeSoftness");
        private static readonly int EyelidColorID = Shader.PropertyToID("_EyelidColor");
        private static readonly int SkinColorID = Shader.PropertyToID("_SkinColor");

        // State
        private float blinkAmount = 0f; // 0 = open, 1 = closed
        private BlinkPhase phase = BlinkPhase.Open;
        private float phaseTimer = 0f;
        private float holdTime = 0f;
        private float nextAutoBlinkTime;
        private bool playerHolding = false;
        private Material materialInstance;

        private enum BlinkPhase
        {
            Open,
            Closing,
            Closed,
            Opening
        }

        public float Darkness => blinkAmount;
        public bool IsBlinking => phase != BlinkPhase.Open;
        public bool EyesClosed => blinkAmount > 0.9f;

        public event System.Action OnEyesClosed;
        public event System.Action OnEyesOpened;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            SetupEyelidVisual();
            ScheduleNextAutoBlink();
        }

        private void Start()
        {
            SubscribeToInput();
        }

        private void SetupEyelidVisual()
        {
            // Find or create the shader
            Shader eyelidShader = Shader.Find("StillHere/EyelidBlink");
            
            if (eyelidShader == null)
            {
                Debug.LogError("[BlinkController] EyelidBlink shader not found! Falling back to simple overlay.");
                CreateFallbackOverlay();
                return;
            }

            // Create material instance
            if (eyelidMaterial != null)
            {
                materialInstance = new Material(eyelidMaterial);
            }
            else
            {
                materialInstance = new Material(eyelidShader);
            }

            // Find or create canvas
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasGO = new GameObject("BlinkCanvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 9999;
                canvasGO.AddComponent<CanvasScaler>();
            }

            // Create or use existing overlay
            if (eyelidOverlay == null)
            {
                GameObject overlayGO = new GameObject("EyelidOverlay");
                overlayGO.transform.SetParent(canvas.transform, false);

                eyelidOverlay = overlayGO.AddComponent<RawImage>();
                eyelidOverlay.raycastTarget = false;

                RectTransform rt = eyelidOverlay.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            eyelidOverlay.material = materialInstance;
            eyelidOverlay.color = Color.white;

            // Initialize shader properties
            UpdateShaderProperties();
            SetBlinkAmount(0f);
        }

        private void CreateFallbackOverlay()
        {
            // Fallback to simple black overlay if shader not found
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasGO = new GameObject("BlinkCanvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 9999;
            }

            GameObject overlayGO = new GameObject("BlinkOverlayFallback");
            overlayGO.transform.SetParent(canvas.transform, false);

            var image = overlayGO.AddComponent<Image>();
            image.color = new Color(0, 0, 0, 0);
            image.raycastTarget = false;

            RectTransform rt = image.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void UpdateShaderProperties()
        {
            if (materialInstance == null) return;

            materialInstance.SetFloat(EyeWidthID, eyeWidth);
            materialInstance.SetFloat(EyeHeightID, eyeHeight);
            materialInstance.SetFloat(EdgeSoftnessID, edgeSoftness);
            materialInstance.SetColor(EyelidColorID, eyelidColor);
            materialInstance.SetColor(SkinColorID, skinTint);
        }

        private void SetBlinkAmount(float amount)
        {
            blinkAmount = amount;
            if (materialInstance != null)
            {
                materialInstance.SetFloat(BlinkAmountID, amount);
            }
            
            // Disable overlay entirely when eyes are fully open (optimization + prevents any artifacts)
            if (eyelidOverlay != null)
            {
                eyelidOverlay.enabled = amount > 0.001f;
            }
        }

        private void SubscribeToInput()
        {
            if (StillHereInput.Instance != null)
            {
                StillHereInput.Instance.OnBlinkStart += HandleBlinkStart;
                StillHereInput.Instance.OnBlinkEnd += HandleBlinkEnd;
                Debug.Log("[BlinkController] Subscribed to input.");
            }
            else
            {
                StartCoroutine(RetrySubscription());
            }
        }

        private System.Collections.IEnumerator RetrySubscription()
        {
            yield return null;
            yield return null;
            if (StillHereInput.Instance != null)
            {
                StillHereInput.Instance.OnBlinkStart += HandleBlinkStart;
                StillHereInput.Instance.OnBlinkEnd += HandleBlinkEnd;
            }
            else
            {
                Debug.LogError("[BlinkController] StillHereInput not found!");
            }
        }

        private void OnDestroy()
        {
            if (StillHereInput.Instance != null)
            {
                StillHereInput.Instance.OnBlinkStart -= HandleBlinkStart;
                StillHereInput.Instance.OnBlinkEnd -= HandleBlinkEnd;
            }
            
            if (materialInstance != null)
            {
                Destroy(materialInstance);
            }
            
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            UpdateBlinkPhase();
            UpdateAutoBlink();
        }

        private void UpdateBlinkPhase()
        {
            switch (phase)
            {
                case BlinkPhase.Closing:
                    phaseTimer += Time.unscaledDeltaTime;
                    float closeT = Mathf.Clamp01(phaseTimer / closeDuration);
                    // Fast snap shut - ease in
                    SetBlinkAmount(EaseInQuad(closeT));
                    
                    if (closeT >= 1f)
                    {
                        SetBlinkAmount(1f);
                        phase = BlinkPhase.Closed;
                        phaseTimer = 0f;
                        holdTime = 0f;
                        PlaySound(eyeCloseSound);
                        OnEyesClosed?.Invoke();
                    }
                    break;

                case BlinkPhase.Closed:
                    holdTime += Time.unscaledDeltaTime;
                    SetBlinkAmount(1f);
                    
                    if (!playerHolding && holdTime >= minClosedTime)
                    {
                        phase = BlinkPhase.Opening;
                        phaseTimer = 0f;
                    }
                    break;

                case BlinkPhase.Opening:
                    phaseTimer += Time.unscaledDeltaTime;
                    float openDuration = holdTime > 0.5f ? openDurationExtended : openDurationBrief;
                    float openT = Mathf.Clamp01(phaseTimer / openDuration);
                    
                    // Smooth open - ease out
                    SetBlinkAmount(1f - EaseOutQuad(openT));
                    
                    if (openT >= 1f)
                    {
                        SetBlinkAmount(0f);
                        phase = BlinkPhase.Open;
                        phaseTimer = 0f;
                        PlaySound(eyeOpenSound);
                        OnEyesOpened?.Invoke();
                        ScheduleNextAutoBlink();
                    }
                    break;

                case BlinkPhase.Open:
                    // Ensure fully open
                    if (blinkAmount > 0.001f)
                    {
                        SetBlinkAmount(0f);
                    }
                    break;
            }
        }

        private void UpdateAutoBlink()
        {
            if (!enableAutoBlinks) return;
            if (phase != BlinkPhase.Open) return;
            if (playerHolding) return;

            if (Time.unscaledTime >= nextAutoBlinkTime)
            {
                StartBlink();
                StartCoroutine(AutoBlinkRelease());
            }
        }

        private System.Collections.IEnumerator AutoBlinkRelease()
        {
            yield return new WaitForSecondsRealtime(closeDuration + minClosedTime + 0.02f);
            if (phase == BlinkPhase.Closed && !playerHolding)
            {
                phase = BlinkPhase.Opening;
                phaseTimer = 0f;
            }
        }

        private void ScheduleNextAutoBlink()
        {
            float variance = Random.Range(-autoBlinkVariance, autoBlinkVariance);
            nextAutoBlinkTime = Time.unscaledTime + autoBlinkInterval + variance;
        }

        private void HandleBlinkStart()
        {
            playerHolding = true;
            if (phase == BlinkPhase.Open)
            {
                StartBlink();
            }
        }

        private void HandleBlinkEnd(float duration)
        {
            playerHolding = false;
            if (phase == BlinkPhase.Closed)
            {
                phase = BlinkPhase.Opening;
                phaseTimer = 0f;
            }
        }

        private void StartBlink()
        {
            phase = BlinkPhase.Closing;
            phaseTimer = 0f;
        }

        public void TriggerBlink(float holdDuration = 0f, System.Action onClosed = null)
        {
            StartCoroutine(ProgrammaticBlink(holdDuration, onClosed));
        }

        private System.Collections.IEnumerator ProgrammaticBlink(float holdDuration, System.Action onClosed)
        {
            StartBlink();
            
            while (phase == BlinkPhase.Closing)
                yield return null;

            onClosed?.Invoke();

            if (holdDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(holdDuration);
            }
            else
            {
                yield return new WaitForSecondsRealtime(minClosedTime);
            }

            if (phase == BlinkPhase.Closed)
            {
                phase = BlinkPhase.Opening;
                phaseTimer = 0f;
            }
        }

        public void SetDarknessImmediate(float darkness)
        {
            SetBlinkAmount(Mathf.Clamp01(darkness));
            phase = darkness > 0.5f ? BlinkPhase.Closed : BlinkPhase.Open;
        }

        private float EaseInQuad(float t) => t * t;
        private float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

        private void PlaySound(AudioClip clip)
        {
            if (blinkAudioSource != null && clip != null)
            {
                blinkAudioSource.PlayOneShot(clip, blinkSoundVolume);
            }
        }

        private void OnValidate()
        {
            // Update shader in editor when values change
            if (materialInstance != null)
            {
                UpdateShaderProperties();
            }
        }
    }
}


