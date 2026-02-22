using UnityEngine;

namespace RhythmForge.Ring
{
    public enum RF_HitType
    {
        Kick,
        Snare,
        Hat,
        Clap
    }

    public class RF_RingModel : MonoBehaviour
    {
        [Header("Structure")]
        [SerializeField] private int steps = 16;

        public int Steps => steps;

        private RF_HitType?[] hits;
        private float[] hills; // 0..1 intensity

        void Awake()
        {
            hits = new RF_HitType?[steps];
            hills = new float[steps];
        }

        public void ToggleHit(int step, RF_HitType type)
        {
            if (!IsValid(step)) return;

            if (hits[step].HasValue && hits[step].Value == type)
                hits[step] = null;
            else
                hits[step] = type;
        }

        public RF_HitType? GetHit(int step)
        {
            return IsValid(step) ? hits[step] : null;
        }

        public void AddHill(int step, float amount)
        {
            if (!IsValid(step)) return;
            hills[step] = Mathf.Clamp01(hills[step] + amount);
        }

        public float GetHill(int step)
        {
            return IsValid(step) ? hills[step] : 0f;
        }

        private bool IsValid(int step)
        {
            return step >= 0 && step < steps;
        }
    }
}