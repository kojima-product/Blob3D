using UnityEngine;
using Blob3D.Core;

namespace Blob3D.Gameplay
{
    /// <summary>
    /// Animates a blob being absorbed: pulled toward absorber,
    /// squishes, shrinks, and merges like water droplets combining.
    /// Attach dynamically when absorption starts.
    /// </summary>
    public class AbsorptionEffect : MonoBehaviour
    {
        private Transform absorberTransform;
        private float duration = 0.5f;
        private float elapsed;
        private Vector3 startScale;
        private Vector3 startPos;
        private float startSize;
        private Renderer blobRenderer;
        private MaterialPropertyBlock mpb;
        private Color originalColor;
        private Color absorberColor;

        // Absorption pull particle effect
        private ParticleSystem absorptionParticles;

        // Reference for cleanup
        private BlobBase absorber;
        private BlobBase victim;

        /// <summary>Called right after absorption starts</summary>
        public void Initialize(Transform absorber, float victimSize)
        {
            absorberTransform = absorber;
            startScale = transform.localScale;
            startPos = transform.position;
            startSize = victimSize;
            blobRenderer = GetComponent<Renderer>();
            mpb = new MaterialPropertyBlock();

            if (blobRenderer != null)
            {
                blobRenderer.GetPropertyBlock(mpb);
                originalColor = blobRenderer.material.color;
            }

            // Get absorber's color for color shift during absorption
            Renderer absorberRenderer = absorber != null ? absorber.GetComponent<Renderer>() : null;
            if (absorberRenderer == null)
                absorberRenderer = absorberTransform.GetComponent<Renderer>();
            absorberColor = absorberRenderer != null ? absorberRenderer.material.color : originalColor;

            // Larger blobs take slightly longer to absorb
            duration = Mathf.Lerp(0.3f, 0.7f, Mathf.Clamp01(victimSize / 20f));

            // Disable collider so it can't interact during animation
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            // Disable rigidbody physics
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.velocity = Vector3.zero;
            }

            // Spawn absorption pull particles
            absorptionParticles = CreateAbsorptionParticles();
        }

        /// <summary>Set blob references for cleanup on finish</summary>
        public void SetBlobPair(BlobBase absorberBlob, BlobBase victimBlob)
        {
            absorber = absorberBlob;
            victim = victimBlob;
        }

        private void Update()
        {
            if (absorberTransform == null)
            {
                Finish();
                return;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Ease-in curve: slow start, fast end (like being sucked in)
            float easedT = t * t * t; // Cubic ease-in

            // Smooth ease for scale (starts slow, accelerates)
            float scaleEaseT = t * t; // Quadratic

            // --- Position: pull toward absorber with acceleration ---
            Vector3 targetPos = absorberTransform.position;
            transform.position = Vector3.Lerp(startPos, targetPos, easedT);

            // --- Scale: shrink with organic squish ---
            float shrink = 1f - scaleEaseT;

            // Direction toward absorber for oriented squish
            Vector3 toAbsorber = (targetPos - transform.position).normalized;
            if (toAbsorber.sqrMagnitude < 0.01f) toAbsorber = Vector3.up;

            // Stretch toward absorber (being pulled like taffy)
            float stretchAlongPull = 1f + scaleEaseT * 0.8f; // Stretches up to 1.8x
            float squashPerp = shrink / Mathf.Sqrt(Mathf.Max(stretchAlongPull, 0.3f)); // Volume-ish preservation

            // As it gets close, collapse into a tiny sphere
            if (t > 0.7f)
            {
                float collapseT = (t - 0.7f) / 0.3f;
                stretchAlongPull = Mathf.Lerp(stretchAlongPull, shrink, collapseT);
                squashPerp = shrink;
            }

            Vector3 scale = startScale * shrink;
            // Apply directional deformation
            transform.localScale = new Vector3(
                Mathf.Max(scale.x * squashPerp, 0.01f),
                Mathf.Max(scale.y * stretchAlongPull * shrink, 0.01f),
                Mathf.Max(scale.z * squashPerp, 0.01f)
            );

            // Orient stretch toward absorber
            if (toAbsorber.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toAbsorber, Vector3.up);
                // Swap so Y-axis (stretch axis) points toward absorber
                targetRot *= Quaternion.Euler(90f, 0f, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
            }

            // --- Color: shift toward absorber color and fade to transparent ---
            if (blobRenderer != null)
            {
                // Blend RGB toward the absorber's color as we get pulled in
                Color c = Color.Lerp(originalColor, absorberColor, scaleEaseT * 0.7f);
                c.a = Mathf.Lerp(1f, 0f, scaleEaseT);
                mpb.SetColor("_BaseColor", c);
                blobRenderer.SetPropertyBlock(mpb);
            }

            // --- Update absorption particles position ---
            if (absorptionParticles != null && absorberTransform != null)
            {
                absorptionParticles.transform.position = transform.position;
            }

            // --- Wobble: add jitter during pull (organic feel) ---
            if (t < 0.8f)
            {
                float wobbleAmount = Mathf.Sin(elapsed * 25f) * 0.05f * (1f - t);
                transform.position += new Vector3(
                    Mathf.Sin(elapsed * 31f) * wobbleAmount,
                    Mathf.Cos(elapsed * 37f) * wobbleAmount * 0.15f,
                    Mathf.Sin(elapsed * 43f) * wobbleAmount
                );
            }

            // Clamp Y position to prevent sinking below ground
            {
                Vector3 pos = transform.position;
                pos.y = Mathf.Max(pos.y, 0.1f);
                transform.position = pos;
            }

            if (t >= 1f)
            {
                Finish();
            }
        }

        private void Finish()
        {
            // Clean up absorption pair lock
            if (absorber != null && victim != null)
            {
                BlobBase.ClearAbsorbPair(absorber, victim);
            }

            // Clean up absorption particles
            if (absorptionParticles != null)
            {
                absorptionParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(absorptionParticles.gameObject, absorptionParticles.main.startLifetime.constantMax + 0.1f);
                absorptionParticles = null;
            }

            // Fix: don't destroy pooled AI objects — AISpawner.OnAIDied handles pool return.
            // For player objects, also don't destroy — just deactivate visually.
            // The AbsorptionEffect component itself will be cleaned up by the pool or AI death handler.
            gameObject.SetActive(false);

            // Fix: only destroy non-pooled objects (e.g., SplitBlob primitives).
            // Pooled objects (AI blobs) and player are managed elsewhere.
            // Self-destruct this component only, not the entire GameObject.
            Destroy(this);
        }

        /// <summary>Create particle system for absorption pull effect</summary>
        private ParticleSystem CreateAbsorptionParticles()
        {
            var go = new GameObject("AbsorptionPull");
            go.transform.position = transform.position;
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startColor = Color.Lerp(originalColor, absorberColor, 0.5f);
            main.maxParticles = 30;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.loop = true;
            main.gravityModifier = -0.1f;

            var emission = ps.emission;
            emission.rateOverTime = 20;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Clamp(startSize * 0.15f * 0.5f, 0.1f, 2f);

            // Particles spiral inward toward absorber
            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.radial = new ParticleSystem.MinMaxCurve(-2f);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(originalColor, 0f),
                    new GradientColorKey(absorberColor, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.8f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            // Configure renderer with URP particle material
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");

                var material = new Material(shader);
                material.color = Color.white;
                material.SetFloat("_Surface", 1f);
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.renderQueue = 3000;
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                renderer.material = material;
            }

            ps.Play();
            return ps;
        }
    }
}
