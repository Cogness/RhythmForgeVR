    using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using RhythmForge.Ring;

namespace RhythmForge.RingEditing
{
    public class RF_RingDrawTool : MonoBehaviour
    {
        [Header("Input")]
        public InputActionProperty drawAction;

        [Header("Pen")]
        public Transform penTip;

        [Header("Ring Prefab")]
        public RF_RingModel ringPrefab;

        [Header("Settings")]
        public float minPointDistance = 0.02f;
        public int minPoints = 25;

        private List<Vector3> points = new();
        private bool isDrawing;

        void OnEnable() => drawAction.action.Enable();
        void OnDisable() => drawAction.action.Disable();

        void Update()
        {
            float value = drawAction.action.ReadValue<float>();
            if (value > 0.01f)
                Debug.Log($"DRAW value: {value:0.00}");
            if (!isDrawing && value > 0.75f)
            {
                isDrawing = true;
                points.Clear();
            }

            if (isDrawing && value > 0.75f)
            {
                Vector3 p = penTip.position;

                if (points.Count == 0 ||
                    Vector3.Distance(points[points.Count - 1], p) > minPointDistance)
                {
                    points.Add(p);
                }
            }

            if (isDrawing && value < 0.25f)
            {
                isDrawing = false;

                if (points.Count >= minPoints)
                {
                    SpawnRing();
                }
            }
        }

        void SpawnRing()
        {
            // Center
            Vector3 center = Vector3.zero;
            foreach (var p in points) center += p;
            center /= points.Count;

            // Average radius
            float radius = 0f;
            foreach (var p in points) radius += Vector3.Distance(center, p);
            radius /= points.Count;

            // Estimate drawing plane normal
            // (use first point as reference, accumulate cross products)
            Vector3 normal = Vector3.zero;
            for (int i = 2; i < points.Count; i++)
            {
                Vector3 a = points[i - 1] - points[0];
                Vector3 b = points[i] - points[0];
                normal += Vector3.Cross(a, b);
            }
            if (normal.sqrMagnitude < 1e-6f)
                normal = Vector3.up;
            else
                normal.Normalize();

            // Make it face the user (consistent normal direction)
            Vector3 toCamera = (Camera.main ? (Camera.main.transform.position - center) : Vector3.back);
            if (Vector3.Dot(normal, toCamera) > 0f)
                normal = -normal;

            // Our ring is authored in XZ plane with +Y normal, so rotate +Y to the drawing normal
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, normal);

            var ring = Instantiate(ringPrefab, center, rot);
            ring.transform.localScale = Vector3.one * radius;

            Debug.Log("Ring spawned.");
        }
    }
}