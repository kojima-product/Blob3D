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

            // --- Color: fade to transparent ---
            if (blobRenderer != null)
            {
                Color c = originalColor;
                c.a = Mathf.Lerp(1f, 0f, scaleEaseT);
                mpb.SetColor("_BaseColor", c);
                blobRenderer.SetPropertyBlock(mpb);
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

            gameObject.SetActive(false);
            Destroy(gameObject, 0.1f);
        }
    }
}
