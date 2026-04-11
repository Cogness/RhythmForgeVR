using System;

namespace RhythmForge.Sequencer
{
    [Serializable]
    public class Transport
    {
        public bool playing;
        public string mode = "scene"; // "scene" or "arrangement"
        public double nextNoteTime;
        public int sceneStep;
        public int slotIndex = -1;
        public int slotStep;
        public string playbackSceneId;
        public int absoluteBar = 1;
    }
}
