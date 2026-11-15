using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyDreamGame
{
    internal sealed class RallyView
    {
        private readonly Camera camera;
        private readonly RallyScenery scenery;
        private readonly Transform chassis;
        private readonly ParticleSystem rain;
        private readonly ParticleSystem spray;
        private readonly System.Random random = new System.Random(8701);
        private float shock;
        private float roll;
        private float pitch;
        private float heave;
        private float leftSpray;
        private float rightSpray;

        internal RallyView(Transform root, RallyScenery scenery, Color fog, List<Object> assets)
        {
            this.scenery = scenery;
            chassis = RallyScenery.NewChild("Rally car", root).transform;
            GameObject eye = RallyScenery.NewChild("Bonnet camera", chassis);
            camera = eye.AddComponent<Camera>();
            eye.AddComponent<AudioListener>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = fog;
            camera.allowHDR = true;
            camera.fieldOfView = 65f;
            camera.nearClipPlane = 0.055f;
            camera.farClipPlane = 220f;
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            Volume volume = RallyScenery.NewChild("Night grading", root).AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 20f;
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            assets.Add(profile);
            volume.profile = profile;
            Tonemapping tone = profile.Add<Tonemapping>();
            tone.mode.Override(TonemappingMode.Neutral);
            Bloom bloom = profile.Add<Bloom>();
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.22f);
            bloom.scatter.Override(0.58f);
            Vignette vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.20f);
            vignette.smoothness.Override(0.44f);
            assets.Add(tone);
            assets.Add(bloom);
            assets.Add(vignette);

            CreateBonnet(scenery);
            CreateLamp(-1f);
            CreateLamp(1f);
            rain = CreateRain(eye.transform, assets);
            spray = CreateSpray(chassis, rain.GetComponent<ParticleSystemRenderer>().sharedMaterial);
        }

        internal void Reset(RallyCar car, RallyCourse course)
        {
            roll = 0f;
            pitch = 0f;
            heave = 0f;
            shock = 0f;
            leftSpray = 0f;
            rightSpray = 0f;
            spray.Clear();
            Update(car, course, 0f, 1f, false);
        }

        internal void Impact(float speed)
        {
            shock = Mathf.Max(shock, Mathf.Min(1f, speed * 0.12f));
        }

        internal void Update(RallyCar car, RallyCourse course, float time, float dt, bool driving)
        {
            RallyCourse.Frame frame = course.At(car.Distance);
            float grade = (course.At(car.Distance + 2f).Center.y - course.At(car.Distance - 2f).Center.y) * 0.25f;
            float relative = car.Heading - frame.Heading;
            float targetPitch = -(grade * Mathf.Cos(relative) + frame.Bank * Mathf.Sin(relative)) * Mathf.Rad2Deg;
            float targetRoll = (frame.Bank * Mathf.Cos(relative) - grade * Mathf.Sin(relative)) * Mathf.Rad2Deg;
            pitch = Mathf.Lerp(pitch, targetPitch - car.Acceleration * 0.30f, 1f - Mathf.Exp(-9f * dt));
            roll = Mathf.Lerp(roll, targetRoll - car.Steering * Mathf.Min(car.Speed, 19f) * 0.6f, 1f - Mathf.Exp(-8f * dt));
            float roughness = Mathf.InverseLerp(0.77f, 0.45f, car.Grip) * Mathf.InverseLerp(2f, 16f, car.Speed);
            heave = Mathf.Lerp(heave, Mathf.Sin(time * 63f) * roughness * 0.022f, 1f - Mathf.Exp(-13f * dt));
            shock = Mathf.MoveTowards(shock, 0f, dt * 1.9f);
            chassis.SetPositionAndRotation(car.Position, Quaternion.Euler(pitch, car.Heading * Mathf.Rad2Deg, roll));
            camera.transform.localPosition = new Vector3(-0.08f + Mathf.Sin(time * 85f) * shock * 0.03f,
                1.28f + heave + Mathf.Sin(time * 72f) * shock * 0.04f, 0.21f);
            camera.transform.localRotation = Quaternion.Euler(4.0f + Mathf.Sin(time * 49f) * shock * 1.5f, 0f, shock * Mathf.Sin(time * 35f));

            float rainStrength = RallyCourse.RainAt(car.Distance);
            float cover = RallyCourse.GalleryCover(car.Distance);
            RenderSettings.fogDensity = Mathf.Lerp(0.0074f, 0.014f, rainStrength);
            ParticleSystem.EmissionModule emission = rain.emission;
            emission.rateOverTime = Mathf.Lerp(590f, 1520f, rainStrength) * Mathf.Lerp(1f, 0.15f, cover);
            scenery.AnimateWater(time, rainStrength);
            if (driving)
            {
                EmitSpray(-1f, car.LeftWater, car.Speed, dt, ref leftSpray);
                EmitSpray(1f, car.RightWater, car.Speed, dt, ref rightSpray);
            }
        }

        internal void SetPaused(bool paused)
        {
            if (paused)
            {
                rain.Pause();
                spray.Pause();
            }
            else
            {
                rain.Play();
                spray.Play();
            }
        }

        private void CreateBonnet(RallyScenery scenery)
        {
            Material paint = scenery.Lit("Deep graphite bodywork", new Color(0.085f, 0.12f, 0.16f), 0.72f);
            paint.SetFloat("_Metallic", 0.34f);
            Material rubber = scenery.Lit("Windscreen and bonnet trim", new Color(0.045f, 0.055f, 0.062f), 0.32f);
            MeshDraft bonnet = new MeshDraft();
            float[] widths = { 0.88f, 0.89f, 0.87f, 0.80f };
            float[] heights = { 0.84f, 0.82f, 0.76f, 0.65f };
            float[] depths = { 0.36f, 0.85f, 1.50f, 2.12f };
            for (int row = 0; row < widths.Length; row++)
            {
                for (int col = 0; col < 5; col++)
                {
                    float x = (col - 2f) * 0.5f;
                    float crown = (1f - x * x) * 0.042f;
                    bonnet.Vertex(new Vector3(widths[row] * x, heights[row] + crown, depths[row]), new Vector2(col * 0.25f, row / 3f));
                    if (col > 0 && row > 0)
                    {
                        int i = row * 5 + col;
                        bonnet.Quad(i - 6, i - 1, i, i - 5);
                    }
                }
            }

            scenery.Finish(bonnet, "Bonnet", chassis, paint, false);
            scenery.Box("Rear bonnet seam", chassis, new Vector3(0f, 0.844f, 0.48f), new Vector3(1.69f, 0.012f, 0.027f), rubber);
            scenery.Box("Intake", chassis, new Vector3(0f, 0.807f, 1.53f), new Vector3(0.56f, 0.028f, 0.31f), rubber);
            scenery.Box("Front lip", chassis, new Vector3(0f, 0.634f, 2.12f), new Vector3(1.57f, 0.065f, 0.042f), rubber);
        }

        private void CreateLamp(float side)
        {
            Light light = RallyScenery.NewChild("Dipped headlight", chassis).AddComponent<Light>();
            light.transform.localPosition = new Vector3(side * 0.64f, 0.68f, 2.06f);
            light.transform.localRotation = Quaternion.Euler(3.2f, side * 3f, 0f);
            light.type = LightType.Spot;
            light.range = 102f;
            light.spotAngle = 62f;
            light.innerSpotAngle = 39f;
            light.intensity = 280f;
            light.color = new Color(1f, 0.87f, 0.69f);
        }

        private static ParticleSystem CreateRain(Transform eye, List<Object> assets)
        {
            ParticleSystem particles = RallyScenery.NewChild("Rain in the headlights", eye).AddComponent<ParticleSystem>();
            particles.transform.localPosition = new Vector3(0f, 2.8f, 10f);
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.48f, 0.77f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.011f, 0.031f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.48f, 0.60f, 0.69f, 0.06f), new Color(0.76f, 0.82f, 0.89f, 0.34f));
            main.maxParticles = 1080;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 1320f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(21f, 7.0f, 19f);
            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(-2.0f, -1.2f);
            velocity.y = new ParticleSystem.MinMaxCurve(-11f, -9f);
            velocity.z = new ParticleSystem.MinMaxCurve(-5.2f, -4f);

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 4.5f;
            renderer.velocityScale = 0.027f;
            renderer.maxParticleSize = 0.12f;
            Material material = new Material(Shader.Find("MyDreamGame/Rain"));
            assets.Add(material);
            renderer.sharedMaterial = material;
            particles.Play();
            return particles;
        }

        private static ParticleSystem CreateSpray(Transform chassis, Material material)
        {
            ParticleSystem particles = RallyScenery.NewChild("Tyre spray", chassis).AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 260;
            main.gravityModifier = 1.12f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.48f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.016f, 0.048f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.52f, 0.64f, 0.74f, 0.15f), new Color(0.74f, 0.84f, 0.89f, 0.49f));
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2f;
            renderer.velocityScale = 0.024f;
            renderer.sharedMaterial = material;
            particles.Play();
            return particles;
        }

        private void EmitSpray(float side, float water, float speed, float dt, ref float pending)
        {
            pending += water * Mathf.InverseLerp(4f, 25f, speed) * 152f * dt;
            int count = (int)pending;
            pending -= count;
            for (int i = 0; i < count; i++)
            {
                float scatter = (float)random.NextDouble();
                ParticleSystem.EmitParams drop = new ParticleSystem.EmitParams
                {
                    position = chassis.TransformPoint(new Vector3(side * 0.78f, 0.11f, 1.22f)),
                    velocity = chassis.TransformDirection(new Vector3(side * (0.35f + 2.25f * scatter),
                        1.8f + (float)random.NextDouble() * 4.6f, speed * (0.12f + scatter * 0.25f)))
                };
                spray.Emit(drop, 1);
            }
        }
    }
}
