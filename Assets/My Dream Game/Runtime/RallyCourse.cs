using System.Collections.Generic;
using UnityEngine;

namespace MyDreamGame
{
    internal sealed class RallyCourse
    {
        internal const float RoadHalfWidth = 3.15f;
        internal const float BarrierOffset = 4.85f;
        internal const float FirstSplit = 600f;
        internal const float SecondSplit = 1260f;
        internal const float TimeLimit = 145f;
        internal const float GalleryStart = 934f;
        internal const float GalleryEnd = 982f;
        private const float SampleSpacing = 2f;
        private const float Runout = 60f;

        // Each turn eases in and out. The signed angle is relative to the current heading.
        private static readonly Vector2[] Layout =
        {
            new Vector2(112f, 0f), new Vector2(110f, 46f), new Vector2(84f, 0f),
            new Vector2(90f, -88f), new Vector2(72f, 0f), new Vector2(102f, 69f),
            new Vector2(54f, 0f), new Vector2(85f, -76f), new Vector2(86f, 0f),
            new Vector2(118f, 43f), new Vector2(112f, 0f), new Vector2(76f, 61f),
            new Vector2(58f, 0f), new Vector2(96f, -116f), new Vector2(88f, 0f),
            new Vector2(90f, 83f), new Vector2(64f, 0f), new Vector2(94f, -82f),
            new Vector2(110f, 0f), new Vector2(114f, 49f), new Vector2(102f, 0f)
        };

        internal readonly struct Frame
        {
            internal readonly Vector3 Center;
            internal readonly float Heading;
            internal readonly float Bank;

            internal Frame(Vector3 center, float heading, float bank)
            {
                Center = center;
                Heading = heading;
                Bank = bank;
            }

            internal Vector3 Forward => new Vector3(Mathf.Sin(Heading), 0f, Mathf.Cos(Heading));
            internal Vector3 Right => new Vector3(Mathf.Cos(Heading), 0f, -Mathf.Sin(Heading));

            internal Vector3 Point(float lateral, float height = 0f)
            {
                return Center + Right * lateral + Vector3.up * (lateral * Bank + height);
            }
        }

        internal readonly struct Turn
        {
            internal readonly float Start;
            internal readonly float End;
            internal readonly float Angle;
            internal readonly int Direction;
            internal readonly int Severity;
            internal readonly int Speed;

            internal Turn(float start, float end, float degrees)
            {
                Start = start;
                End = end;
                Angle = degrees * Mathf.Deg2Rad;
                Direction = degrees > 0f ? 1 : -1;
                float minimumRadius = (end - start) / (2f * Mathf.Abs(degrees) * Mathf.Deg2Rad);
                Speed = Mathf.RoundToInt(Mathf.Sqrt(4.4f * minimumRadius) * 3.6f / 5f) * 5;
                Severity = Mathf.Clamp(Mathf.RoundToInt((Speed - 20f) / 11f), 2, 5);
            }
        }

        internal readonly struct Puddle
        {
            internal readonly float Distance;
            internal readonly float Lateral;
            internal readonly float HalfLength;
            internal readonly float HalfWidth;

            internal Puddle(float distance, float lateral, float halfLength, float halfWidth)
            {
                Distance = distance;
                Lateral = lateral;
                HalfLength = halfLength;
                HalfWidth = halfWidth;
            }

            internal float WidthAt(float distance)
            {
                float along = (distance - Distance) / HalfLength;
                return HalfWidth * Mathf.Sqrt(Mathf.Max(0f, 1f - along * along)) *
                    (0.91f + 0.11f * Mathf.Sin(distance * 0.9f));
            }
        }

        internal readonly struct Surface
        {
            internal readonly float Grip;
            internal readonly float Water;
            internal readonly float Gravel;

            internal Surface(float grip, float water, float gravel)
            {
                Grip = grip;
                Water = water;
                Gravel = gravel;
            }
        }

        private readonly Frame[] samples;
        internal readonly List<Turn> Turns = new List<Turn>();
        internal readonly Puddle[] Puddles =
        {
            new Puddle(181f, 1.18f, 10f, 1.05f),
            new Puddle(351f, -1.17f, 12f, 1.15f),
            new Puddle(522f, 1.29f, 14f, 1.05f),
            new Puddle(691f, -1.1f, 12f, 1.16f),
            new Puddle(885f, 1.18f, 17f, 1.3f),
            new Puddle(1112f, 1.2f, 11f, 1.12f),
            new Puddle(1302f, -1.32f, 16f, 1.24f),
            new Puddle(1466f, 1.3f, 15f, 1.1f),
            new Puddle(1639f, -1.15f, 14f, 1.2f),
            new Puddle(1778f, 0.9f, 18f, 1.3f)
        };

        internal float Finish { get; }
        internal float End => Finish + Runout;
        internal float Start => -Runout;

        internal RallyCourse()
        {
            foreach (Vector2 section in Layout)
            {
                if (section.y != 0f)
                {
                    Turns.Add(new Turn(Finish, Finish + section.x, section.y));
                }

                Finish += section.x;
            }

            samples = new Frame[Mathf.CeilToInt((End - Start) / SampleSpacing) + 1];
            Vector3 center = new Vector3(0f, 0f, Start);
            for (int i = 0; i < samples.Length; i++)
            {
                float s = Start + i * SampleSpacing;
                float heading = HeadingAt(s);
                float bank = Mathf.Clamp(-(HeadingAt(s + 1f) - HeadingAt(s - 1f)) * 1.65f, -0.075f, 0.075f);
                center.y = Elevation(s);
                samples[i] = new Frame(center, heading, bank);
                float midpointHeading = HeadingAt(s + SampleSpacing * 0.5f);
                center.x += SampleSpacing * Mathf.Sin(midpointHeading);
                center.z += SampleSpacing * Mathf.Cos(midpointHeading);
            }
        }

        internal Frame At(float distance)
        {
            float index = Mathf.Clamp((distance - Start) / SampleSpacing, 0f, samples.Length - 1f);
            int left = Mathf.Min((int)index, samples.Length - 2);
            float blend = index - left;
            Frame a = samples[left];
            Frame b = samples[left + 1];
            return new Frame(Vector3.Lerp(a.Center, b.Center, blend),
                Mathf.Lerp(a.Heading, b.Heading, blend), Mathf.Lerp(a.Bank, b.Bank, blend));
        }

        internal void Locate(Vector3 point, ref float distance, out float lateral)
        {
            int middle = Mathf.RoundToInt((distance - Start) / SampleSpacing);
            int first = Mathf.Max(0, middle - 5);
            int last = Mathf.Min(samples.Length - 2, middle + 5);
            float bestSquare = float.PositiveInfinity;
            for (int i = first; i <= last; i++)
            {
                Vector3 offset = point - samples[i].Center;
                Vector3 segment = samples[i + 1].Center - samples[i].Center;
                offset.y = 0f;
                segment.y = 0f;
                float t = Mathf.Clamp01(Vector3.Dot(offset, segment) / segment.sqrMagnitude);
                float square = (offset - t * segment).sqrMagnitude;
                if (square < bestSquare)
                {
                    bestSquare = square;
                    distance = Start + (i + t) * SampleSpacing;
                }
            }

            Frame frame = At(distance);
            lateral = Vector3.Dot(point - frame.Center, frame.Right);
        }

        internal static float RainAt(float distance)
        {
            float front = Mathf.SmoothStep(0f, 1f, (distance - 420f) / 275f);
            float tail = 1f - Mathf.SmoothStep(0f, 1f, (distance - 1370f) / 410f);
            return 0.24f + 0.61f * front * tail + 0.06f * (1f + Mathf.Sin(distance * 0.012f));
        }

        internal static float GalleryCover(float distance)
        {
            float entrance = Mathf.SmoothStep(0f, 1f, (distance - GalleryStart) / 3f);
            float exit = Mathf.SmoothStep(0f, 1f, (GalleryEnd - distance) / 3f);
            return entrance * exit;
        }

        internal Surface SurfaceAt(float distance, float lateral, float speed)
        {
            float gravel = Mathf.InverseLerp(RoadHalfWidth - 0.12f, RoadHalfWidth + 0.42f, Mathf.Abs(lateral));
            float asphaltGrip = Mathf.Lerp(0.80f, 0.69f, RainAt(distance));
            float grip = Mathf.Lerp(asphaltGrip, 0.45f, gravel);
            foreach (Puddle patch in Puddles)
            {
                if (Mathf.Abs(distance - patch.Distance) < patch.HalfLength)
                {
                    float width = patch.WidthAt(distance);
                    float across = Mathf.Abs(lateral - patch.Lateral);
                    float water = Mathf.SmoothStep(0f, 1f, (width - across) / 0.57f);
                    if (water > 0f)
                    {
                        float wetGrip = Mathf.Lerp(0.61f, 0.30f, Mathf.InverseLerp(10f, 30f, speed));
                        return new Surface(Mathf.Lerp(grip, wetGrip, water), water, gravel);
                    }
                }
            }

            return new Surface(grip, 0f, gravel);
        }

        private float HeadingAt(float distance)
        {
            float heading = 0f;
            foreach (Turn turn in Turns)
            {
                if (distance <= turn.Start)
                {
                    break;
                }

                float t = Mathf.InverseLerp(turn.Start, turn.End, distance);
                heading += turn.Angle * (t - Mathf.Sin(t * 2f * Mathf.PI) / (2f * Mathf.PI));
            }

            return heading;
        }

        private static float Elevation(float distance)
        {
            return -distance * 0.014f + 4.4f * Mathf.Sin(distance / 127f) +
                1.1f * Mathf.Sin(distance / 40f) + 0.19f * Mathf.Sin(distance / 7.8f);
        }
    }
}
