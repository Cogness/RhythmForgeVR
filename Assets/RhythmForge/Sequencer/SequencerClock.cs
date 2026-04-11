namespace RhythmForge.Sequencer
{
    public static class SequencerClock
    {
        /// <summary>
        /// Duration of one step (16th note) in seconds.
        /// </summary>
        public static float StepDuration(float tempo)
        {
            return 60f / tempo / 4f;
        }

        /// <summary>
        /// Swing offset for odd steps, in seconds.
        /// </summary>
        public static float SwingOffset(float swing, float stepDuration)
        {
            return swing * stepDuration * 0.45f;
        }
    }
}
