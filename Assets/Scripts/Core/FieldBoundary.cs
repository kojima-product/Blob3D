using UnityEngine;

namespace Blob3D.Core
{
    /// <summary>
    /// フィールド境界の可視化とミニマップ用。
    /// 円形フィールドの境界をLineRendererで描画する。
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class FieldBoundary : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int segments = 64;
        [SerializeField] private float lineWidth = 0.5f;
        [SerializeField] private Color boundaryColor = new Color(1f, 0.3f, 0.3f, 0.5f);
        [SerializeField] private float warningDistance = 20f;
        [SerializeField] private Color shrinkPulseColor = new Color(1f, 0f, 0f, 0.8f);
        [SerializeField] private float shrinkPulseSpeed = 4f;

        private LineRenderer lineRenderer;
        private float previousRadius;

        private void Start()
        {
            lineRenderer = GetComponent<LineRenderer>();
            previousRadius = GameManager.Instance.FieldRadius;
            DrawBoundary();
        }

        private void Update()
        {
            float radius = GameManager.Instance.FieldRadius;
            // Only redraw when radius actually changed or color needs updating
            bool needsRedraw = Mathf.Abs(radius - previousRadius) > 0.01f;
            bool needsColorUpdate = radius < previousRadius - 0.01f ||
                (GameManager.Instance.RemainingTime < 90f && GameManager.Instance.RemainingTime > 0f);
            if (needsRedraw || needsColorUpdate)
            {
                DrawBoundary();
            }
        }

        private void DrawBoundary()
        {
            float radius = GameManager.Instance.FieldRadius;
            bool isShrinking = radius < previousRadius - 0.01f;
            previousRadius = radius;

            lineRenderer.positionCount = segments + 1;
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.loop = true;
            lineRenderer.useWorldSpace = true;

            // Pulse red when field is shrinking
            Color currentColor;
            if (isShrinking || (GameManager.Instance.RemainingTime < 90f && GameManager.Instance.RemainingTime > 0f))
            {
                float pulse = (Mathf.Sin(Time.time * shrinkPulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
                currentColor = Color.Lerp(boundaryColor, shrinkPulseColor, pulse);
            }
            else
            {
                currentColor = boundaryColor;
            }

            lineRenderer.startColor = currentColor;
            lineRenderer.endColor = currentColor;

            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                lineRenderer.SetPosition(i, new Vector3(x, 0.1f, z));
            }
        }

        /// <summary>プレイヤーが境界に近づいた時の警告度（0〜1）を返す</summary>
        public float GetBoundaryProximity(Vector3 position)
        {
            float radius = GameManager.Instance.FieldRadius;
            Vector3 flat = new Vector3(position.x, 0f, position.z);
            float distFromEdge = radius - flat.magnitude;

            if (distFromEdge >= warningDistance) return 0f;
            if (distFromEdge <= 0f) return 1f;

            return 1f - (distFromEdge / warningDistance);
        }
    }
}
