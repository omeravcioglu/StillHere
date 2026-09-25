using UnityEngine;
using StillHere.Input;
using StillHere.Core;

namespace StillHere.Debugging
{
    /// <summary>
    /// Debug helper to visualize input state.
    /// Add to any GameObject to see input status in Game view.
    /// Remove or disable in final build.
    /// </summary>
    public class InputDebugger : MonoBehaviour
    {
        [Header("Display Settings")]
        [SerializeField] private bool showOnScreen = true;
        [SerializeField] private bool logToConsole = false;

        private GUIStyle boxStyle;
        private GUIStyle labelStyle;

        private void OnGUI()
        {
            if (!showOnScreen) return;

            // Initialize styles
            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(GUI.skin.box);
                boxStyle.normal.background = MakeTexture(2, 2, new Color(0f, 0f, 0f, 0.7f));
            }

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.fontSize = 14;
                labelStyle.normal.textColor = Color.white;
            }

            GUILayout.BeginArea(new Rect(10, 10, 350, 300), boxStyle);
            GUILayout.Label("=== STILL, HERE - Input Debug ===", labelStyle);
            GUILayout.Space(5);

            // Input System Status
            if (StillHereInput.Instance == null)
            {
                GUILayout.Label("<color=red>✗ StillHereInput: NOT FOUND</color>", labelStyle);
                GUILayout.Label("  → Add StillHereInput component", labelStyle);
                GUILayout.Label("  → Assign StillHereInputActions asset", labelStyle);
            }
            else
            {
                GUILayout.Label("<color=lime>✓ StillHereInput: Active</color>", labelStyle);

                // Blink
                bool isBlinking = StillHereInput.Instance.IsBlinking;
                float blinkTime = StillHereInput.Instance.BlinkHoldTime;
                string blinkColor = isBlinking ? "yellow" : "white";
                GUILayout.Label($"<color={blinkColor}>Blink: {(isBlinking ? "HOLDING" : "Released")} ({blinkTime:F2}s)</color>", labelStyle);

                // Focus
                Vector2 focus = StillHereInput.Instance.FocusNormalized;
                GUILayout.Label($"Focus: ({focus.x:F2}, {focus.y:F2})", labelStyle);

                // Time
                float timeInf = StillHereInput.Instance.TimeInfluence;
                string timeStr = timeInf < -0.1f ? "SLOWING" : (timeInf > 0.1f ? "SPEEDING" : "Normal");
                GUILayout.Label($"Time Influence: {timeInf:F2} ({timeStr})", labelStyle);
            }

            GUILayout.Space(10);

            // Blink Controller Status
            if (BlinkController.Instance == null)
            {
                GUILayout.Label("<color=red>✗ BlinkController: NOT FOUND</color>", labelStyle);
            }
            else
            {
                GUILayout.Label("<color=lime>✓ BlinkController: Active</color>", labelStyle);
                float darkness = BlinkController.Instance.Darkness;
                GUILayout.Label($"Screen Darkness: {darkness:F2}", labelStyle);
            }

            // Time Controller Status
            if (TimeFlowController.Instance == null)
            {
                GUILayout.Label("<color=red>✗ TimeFlowController: NOT FOUND</color>", labelStyle);
            }
            else
            {
                GUILayout.Label("<color=lime>✓ TimeFlowController: Active</color>", labelStyle);
                float scale = TimeFlowController.Instance.CurrentTimeScale;
                GUILayout.Label($"Time Scale: {scale:F2}x", labelStyle);
            }

            GUILayout.Space(10);
            GUILayout.Label("<color=cyan>Controls:</color>", labelStyle);
            GUILayout.Label("  Space/LMB = Blink", labelStyle);
            GUILayout.Label("  Q/E = Slow/Fast time", labelStyle);
            GUILayout.Label("  Mouse = Focus attention", labelStyle);

            GUILayout.EndArea();
        }

        private void Update()
        {
            if (!logToConsole) return;

            if (StillHereInput.Instance != null && StillHereInput.Instance.IsBlinking)
            {
                UnityEngine.Debug.Log($"[InputDebug] Blinking: {StillHereInput.Instance.BlinkHoldTime:F2}s");
            }
        }

        private Texture2D MakeTexture(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            Texture2D tex = new Texture2D(width, height);
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}

