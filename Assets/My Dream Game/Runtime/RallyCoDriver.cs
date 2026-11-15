using System.Collections.Generic;
using UnityEngine;

namespace MyDreamGame
{
    internal sealed class RallyCoDriver
    {
        private readonly struct Call
        {
            internal readonly float Distance;
            internal readonly float LeadSeconds;
            internal readonly float MinimumLead;
            internal readonly float MaximumLead;
            internal readonly AudioClip Clip;

            internal Call(float distance, float leadSeconds, float minimumLead, float maximumLead, AudioClip clip)
            {
                Distance = distance;
                LeadSeconds = leadSeconds;
                MinimumLead = minimumLead;
                MaximumLead = maximumLead;
                Clip = clip;
            }
        }

        private readonly AudioSource radio;
        private readonly List<Call> calls = new List<Call>();
        private int nextCall;
        private bool muted;

        internal bool Speaking => radio.isPlaying;
        internal bool Muted => muted;

        internal RallyCoDriver(RallyCourse course, AudioSource radio)
        {
            this.radio = radio;
            AudioClip[] left = LoadTurns("Left");
            AudioClip[] right = LoadTurns("Right");
            AudioClip waterLeft = Load("WaterLeft");
            AudioClip waterRight = Load("WaterRight");
            foreach (RallyCourse.Turn turn in course.Turns)
            {
                AudioClip clip = (turn.Direction > 0 ? right : left)[turn.Severity - 2];
                float lead = Mathf.Lerp(4.1f, 3f, (turn.Severity - 2) / 3f);
                calls.Add(new Call(turn.Start, lead, 48f, 134f, clip));
            }

            foreach (RallyCourse.Puddle puddle in course.Puddles)
            {
                AudioClip clip = puddle.Lateral > 0f ? waterRight : waterLeft;
                calls.Add(new Call(puddle.Distance - puddle.HalfLength, 2.9f, 38f, 105f, clip));
            }

            calls.Add(new Call(course.Finish, 3.2f, 48f, 115f, Load("Finish")));
            calls.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        }

        internal void Update(float distance, float speed)
        {
            while (nextCall < calls.Count)
            {
                Call call = calls[nextCall];
                float remaining = call.Distance - distance;
                if (remaining < 7f)
                {
                    nextCall++;
                    continue;
                }

                float lead = Mathf.Clamp(speed * call.LeadSeconds, call.MinimumLead, call.MaximumLead);
                if (remaining > lead || radio.isPlaying)
                {
                    return;
                }

                nextCall++;
                if (!muted)
                {
                    radio.PlayOneShot(call.Clip);
                }

                return;
            }
        }

        internal void Reset()
        {
            radio.Stop();
            nextCall = 0;
        }

        internal void Toggle()
        {
            muted = !muted;
            if (muted)
            {
                radio.Stop();
            }
        }

        internal void SetPaused(bool paused)
        {
            if (paused)
            {
                radio.Pause();
            }
            else
            {
                radio.UnPause();
            }
        }

        private static AudioClip[] LoadTurns(string direction)
        {
            AudioClip[] turns = new AudioClip[4];
            for (int i = 0; i < turns.Length; i++)
            {
                turns[i] = Load(direction + (i + 2));
            }

            return turns;
        }

        private static AudioClip Load(string name)
        {
            return Resources.Load<AudioClip>("MyDreamGame/Calls/" + name);
        }
    }
}
