using UnityEngine;
using System.Collections;
using Blob3D.Player;

namespace Blob3D.Gameplay
{
    /// <summary>
    /// パワーアップの時限効果を管理する。
    /// コルーチンで各効果の持続時間を制御。
    /// Visual feedback for shield, magnet, and ghost effects.
    /// </summary>
    public class PowerUpEffectManager : MonoBehaviour
    {
        public static PowerUpEffectManager Instance { get; private set; }

        [Header("Magnet Settings")]
        [SerializeField] private float magnetRange = 15f;
        [SerializeField] private float magnetPullSpeed = 20f;

        [Header("Ghost Settings")]
        [SerializeField] private float ghostPulseSpeed = 4f;
        [SerializeField] private float ghostAlphaMin = 0.25f;
        [SerializeField] private float ghostAlphaMax = 0.5f;

        private Coroutine speedCoroutine;
        private Coroutine magnetCoroutine;
        private Coroutine ghostCoroutine;

        // Shield visual
        private GameObject shieldVisual;

        // Magnet visual
        private ParticleSystem magnetAuraPS;

        // Ghost visual state
        private Renderer ghostTargetRenderer;
        private Color ghostOriginalColor;
        private bool ghostPulsing;

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            // Ghost pulsing alpha effect
            if (ghostPulsing && ghostTargetRenderer != null)
            {
                float t = (Mathf.Sin(Time.time * ghostPulseSpeed) + 1f) * 0.5f;
                float alpha = Mathf.Lerp(ghostAlphaMin, ghostAlphaMax, t);
                Color c = ghostTargetRenderer.material.color;
                c.a = alpha;
                ghostTargetRenderer.material.color = c;
            }
        }

        // ---------- スピードブースト ----------

        public void ApplySpeedBoost(BlobController player, float duration)
        {
            if (speedCoroutine != null) StopCoroutine(speedCoroutine);
            speedCoroutine = StartCoroutine(SpeedBoostRoutine(player, duration));
        }

        private IEnumerator SpeedBoostRoutine(BlobController player, float duration)
        {
            player.SpeedMultiplier = 1.5f;
            yield return new WaitForSeconds(duration);
            player.SpeedMultiplier = 1f;
            speedCoroutine = null;
        }

        // ---------- シールド ----------

        /// <summary>
        /// Show a semi-transparent sphere around the target to indicate shield.
        /// </summary>
        public void ShowShieldEffect(Transform target)
        {
            HideShieldEffect();

            shieldVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shieldVisual.name = "ShieldVisual";
            shieldVisual.transform.SetParent(target);
            shieldVisual.transform.localPosition = Vector3.zero;
            shieldVisual.transform.localScale = Vector3.one * 1.3f;

            // Remove physics collider — visual only
            var col = shieldVisual.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Configure semi-transparent URP material
            var rend = shieldVisual.GetComponent<Renderer>();
            if (rend != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");

                var mat = new Material(shader);
                mat.color = new Color(0.5f, 0.8f, 1f, 0.25f);
                mat.SetFloat("_Surface", 1f); // Transparent
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");

                // Slight emission for visibility
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.3f, 0.6f, 1f) * 0.5f);

                rend.material = mat;
            }
        }

        /// <summary>
        /// Hide and destroy the shield visual.
        /// </summary>
        public void HideShieldEffect()
        {
            if (shieldVisual != null)
            {
                Destroy(shieldVisual);
                shieldVisual = null;
            }
        }

        // ---------- マグネット ----------

        public void ApplyMagnet(BlobController player, float duration)
        {
            if (magnetCoroutine != null) StopCoroutine(magnetCoroutine);
            magnetCoroutine = StartCoroutine(MagnetRoutine(player, duration));
        }

        private IEnumerator MagnetRoutine(BlobController player, float duration)
        {
            player.IsMagnetActive = true;

            // Start magnet aura VFX
            magnetAuraPS = VFXManager.Instance?.PlayMagnetAura(player.transform, new Color(1f, 0.2f, 0.8f));

            float elapsed = 0f;

            while (elapsed < duration && player.IsAlive)
            {
                // 範囲内のエサを引き寄せる
                Collider[] hits = Physics.OverlapSphere(player.transform.position, magnetRange);
                foreach (var hit in hits)
                {
                    Feed feed = hit.GetComponent<Feed>();
                    if (feed != null && feed.IsActive)
                    {
                        Vector3 dir = (player.transform.position - feed.transform.position).normalized;
                        feed.transform.position += dir * magnetPullSpeed * Time.deltaTime;
                    }
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            player.IsMagnetActive = false;

            // Stop magnet aura VFX
            if (magnetAuraPS != null)
            {
                VFXManager.Instance?.StopMagnetAura(magnetAuraPS);
                magnetAuraPS = null;
            }

            magnetCoroutine = null;
        }

        // ---------- 全効果キャンセル ----------

        /// <summary>
        /// Cancel all active power-up effects and reset player state.
        /// Call this when the player dies.
        /// </summary>
        public void CancelAllEffects(BlobController player)
        {
            if (speedCoroutine != null)
            {
                StopCoroutine(speedCoroutine);
                speedCoroutine = null;
            }
            if (magnetCoroutine != null)
            {
                StopCoroutine(magnetCoroutine);
                magnetCoroutine = null;
            }
            if (ghostCoroutine != null)
            {
                StopCoroutine(ghostCoroutine);
                ghostCoroutine = null;
            }

            // Stop visual effects
            HideShieldEffect();
            if (magnetAuraPS != null)
            {
                VFXManager.Instance?.StopMagnetAura(magnetAuraPS);
                magnetAuraPS = null;
            }
            ghostPulsing = false;
            ghostTargetRenderer = null;

            // Reset player state
            if (player != null)
            {
                player.SpeedMultiplier = 1f;
                player.IsMagnetActive = false;
                player.IsGhostActive = false;

                // Restore renderer alpha in case ghost was active
                Renderer rend = player.GetComponent<Renderer>();
                if (rend != null)
                {
                    var mat = rend.material;
                    mat.SetFloat("_Surface", 0f);
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    mat.SetInt("_ZWrite", 1);
                    mat.renderQueue = -1;
                    mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    Color c = mat.color;
                    c.a = 1f;
                    mat.color = c;
                }
            }
        }

        // ---------- ゴースト ----------

        public void ApplyGhost(BlobController player, float duration)
        {
            if (ghostCoroutine != null) StopCoroutine(ghostCoroutine);
            ghostCoroutine = StartCoroutine(GhostRoutine(player, duration));
        }

        private IEnumerator GhostRoutine(BlobController player, float duration)
        {
            player.IsGhostActive = true;

            // Store original material state and enable transparency
            Renderer rend = player.GetComponent<Renderer>();
            ghostOriginalColor = Color.white;
            if (rend != null)
            {
                ghostOriginalColor = rend.material.color;

                // Switch material to transparent mode
                var mat = rend.material;
                mat.SetFloat("_Surface", 1f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

                Color ghostColor = ghostOriginalColor;
                ghostColor.a = ghostAlphaMax;
                mat.color = ghostColor;

                ghostTargetRenderer = rend;
                ghostPulsing = true;
            }

            yield return new WaitForSeconds(duration);

            // Restore original material state
            player.IsGhostActive = false;
            ghostPulsing = false;

            if (rend != null)
            {
                var mat = rend.material;
                mat.SetFloat("_Surface", 0f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                mat.SetInt("_ZWrite", 1);
                mat.renderQueue = -1;
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.color = ghostOriginalColor;
            }

            ghostTargetRenderer = null;
            ghostCoroutine = null;
        }
    }
}
