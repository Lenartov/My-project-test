using System;
using System.IO;
using UnityEngine;

namespace MyDreamGame
{
    internal sealed class RallyRecording
    {
        internal const float StepSeconds = 1f / 120f;
        internal const int SampleStride = 8;
        private const string SaveKey = "MyDreamGame.BlackRidge.Stage01.Reference.WetWeather";
        private const int Format = 0x31525242;
        private const int HeaderBytes = 16;
        private const int PoseBytes = 8;

        internal readonly struct Pose
        {
            internal readonly float Distance;
            internal readonly float Lateral;
            internal readonly float Heading;

            internal Pose(float distance, float lateral, float heading)
            {
                Distance = distance;
                Lateral = lateral;
                Heading = heading;
            }

            internal Pose(RallyCar car) : this(car.Distance, car.Lateral, car.Heading)
            {
            }
        }

        private readonly Pose[] poses;
        private readonly int firstSplitTick;
        private readonly int secondSplitTick;
        private readonly int finishTick;
        private readonly float[] firstCrossing;

        internal float Time => finishTick * StepSeconds;

        internal RallyRecording(Pose[] poses, int firstSplitTick, int secondSplitTick, int finishTick, int finishDistance)
        {
            this.poses = poses;
            this.firstSplitTick = firstSplitTick;
            this.secondSplitTick = secondSplitTick;
            this.finishTick = finishTick;
            firstCrossing = BuildProgressTimes(finishDistance);
        }

        internal float SplitTime(int index)
        {
            return (index == 0 ? firstSplitTick : secondSplitTick) * StepSeconds;
        }

        internal float TimeAtDistance(float distance)
        {
            float meter = Mathf.Clamp(distance, 0f, firstCrossing.Length - 1f);
            int index = Mathf.Min((int)meter, firstCrossing.Length - 2);
            return Mathf.Lerp(firstCrossing[index], firstCrossing[index + 1], meter - index);
        }

        internal Pose PoseAtTime(float elapsed)
        {
            float tick = Mathf.Clamp(elapsed / StepSeconds, 0f, finishTick);
            int index = Mathf.Min((int)tick / SampleStride, poses.Length - 2);
            float blend = Mathf.InverseLerp(TickAt(index), TickAt(index + 1), tick);
            Pose a = poses[index];
            Pose b = poses[index + 1];
            float heading = Mathf.LerpAngle(a.Heading * Mathf.Rad2Deg, b.Heading * Mathf.Rad2Deg, blend) * Mathf.Deg2Rad;
            return new Pose(Mathf.Lerp(a.Distance, b.Distance, blend), Mathf.Lerp(a.Lateral, b.Lateral, blend), heading);
        }

        internal bool TrySave()
        {
            using MemoryStream buffer = new MemoryStream(HeaderBytes + poses.Length * PoseBytes);
            using BinaryWriter writer = new BinaryWriter(buffer);
            writer.Write(Format);
            writer.Write(firstSplitTick);
            writer.Write(secondSplitTick);
            writer.Write(finishTick);
            foreach (Pose pose in poses)
            {
                writer.Write(pose.Distance);
                writer.Write((short)Mathf.RoundToInt(pose.Lateral * 100f));
                writer.Write((short)Mathf.RoundToInt(pose.Heading * 10000f));
            }

            string encoded = Convert.ToBase64String(buffer.GetBuffer(), 0, (int)buffer.Length);
            try
            {
                PlayerPrefs.SetString(SaveKey, encoded);
                PlayerPrefs.Save();
                return true;
            }
            catch (PlayerPrefsException exception)
            {
                Debug.LogWarning("Black Ridge could not save the reference run. " + exception.Message);
                return false;
            }
        }

        internal static RallyRecording Load(RallyCourse course)
        {
            int maxTick = Mathf.RoundToInt(RallyCourse.TimeLimit / StepSeconds);
            int maxBytes = HeaderBytes + SampleCount(maxTick) * PoseBytes;
            string encoded = PlayerPrefs.GetString(SaveKey, string.Empty);
            if (encoded.Length == 0 || encoded.Length > ((maxBytes + 2) / 3) * 4)
            {
                return null;
            }

            byte[] bytes = new byte[maxBytes];
            if (!Convert.TryFromBase64String(encoded, bytes, out int written) || written < HeaderBytes)
            {
                return null;
            }

            using MemoryStream buffer = new MemoryStream(bytes, 0, written, false);
            using BinaryReader reader = new BinaryReader(buffer);
            int format = reader.ReadInt32();
            int splitOne = reader.ReadInt32();
            int splitTwo = reader.ReadInt32();
            int finish = reader.ReadInt32();
            if (format != Format || splitOne <= 0 || splitTwo <= splitOne || finish <= splitTwo || finish > maxTick)
            {
                return null;
            }

            int sampleCount = SampleCount(finish);
            if (written != HeaderBytes + sampleCount * PoseBytes)
            {
                return null;
            }

            Pose[] samples = new Pose[sampleCount];
            for (int i = 0; i < samples.Length; i++)
            {
                float distance = reader.ReadSingle();
                float lateral = reader.ReadInt16() * 0.01f;
                float heading = reader.ReadInt16() * 0.0001f;
                if (!(distance >= course.Start && distance <= course.Finish + 2f) ||
                    Mathf.Abs(lateral) > RallyCourse.BarrierOffset || Mathf.Abs(heading) > Mathf.PI + 0.001f)
                {
                    return null;
                }

                samples[i] = new Pose(distance, lateral, heading);
            }

            if (samples[0].Distance != 0f || samples[samples.Length - 1].Distance < course.Finish)
            {
                return null;
            }

            return new RallyRecording(samples, splitOne, splitTwo, finish, Mathf.RoundToInt(course.Finish));
        }

        private static int SampleCount(int finish)
        {
            return 1 + (finish + SampleStride - 1) / SampleStride;
        }

        private int TickAt(int poseIndex)
        {
            return Mathf.Min(poseIndex * SampleStride, finishTick);
        }

        private float[] BuildProgressTimes(int finishDistance)
        {
            float[] crossings = new float[finishDistance + 1];
            int meter = 1;
            for (int i = 1; i < poses.Length && meter <= finishDistance; i++)
            {
                Pose a = poses[i - 1];
                Pose b = poses[i];
                while (meter <= finishDistance && meter <= b.Distance)
                {
                    float blend = Mathf.InverseLerp(a.Distance, b.Distance, meter);
                    crossings[meter] = Mathf.Lerp(TickAt(i - 1), TickAt(i), blend) * StepSeconds;
                    meter++;
                }
            }

            return crossings;
        }
    }
}
