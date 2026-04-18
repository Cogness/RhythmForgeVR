using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmForge.Core.Analysis
{
    [Serializable]
    public class KinematicPoint
    {
        public Vector3 position;
        public float pressure;
        public float tilt; // Angle in radians between stylus forward and plane normal
        public Quaternion rotation;
        public float time; // Relative time since stroke start
        
        // Projected 2D data (filled during analysis)
        public Vector2 pos2D;
        public float speed;

        // Kinematic features (filled during analysis — Phase 1)
        public Vector2 tiltXY;  // pen-local tilt projected into stroke plane's (right, up) frame
    }

    [Serializable]
    public class StrokeKinematics
    {
        public List<KinematicPoint> points = new List<KinematicPoint>();
        public float totalTime;

        // Plane normal set after BuildStrokeFrame (Phase 1)
        public Vector3 planeNormal;

        public StrokeKinematics()
        {
            points = new List<KinematicPoint>();
        }

        public void AddPoint(Vector3 pos, float pressure, float tilt, Quaternion rot, float time)
        {
            points.Add(new KinematicPoint
            {
                position = pos,
                pressure = pressure,
                tilt = tilt,
                rotation = rot,
                time = time
            });
            totalTime = time;
        }

        /// <summary>How flat the stroke is. 1 = perfectly planar, 0 = volumetric/spherical. Computed in StrokeCapture.FinishStroke.</summary>
        public float planarity;

        /// <summary>How much the stroke jabbed toward/away from the user (0–1). 1 = pure thrust, 0 = side-to-side. Computed in StrokeCapture.FinishStroke.</summary>
        public float thrustAxis;

        /// <summary>How aligned the stroke's principal axis is with world up (0–1). 1 = vertical arabesque, 0 = horizontal. Computed in StrokeCapture.FinishStroke.</summary>
        public float verticalityWorld;

        /// <summary>Stores the stroke plane's outward normal for later tilt projection.</summary>
        public void SetPlaneNormal(Vector3 normal)
        {
            planeNormal = normal;
        }
    }
}
