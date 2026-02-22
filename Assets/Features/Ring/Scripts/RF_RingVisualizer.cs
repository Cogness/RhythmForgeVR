using UnityEngine;

namespace RhythmForge.Ring
{
    [RequireComponent(typeof(LineRenderer))]
    public class RF_RingVisualizer : MonoBehaviour
    {
        [SerializeField] private RF_RingModel model;
        [SerializeField] private RF_RingPlayback playback;
        [SerializeField] private Transform playhead;

        [SerializeField] private float radius = 1f;

        private LineRenderer line;

        void Awake()
        {
            line = GetComponent<LineRenderer>();
        }

        void Update()
        {
            DrawRing();
            UpdatePlayhead();
        }

        void DrawRing()
        {
            int steps = model.Steps;
            line.positionCount = steps + 1;

            for (int i = 0; i <= steps; i++)
            {
                int step = i % steps;
                float angle = (step / (float)steps) * Mathf.PI * 2f;

                float hill = model.GetHill(step);
                float r = radius + hill * 0.15f;

                Vector3 pos = new Vector3(
                    Mathf.Cos(angle) * r,
                    0f,
                    Mathf.Sin(angle) * r
                );

                line.SetPosition(i, pos);
            }
        }

        void UpdatePlayhead()
        {
            float phase = playback.GetPhase();
            float angle = phase * Mathf.PI * 2f;

            playhead.localPosition = new Vector3(
                Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius
            );
        }
    }
}