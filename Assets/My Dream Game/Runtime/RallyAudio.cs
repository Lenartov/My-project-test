using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace MyDreamGame
{
    internal sealed class RallyAudio : IDisposable
    {
        private const int SampleRate = 22050;
        private readonly MonoBehaviour host;
        private readonly AudioSource music;
        private readonly AudioSource engine;
        private readonly AudioSource weather;
        private readonly AudioSource gravel;
        private readonly AudioSource effects;
        private readonly RallyCoDriver coDriver;
        private readonly AudioClip stockTrack;
        private readonly AudioClip collision;
        private readonly AudioClip tick;
        private AudioClip customTrack;
        private Coroutine loadRoutine;
        private UnityWebRequest request;
        private bool muted;
        private string trackLabel = "BLACK ICE";

        internal string MusicStatus => muted ? "MUTED  /  " + trackLabel : trackLabel;

        internal RallyAudio(MonoBehaviour host, RallyCourse course, List<Object> assets)
        {
            this.host = host;
            stockTrack = Resources.Load<AudioClip>("MyDreamGame/Black Ice");
            engine = Source("Engine", host.transform, Clip("Flat-four engine", 1f, EngineSignal, assets), 0.22f, true);
            System.Random random = new System.Random(2030);
            float slowNoise = 0f;
            weather = Source("Rain and tyre wash", host.transform, Clip("Rain wash", 3f, t =>
            {
                float noise = (float)random.NextDouble() * 2f - 1f;
                slowNoise = Mathf.Lerp(slowNoise, noise, 0.022f);
                float edge = Mathf.Min(1f, t * 11f, (3f - t) * 11f);
                return (slowNoise * 1.4f + noise * 0.18f) * edge;
            }, assets), 0.18f, true);
            float rattle = 0f;
            float nextStone = 0f;
            gravel = Source("Gravel under the tyres", host.transform, Clip("Loose stones", 1.35f, t =>
            {
                if (t >= nextStone)
                {
                    nextStone = t + 0.006f + 0.042f * (float)random.NextDouble();
                    rattle = 0.38f + 0.62f * (float)random.NextDouble();
                }

                rattle *= 0.981f;
                float noise = (float)random.NextDouble() * 2f - 1f;
                float edge = Mathf.Min(1f, t * 35f, (1.35f - t) * 35f);
                return rattle * (noise * 0.63f + Mathf.Sin(t * 977f) * 0.21f) * edge;
            }, assets), 0f, true);
            collision = Clip("Guardrail strike", 0.45f, t =>
            {
                float noise = (float)random.NextDouble() * 2f - 1f;
                return Mathf.Exp(-t * 15f) * Mathf.Min(t * 240f, 1f) *
                    (noise * 0.28f + Mathf.Sin(t * 565f) * 0.38f + Mathf.Sin(t * 139f) * 0.25f);
            }, assets);
            tick = Clip("Timing signal", 0.16f, t =>
                Mathf.Sin(t * Mathf.PI * 2f * 510f) * Mathf.Exp(-t * 24f) * Mathf.Min(1f, t * 260f), assets);
            effects = Source("Timing and impacts", host.transform, null, 0.72f, false);
            music = Source("Music", host.transform, stockTrack, 0.36f, true);
            music.bypassReverbZones = true;
            AudioSource radio = Source("Co-driver intercom", host.transform, null, 0.69f, false);
            radio.bypassReverbZones = true;
            coDriver = new RallyCoDriver(course, radio);
        }

        internal void Update(RallyCar car, bool driving, bool accelerating, float dt)
        {
            float blend = 1f - Mathf.Exp(-13f * dt);
            float rpm = driving ? car.Rpm : 950f;
            engine.pitch = Mathf.Lerp(engine.pitch, Mathf.Clamp(rpm / 2380f, 0.48f, 2.98f), blend);
            engine.volume = Mathf.Lerp(engine.volume, accelerating ? 0.31f : 0.17f, blend);
            float airSpeed = driving ? Mathf.InverseLerp(3f, 35f, car.Speed) : 0f;
            float rain = RallyCourse.RainAt(car.Distance) * Mathf.Lerp(1f, 0.25f, RallyCourse.GalleryCover(car.Distance));
            float wash = driving ? Mathf.Max(car.LeftWater, car.RightWater) * Mathf.InverseLerp(5f, 28f, car.Speed) : 0f;
            weather.volume = Mathf.Lerp(weather.volume, 0.08f + rain * 0.18f + airSpeed * 0.36f + wash * 0.31f, blend);
            weather.pitch = Mathf.Lerp(weather.pitch, 0.85f + airSpeed * 0.33f + wash * 0.12f, blend);
            float gravelGain = driving ? car.Gravel * Mathf.InverseLerp(2f, 18f, car.Speed) * 0.53f : 0f;
            gravel.volume = Mathf.Lerp(gravel.volume, gravelGain, blend);
            gravel.pitch = Mathf.Lerp(gravel.pitch, 0.78f + airSpeed * 0.62f, blend);
            if (driving)
            {
                coDriver.Update(car.Distance, car.Speed);
            }

            float musicGain = coDriver.Speaking ? 0.58f : 1f;
            music.volume = Mathf.Lerp(music.volume, (0.40f - airSpeed * 0.10f) * musicGain, blend);
        }

        internal void PlayTick(bool green)
        {
            effects.pitch = green ? 1.50f : 1f;
            effects.PlayOneShot(tick, 0.26f);
        }

        internal void PlayImpact(float speed)
        {
            effects.pitch = 0.91f;
            effects.PlayOneShot(collision, Mathf.Clamp01(speed * 0.13f));
        }

        internal void ToggleMusic()
        {
            muted = !muted;
            music.mute = muted;
        }

        internal bool ToggleCoDriver()
        {
            coDriver.Toggle();
            return !coDriver.Muted;
        }

        internal void ResetCalls()
        {
            coDriver.Reset();
        }

        internal void SetPaused(bool paused)
        {
            coDriver.SetPaused(paused);
            if (paused)
            {
                engine.Pause();
                weather.Pause();
                gravel.Pause();
                effects.Pause();
            }
            else
            {
                engine.UnPause();
                weather.UnPause();
                gravel.UnPause();
                effects.UnPause();
            }
        }

        internal void SelectMusic(string path)
        {
            CancelLoad();
            if (string.IsNullOrEmpty(path))
            {
                PlayMusic(stockTrack, "BLACK ICE");
                return;
            }

            AudioType type = Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".wav" => AudioType.WAV,
                ".ogg" => AudioType.OGGVORBIS,
                ".mp3" => AudioType.MPEG,
                _ => AudioType.UNKNOWN
            };
            if (type == AudioType.UNKNOWN || !File.Exists(path))
            {
                trackLabel = "MUSIC FILE UNAVAILABLE";
                return;
            }

            trackLabel = "LOADING MUSIC";
            loadRoutine = host.StartCoroutine(LoadMusic(path, type));
        }

        private IEnumerator LoadMusic(string path, AudioType type)
        {
            request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, type);
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip != null && clip.loadState != AudioDataLoadState.Failed)
                {
                    PlayMusic(clip, Path.GetFileNameWithoutExtension(path));
                    customTrack = clip;
                }
                else
                {
                    trackLabel = "MUSIC FILE UNREADABLE";
                    Object.Destroy(clip);
                }
            }
            else
            {
                trackLabel = "MUSIC FILE UNREADABLE";
                Debug.LogWarning("Black Ridge could not load the selected music file. " + request.error);
            }

            request.Dispose();
            request = null;
            loadRoutine = null;
        }

        private void PlayMusic(AudioClip clip, string label)
        {
            music.Stop();
            music.clip = clip;
            music.Play();
            trackLabel = label;
            Object.Destroy(customTrack);
            customTrack = null;
        }

        private void CancelLoad()
        {
            if (loadRoutine != null)
            {
                host.StopCoroutine(loadRoutine);
                request.Dispose();
                request = null;
                loadRoutine = null;
            }
        }

        public void Dispose()
        {
            CancelLoad();
            Object.Destroy(customTrack);
        }

        private static AudioSource Source(string name, Transform parent, AudioClip clip, float volume, bool loop)
        {
            AudioSource source = RallyScenery.NewChild(name, parent).AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.clip = clip;
            source.volume = volume;
            source.loop = loop;
            source.spatialBlend = 0f;
            if (loop)
            {
                source.Play();
            }

            return source;
        }

        private static AudioClip Clip(string name, float length, Func<float, float> signal, List<Object> assets)
        {
            int count = Mathf.RoundToInt(length * SampleRate);
            AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
            float[] samples = new float[count];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = signal(i / (float)SampleRate);
            }

            clip.SetData(samples, 0);
            assets.Add(clip);
            return clip;
        }

        private static float EngineSignal(float t)
        {
            float cycle = t * Mathf.PI * 2f;
            float compression = Mathf.Sin(cycle * 79f + Mathf.Sin(cycle * 39f) * 0.35f);
            return compression * 0.46f + Mathf.Sin(cycle * 158f + 0.36f) * 0.16f +
                Mathf.Sin(cycle * 237f + 0.94f) * 0.09f + Mathf.Sin(cycle * 39f) * 0.12f +
                Mathf.Sin(cycle * 431f + Mathf.Sin(cycle * 79f) * 0.67f) * 0.035f;
        }
    }
}
