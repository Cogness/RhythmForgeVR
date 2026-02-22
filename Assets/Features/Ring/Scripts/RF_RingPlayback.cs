using UnityEngine;

namespace RhythmForge.Ring
{
    public class RF_RingPlayback : MonoBehaviour
    {
        [SerializeField] private RF_RingModel model;
        [SerializeField] private float loopDuration = 2f;
        [SerializeField] private float hillStrength = 0.2f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip kick;
        [SerializeField] private AudioClip snare;
        [SerializeField] private AudioClip hat;
        [SerializeField] private AudioClip clap;

        private float phase;
        private int previousStep = -1;

        void Update()
        {
            AdvancePhase(Time.deltaTime);
            CheckTrigger();
        }

        void AdvancePhase(float dt)
        {
            int step = PhaseToStep(phase);
            float hill = model.GetHill(step);

            float speedMultiplier = 1f - hill * hillStrength;
            phase += (dt / loopDuration) * speedMultiplier;
            phase %= 1f;
        }

        void CheckTrigger()
        {
            int step = PhaseToStep(phase);

            if (step == previousStep)
                return;

            previousStep = step;

            var hit = model.GetHit(step);
            if (hit.HasValue)
                PlayHit(hit.Value);
        }

        void PlayHit(RF_HitType type)
        {
            AudioClip clip = type switch
            {
                RF_HitType.Kick => kick,
                RF_HitType.Snare => snare,
                RF_HitType.Hat => hat,
                RF_HitType.Clap => clap,
                _ => null
            };

            if (clip != null)
                audioSource.PlayOneShot(clip);
        }

        int PhaseToStep(float p)
        {
            return Mathf.FloorToInt(p * model.Steps) % model.Steps;
        }

        public float GetPhase() => phase;
    }
}