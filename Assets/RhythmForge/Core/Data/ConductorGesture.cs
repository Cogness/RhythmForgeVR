using System;
using UnityEngine;

namespace RhythmForge.Core.Data
{
    public enum ConductorGestureKind
    {
        Sway,
        LiftTendu,
        FadePlie,
        CutOff
    }

    [Serializable]
    public struct ConductorGestureEvent
    {
        public ConductorGestureEvent(
            ConductorGestureKind kind,
            string targetZoneId,
            float magnitude,
            Vector3 originPosition,
            double dspTime)
        {
            this.kind = kind;
            this.targetZoneId = targetZoneId;
            this.magnitude = magnitude;
            this.originPosition = originPosition;
            this.dspTime = dspTime;
        }

        public ConductorGestureKind kind;
        public string targetZoneId;
        public float magnitude;
        public Vector3 originPosition;
        public double dspTime;
    }
}
