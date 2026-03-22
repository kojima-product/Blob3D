using UnityEngine;
using System.Collections;
using Blob3D.Player;

namespace Blob3D.Gameplay
{
    /// <summary>
    /// パワーアップの時限効果を管理する。
    /// コルーチンで各効果の持続時間を制御。
    /// </summary>
    public class PowerUpEffectManager : MonoBehaviour
    {
        public static PowerUpEffectManager Instance { get; private set; }

        [Header("Magnet Settings")]
        [SerializeField] private float magnetRange = 15f;
        [SerializeField] private float magnetPullSpeed = 20f;

        private Coroutine speedCoroutine;
        private Coroutine magnetCoroutine;
        private Coroutine ghostCoroutine;

        private void Awake()
        {
            Instance = this;
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

        // ---------- マグネット ----------

        public void ApplyMagnet(BlobController player, float duration)
        {
            if (magnetCoroutine != null) StopCoroutine(magnetCoroutine);
            magnetCoroutine = StartCoroutine(MagnetRoutine(player, duration));
        }

        private IEnumerator MagnetRoutine(BlobController player, float duration)
        {
            player.IsMagnetActive = true;
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
            magnetCoroutine = null;
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

            // 半透明にする
            Renderer rend = player.GetComponent<Renderer>();
            Color originalColor = Color.white;
            if (rend != null)
            {
                originalColor = rend.material.color;
                Color ghostColor = originalColor;
                ghostColor.a = 0.4f;
                rend.material.color = ghostColor;
            }

            yield return new WaitForSeconds(duration);

            player.IsGhostActive = false;
            if (rend != null)
            {
                rend.material.color = originalColor;
            }
            ghostCoroutine = null;
        }
    }
}
