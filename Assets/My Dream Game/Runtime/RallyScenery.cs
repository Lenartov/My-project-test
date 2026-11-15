using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyDreamGame
{
    internal sealed class RallyScenery
    {
        private const float SectionLength = 44f;
        private static readonly Color NightFog = new Color(0.100f, 0.139f, 0.182f);
        private readonly RallyCourse course;
        private readonly Transform root;
        private readonly List<Object> assets;
        private readonly System.Random random = new System.Random(5637);
        private Material asphalt;
        private Material dirt;
        private Material shale;
        private Material water;
        private Material whitePaint;
        private Material steel;
        private Material bark;
        private Material needles;
        private Material reflector;
        private Material orange;
        private Material dark;
        private Material signFace;

        internal RallyScenery(RallyCourse course, Transform root, List<Object> assets)
        {
            this.course = course;
            this.root = root;
            this.assets = assets;
        }

        internal Color Build()
        {
            CreateOvercastSky();
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.17f, 0.22f, 0.29f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = NightFog;
            RenderSettings.fogDensity = 0.0108f;

            Light moon = NewChild("Overcast moon", root).AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.56f, 0.71f, 0.93f);
            moon.intensity = 0.26f;
            moon.transform.rotation = Quaternion.Euler(24f, 127f, 0f);

            CreateMaterials();
            for (float start = course.Start; start < course.End; start += SectionLength)
            {
                BuildSection(start, Mathf.Min(start + SectionLength, course.End));
            }

            foreach (RallyCourse.Puddle puddle in course.Puddles)
            {
                BuildPuddle(puddle);
            }

            foreach (RallyCourse.Turn turn in course.Turns)
            {
                BuildTurnSigns(turn);
            }

            BuildGallery();
            BuildGate(0f, "BLACK RIDGE", "START", new Color(1f, 0.48f, 0.20f));
            BuildGate(RallyCourse.FirstSplit, "SPLIT 01", "600 M", new Color(0.33f, 0.72f, 1f));
            BuildGate(RallyCourse.SecondSplit, "SPLIT 02", "1 260 M", new Color(0.33f, 0.72f, 1f));
            BuildGate(course.Finish, "FINISH", "STOP AHEAD", new Color(1f, 0.48f, 0.20f));
            return NightFog;
        }

        internal void AnimateWater(float time, float rain)
        {
            water.SetTextureOffset("_BaseMap", new Vector2(time * 0.025f, time * -0.038f));
            water.SetFloat("_BumpScale", Mathf.Lerp(0.12f, 0.37f, rain));
        }

        private void CreateOvercastSky()
        {
            const int size = 32;
            Cubemap sky = Track(new Cubemap(size, TextureFormat.RGBA32, true));
            sky.name = "Black Ridge overcast";
            Color horizon = NightFog;
            Color zenith = new Color(0.085f, 0.126f, 0.187f);
            Color cloud = new Color(0.23f, 0.29f, 0.37f);
            Color ground = new Color(0.037f, 0.045f, 0.05f);
            Color[] pixels = new Color[size * size];
            for (int face = 0; face < 6; face++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float u = 2f * (x + 0.5f) / size - 1f;
                        float v = 2f * (y + 0.5f) / size - 1f;
                        Vector3 ray = CubeDirection((CubemapFace)face, u, v).normalized;
                        if (ray.y < 0f)
                        {
                            pixels[y * size + x] = Color.Lerp(horizon, ground, -ray.y * 2f);
                        }
                        else
                        {
                            float aboveHorizon = Mathf.SmoothStep(0f, 1f, ray.y * 3.7f);
                            float coverage = Mathf.PerlinNoise(ray.x * 3.8f + ray.y * 3.3f + 5f, ray.z * 4.9f + 8f);
                            Color highSky = Color.Lerp(zenith, cloud, Mathf.SmoothStep(0f, 0.64f, coverage));
                            pixels[y * size + x] = Color.Lerp(horizon, highSky, aboveHorizon);
                        }
                    }
                }

                sky.SetPixels(pixels, (CubemapFace)face);
            }

            sky.Apply(true, true);
            Material material = Track(new Material(Shader.Find("Skybox/Cubemap")));
            material.name = "Overcast sky";
            material.SetTexture("_Tex", sky);
            RenderSettings.skybox = material;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = sky;
            RenderSettings.reflectionIntensity = 0.8f;
        }

        private static Vector3 CubeDirection(CubemapFace face, float u, float v)
        {
            return face switch
            {
                CubemapFace.PositiveX => new Vector3(1f, v, -u),
                CubemapFace.NegativeX => new Vector3(-1f, v, u),
                CubemapFace.PositiveY => new Vector3(u, 1f, -v),
                CubemapFace.NegativeY => new Vector3(u, -1f, v),
                CubemapFace.PositiveZ => new Vector3(u, v, 1f),
                _ => new Vector3(-u, v, -1f)
            };
        }

        private void CreateMaterials()
        {
            asphalt = Lit("Damp granite asphalt", new Color(0.77f, 0.82f, 0.89f), 0.78f);
            asphalt.SetTexture("_BaseMap", Resources.Load<Texture2D>("MyDreamGame/Wet Asphalt"));
            asphalt.SetTexture("_BumpMap", MakeRoadNormal());
            asphalt.SetFloat("_BumpScale", 0.47f);
            asphalt.EnableKeyword("_NORMALMAP");
            dirt = Lit("Wet gravel and pine litter", new Color(0.65f, 0.68f, 0.65f), 0.32f);
            dirt.SetTexture("_BaseMap", Resources.Load<Texture2D>("MyDreamGame/Wet Gravel"));
            dirt.SetTexture("_BumpMap", asphalt.GetTexture("_BumpMap"));
            dirt.SetFloat("_BumpScale", 0.62f);
            dirt.EnableKeyword("_NORMALMAP");
            shale = Lit("Fractured greywacke", new Color(0.57f, 0.62f, 0.67f), 0.48f);
            shale.SetTexture("_BaseMap", Resources.Load<Texture2D>("MyDreamGame/Greywacke"));
            water = Lit("Standing water", new Color(0.055f, 0.072f, 0.10f), 0.97f);
            water.SetFloat("_Metallic", 0.48f);
            water.SetTexture("_BumpMap", MakeWaterNormal());
            water.SetFloat("_BumpScale", 0.11f);
            water.EnableKeyword("_NORMALMAP");
            whitePaint = Lit("Worn edge paint", new Color(0.56f, 0.57f, 0.54f), 0.43f);
            steel = Lit("Galvanized barrier", new Color(0.36f, 0.40f, 0.42f), 0.67f);
            steel.SetFloat("_Metallic", 0.54f);
            bark = Lit("Pine bark", new Color(0.19f, 0.15f, 0.115f), 0.32f);
            needles = Lit("Rain-dark needles", new Color(0.081f, 0.15f, 0.13f), 0.32f);
            needles.SetTexture("_BaseMap", MakeSurface("Pine needles", new Color(0.31f, 0.47f, 0.42f), new Color(0.94f, 1f, 0.89f), 14f));
            reflector = Lit("Cat's eye", new Color(0.88f, 0.94f, 0.92f), 0.52f, new Color(0.37f, 0.54f, 0.58f));
            orange = Lit("Amber reflector", new Color(1f, 0.48f, 0.10f), 0.58f, new Color(0.84f, 0.27f, 0.04f));
            dark = Lit("Powder coated steel", new Color(0.075f, 0.095f, 0.12f), 0.36f);
            signFace = Lit("Road sign enamel", new Color(0.12f, 0.16f, 0.19f), 0.40f, new Color(0.025f, 0.04f, 0.06f));
        }

        private void BuildSection(float start, float end)
        {
            MeshDraft road = new MeshDraft();
            MeshDraft shoulder = new MeshDraft();
            MeshDraft bank = new MeshDraft();
            MeshDraft striping = new MeshDraft();
            MeshDraft rails = new MeshDraft();
            MeshDraft wood = new MeshDraft();
            MeshDraft foliage = new MeshDraft();
            MeshDraft marker = new MeshDraft();
            MeshDraft stones = new MeshDraft();
            float w = RallyCourse.RoadHalfWidth;
            AddRibbon(road, start, end, new[] { -w, -w * 0.5f, 0f, w * 0.5f, w }, SurfaceHeight, 3f);
            AddRibbon(shoulder, start, end, new[] { -8f, -6.5f, -RallyCourse.BarrierOffset, -w }, SurfaceHeight, 2f);
            AddRibbon(shoulder, start, end, new[] { w, RallyCourse.BarrierOffset, 6.5f, 8f }, SurfaceHeight, 2f);
            AddRibbon(bank, start, end, new[] { -21f, -17f, -13f, -10f, -8f }, SurfaceHeight, 3.8f);
            AddRibbon(bank, start, end, new[] { 8f, 10f, 13f, 17f, 21f }, SurfaceHeight, 3.8f);
            AddRibbon(striping, start, end, new[] { -w + 0.09f, -w + 0.18f }, PaintHeight, 1f);
            AddRibbon(striping, start, end, new[] { w - 0.18f, w - 0.09f }, PaintHeight, 1f);
            AddGuardrail(rails, start, end, -1);
            AddGuardrail(rails, start, end, 1);

            for (float distance = start + 2f; distance < end; distance += 4.4f)
            {
                RallyCourse.Frame frame = course.At(distance);
                Quaternion rotation = Quaternion.Euler(0f, frame.Heading * Mathf.Rad2Deg, 0f);
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * (RallyCourse.BarrierOffset + 0.14f);
                    rails.Box(frame.Point(x, 0.34f), rotation, new Vector3(0.13f, 0.82f, 0.17f));
                    marker.Box(frame.Point(x - side * 0.11f, 0.70f), rotation, new Vector3(0.075f, 0.095f, 0.105f));
                    for (int row = 0; row < 3; row++)
                    {
                        float s = Mathf.Min(end, distance + Range(-1.8f, 1.8f));
                        float lateral = side * (8.4f + row * 4.7f + Range(-0.35f, 1.45f));
                        Vector3 treeBase = course.At(s).Point(lateral, SurfaceHeight(s, lateral) - 0.25f);
                        BuildPine(wood, foliage, treeBase, Range(5.5f, 14.2f), Range(0.8f, 1.4f));
                    }

                    if (random.NextDouble() < 0.53)
                    {
                        float s = Mathf.Min(end, distance + Range(-1.8f, 1.8f));
                        float lateral = side * Range(6.7f, 11f);
                        Vector3 point = course.At(s).Point(lateral, SurfaceHeight(s, lateral) + 0.09f);
                        BuildRock(stones, point, new Vector3(Range(0.6f, 2.2f), Range(0.4f, 1.7f), Range(0.55f, 2.8f)));
                    }
                }
            }

            Transform section = NewChild("Road section " + Mathf.RoundToInt(start), root).transform;
            Finish(road, "Asphalt", section, asphalt);
            Finish(shoulder, "Gravel shoulder", section, dirt);
            Finish(bank, "Hillside", section, shale);
            Finish(striping, "Road edges", section, whitePaint, false);
            Finish(rails, "Guardrail", section, steel);
            Finish(marker, "Guardrail reflectors", section, reflector, false);
            Finish(wood, "Pine trunks", section, bark);
            Finish(foliage, "Pine canopy", section, needles);
            if (stones.Count > 0)
            {
                Finish(stones, "Scree", section, shale);
            }
        }

        private float PaintHeight(float s, float x)
        {
            return SurfaceHeight(s, x) + 0.014f;
        }

        internal static float SurfaceHeight(float s, float x)
        {
            float edge = Mathf.Abs(x) - RallyCourse.RoadHalfWidth;
            if (edge <= 0f)
            {
                return 0f;
            }

            if (edge < 1.75f)
            {
                return -edge * 0.055f;
            }

            float side = Mathf.Sign(x);
            float run = edge - 1.75f;
            float grade = side * (0.38f + 0.23f * Mathf.Sin(s / 75f)) + 0.14f * Mathf.Sin(s / 49f);
            float texture = (Mathf.PerlinNoise(s * 0.115f, x * 0.27f + 13f) - 0.5f) * Mathf.Min(run, 2.3f);
            return -0.09625f + grade * run + texture;
        }

        private void AddRibbon(MeshDraft draft, float start, float end, float[] offsets,
            System.Func<float, float, float> height, float uvScale)
        {
            int steps = Mathf.CeilToInt((end - start) / 2f);
            int first = draft.Count;
            for (int i = 0; i <= steps; i++)
            {
                float s = Mathf.Lerp(start, end, i / (float)steps);
                RallyCourse.Frame frame = course.At(s);
                for (int lane = 0; lane < offsets.Length; lane++)
                {
                    float x = offsets[lane];
                    draft.Vertex(frame.Point(x, height(s, x)), new Vector2(x / uvScale, s / uvScale));
                    if (i > 0 && lane > 0)
                    {
                        int current = first + i * offsets.Length + lane;
                        draft.Quad(current - offsets.Length - 1, current - 1, current, current - offsets.Length);
                    }
                }
            }
        }

        private void AddGuardrail(MeshDraft draft, float start, float end, int side)
        {
            float[] profile = { 0.07f, -0.03f, 0.025f, -0.03f, 0.06f };
            int first = draft.Count;
            int steps = Mathf.CeilToInt((end - start) / 2f);
            for (int i = 0; i <= steps; i++)
            {
                float s = Mathf.Lerp(start, end, i / (float)steps);
                RallyCourse.Frame frame = course.At(s);
                for (int j = 0; j < profile.Length; j++)
                {
                    draft.Vertex(frame.Point(side * (RallyCourse.BarrierOffset + profile[j]), 0.38f + j * 0.092f),
                        new Vector2(s / 3f, j * 0.25f));
                    if (i > 0 && j > 0)
                    {
                        int index = first + i * profile.Length + j;
                        if (side < 0)
                        {
                            draft.Quad(index - profile.Length - 1, index - profile.Length, index, index - 1);
                        }
                        else
                        {
                            draft.Quad(index - profile.Length - 1, index - 1, index, index - profile.Length);
                        }
                    }
                }
            }
        }

        private void BuildPuddle(RallyCourse.Puddle puddle)
        {
            MeshDraft draft = new MeshDraft();
            int steps = Mathf.CeilToInt(puddle.HalfLength * 2f);
            for (int i = 0; i <= steps; i++)
            {
                float t = Mathf.Lerp(-0.999f, 0.999f, i / (float)steps);
                float s = puddle.Distance + t * puddle.HalfLength;
                float width = puddle.WidthAt(s);
                RallyCourse.Frame frame = course.At(s);
                for (int lane = -1; lane <= 1; lane++)
                {
                    float x = puddle.Lateral + lane * width;
                    draft.Vertex(frame.Point(x, 0.019f), new Vector2(x * 0.63f, s * 0.63f));
                    if (i > 0 && lane > -1)
                    {
                        int index = draft.Count - 1;
                        draft.Quad(index - 4, index - 1, index, index - 3);
                    }
                }
            }

            Finish(draft, "Standing water", root, water, false);
        }

        private void BuildGallery()
        {
            Transform gallery = NewChild("Rockfall shelter", root).transform;
            Material concrete = Lit("Water-stained concrete", new Color(0.44f, 0.47f, 0.47f), 0.43f);
            concrete.SetTexture("_BaseMap", Resources.Load<Texture2D>("MyDreamGame/Greywacke"));
            Material lamps = Lit("Gallery luminaire", new Color(0.87f, 0.72f, 0.43f), 0.42f, new Color(0.88f, 0.52f, 0.15f));
            MeshDraft structure = new MeshDraft();
            MeshDraft fixtures = new MeshDraft();
            const float span = 6f;
            for (float start = RallyCourse.GalleryStart; start < RallyCourse.GalleryEnd; start += span)
            {
                float middle = start + span * 0.5f;
                RallyCourse.Frame frame = course.At(middle);
                Vector3 incline = course.At(start + span).Center - course.At(start).Center;
                Quaternion roofRotation = Quaternion.LookRotation(incline, Vector3.up);
                Quaternion upright = Quaternion.Euler(0f, frame.Heading * Mathf.Rad2Deg, 0f);
                structure.Box(frame.Point(0f, 4.56f), roofRotation, new Vector3(10.8f, 0.37f, span + 0.06f));
                structure.Box(frame.Point(5.18f, 2.19f), roofRotation, new Vector3(0.38f, 4.58f, span + 0.05f));
                structure.Box(frame.Point(-5.08f, 2.19f), upright, new Vector3(0.37f, 4.59f, 0.43f));
                structure.Box(frame.Point(0f, 4.32f), roofRotation, new Vector3(10.45f, 0.21f, 0.34f));
                if (Mathf.RoundToInt((start - RallyCourse.GalleryStart) / span) % 3 == 1)
                {
                    Vector3 lightPosition = frame.Point(3.74f, 4.26f);
                    fixtures.Box(lightPosition, roofRotation, new Vector3(0.21f, 0.06f, 0.96f));
                    Light light = NewChild("Warm gallery light", gallery).AddComponent<Light>();
                    light.transform.position = lightPosition - Vector3.up * 0.2f;
                    light.type = LightType.Point;
                    light.color = new Color(1f, 0.79f, 0.50f);
                    light.range = 14f;
                    light.intensity = 25f;
                }
            }

            Finish(structure, "Roof, wall and piers", gallery, concrete);
            Finish(fixtures, "Amber fixtures", gallery, lamps, false);
            AudioReverbZone echo = NewChild("Gallery acoustics", gallery).AddComponent<AudioReverbZone>();
            echo.transform.position = course.At((RallyCourse.GalleryStart + RallyCourse.GalleryEnd) * 0.5f).Center + Vector3.up * 1.2f;
            echo.minDistance = 16f;
            echo.maxDistance = 24f;
            echo.reverbPreset = AudioReverbPreset.StoneCorridor;
        }

        private void BuildTurnSigns(RallyCourse.Turn turn)
        {
            float warning = turn.Start - 31f;
            RallyCourse.Frame frame = course.At(warning);
            Quaternion rotation = Quaternion.Euler(0f, frame.Heading * Mathf.Rad2Deg, 0f);
            Vector3 point = frame.Point(4.30f, 0f);
            Transform sign = NewChild("Advisory speed", root).transform;
            sign.SetPositionAndRotation(point, rotation);
            Box("Post", sign, new Vector3(0f, 0.94f, 0f), new Vector3(0.08f, 2.12f, 0.09f), steel);
            Box("Board edge", sign, new Vector3(0f, 1.72f, 0f), new Vector3(0.88f, 0.92f, 0.10f), orange);
            Box("Board", sign, new Vector3(0f, 1.72f, -0.055f), new Vector3(0.79f, 0.83f, 0.025f), signFace);
            WorldText(sign, turn.Speed.ToString(), new Vector3(0f, 1.68f, -0.075f), 4f, new Color(1f, 0.79f, 0.49f));
            for (int i = 0; i < 3; i++)
            {
                float s = Mathf.Lerp(turn.Start + 12f, turn.End - 15f, i * 0.5f);
                RallyCourse.Frame curve = course.At(s);
                Quaternion curveRotation = Quaternion.Euler(0f, curve.Heading * Mathf.Rad2Deg - turn.Direction * 20f, 0f);
                Transform board = NewChild("Reflective chevron", root).transform;
                board.SetPositionAndRotation(curve.Point(-turn.Direction * 4.62f, 1.16f), curveRotation);
                Box("Backplate", board, Vector3.zero, new Vector3(0.91f, 0.47f, 0.07f), signFace);
                MeshDraft chevron = new MeshDraft();
                chevron.Box(new Vector3(0f, 0.083f, -0.048f), Quaternion.Euler(0f, 0f, -turn.Direction * 36f),
                    new Vector3(0.33f, 0.083f, 0.020f));
                chevron.Box(new Vector3(0f, -0.083f, -0.048f), Quaternion.Euler(0f, 0f, turn.Direction * 36f),
                    new Vector3(0.33f, 0.083f, 0.020f));
                Finish(chevron, "Reflective arrow", board, orange, false);
            }
        }

        private void BuildGate(float distance, string title, string caption, Color lightColor)
        {
            RallyCourse.Frame frame = course.At(distance);
            Transform gate = NewChild(title, root).transform;
            gate.SetPositionAndRotation(frame.Center, Quaternion.Euler(0f, frame.Heading * Mathf.Rad2Deg, 0f));
            Box("Crossbar", gate, new Vector3(0f, 4.1f, 0f), new Vector3(8.84f, 0.78f, 0.34f), dark);
            WorldText(gate, title, new Vector3(0f, 4.21f, -0.19f), 4.2f, new Color(0.86f, 0.91f, 0.94f));
            WorldText(gate, caption, new Vector3(0f, 3.92f, -0.19f), 1.55f, lightColor);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Support", gate, new Vector3(side * 4.28f, 2.05f, 0f), new Vector3(0.21f, 4.1f, 0.31f), steel);
                Material lens = lightColor.r > 0.7f ? orange : reflector;
                Box("Timing lamp", gate, new Vector3(side * 3.90f, 3.80f, -0.16f), new Vector3(0.37f, 0.15f, 0.24f), lens);
                Light lamp = NewChild("Timing lamp spill", gate).AddComponent<Light>();
                lamp.transform.localPosition = new Vector3(side * 3.8f, 3.6f, -0.65f);
                lamp.type = LightType.Point;
                lamp.range = 13f;
                lamp.intensity = 14f;
                lamp.color = lightColor;
            }

            MeshDraft line = new MeshDraft();
            AddRibbon(line, distance, distance + 0.33f, new[] { -3.04f, 3.04f }, PaintHeight, 1f);
            Finish(line, "Timing line", root, whitePaint, false);
        }

        private static void WorldText(Transform parent, string label, Vector3 position, float size, Color color)
        {
            TextMeshPro text = NewChild(label, parent).AddComponent<TextMeshPro>();
            text.transform.localPosition = position;
            text.rectTransform.sizeDelta = new Vector2(9f, 1f);
            text.font = TMP_Settings.defaultFontAsset;
            text.text = label;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
        }

        private void BuildPine(MeshDraft trunk, MeshDraft canopy, Vector3 point, float height, float width)
        {
            const int sides = 8;
            float phase = Range(0f, Mathf.PI * 2f);
            trunk.Ring(point, height * 0.92f, height * 0.016f, height * 0.002f, sides, phase);
            for (int branch = 0; branch < 13; branch++)
            {
                float t = branch / 12f;
                float y = height * Mathf.Lerp(0.18f, 0.94f, t);
                float radius = height * 0.17f * width * Mathf.Lerp(1f, 0.13f, t) * Range(0.84f, 1.16f);
                float layerHeight = height * Mathf.Lerp(0.26f, 0.094f, t);
                Vector3 center = point + new Vector3(Range(-0.13f, 0.13f), y, Range(-0.13f, 0.13f));
                int first = canopy.Count;
                for (int side = 0; side <= sides; side++)
                {
                    float angle = phase + (side % sides) * (2f * Mathf.PI / sides);
                    float variation = 0.83f + 0.17f * Mathf.Sin(angle * 3f + branch * 2f);
                    Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    canopy.Vertex(center + direction * (radius * 0.53f) - Vector3.up * (0.09f * layerHeight),
                        new Vector2(side * 0.25f, t * 4f));
                    canopy.Vertex(center + direction * (radius * variation) + Vector3.up * (layerHeight * 0.12f * Mathf.Sin(angle * 4f)),
                        new Vector2(side * 0.25f, t * 4f + 0.5f));
                    canopy.Vertex(center + direction * (radius * 0.10f) + Vector3.up * layerHeight,
                        new Vector2(side * 0.25f, t * 4f + 1f));
                    if (side > 0)
                    {
                        int at = first + side * 3;
                        canopy.Quad(at - 3, at - 2, at + 1, at);
                        canopy.Quad(at - 2, at - 1, at + 2, at + 1);
                    }
                }

                phase += 0.34f;
            }
        }

        private void BuildRock(MeshDraft draft, Vector3 point, Vector3 scale)
        {
            const int sides = 7;
            float phase = Range(0f, Mathf.PI * 2f);
            Vector3[] edge = new Vector3[sides];
            Vector3[] top = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float angle = phase + i * 2f * Mathf.PI / sides;
                Vector3 radial = new Vector3(Mathf.Cos(angle) * scale.x, 0f, Mathf.Sin(angle) * scale.z);
                edge[i] = point + radial * Range(0.68f, 1.03f) - Vector3.up * (0.39f * scale.y);
                top[i] = point + radial * Range(0.33f, 0.77f) + Vector3.up * (scale.y * Range(0.29f, 0.83f));
            }

            Vector3 peak = point + new Vector3(scale.x * 0.12f, scale.y, -scale.z * 0.1f);
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                draft.Face(edge[i], top[i], top[next], edge[next]);
                draft.Face(top[i], peak, top[next]);
            }
        }

        private Texture2D MakeSurface(string name, Color low, Color high, float frequency)
        {
            const int size = 128;
            Texture2D texture = Track(new Texture2D(size, size, TextureFormat.RGB24, true));
            texture.name = name;
            texture.anisoLevel = 4;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float value = Mathf.PerlinNoise(x * frequency / size, y * frequency / size) * 0.75f +
                        Range(0f, 0.25f);
                    pixels[y * size + x] = Color.Lerp(low, high, value);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private Texture2D MakeRoadNormal()
        {
            const int size = 128;
            float[] height = new float[size * size];
            for (int i = 0; i < height.Length; i++)
            {
                height[i] = Range(-0.19f, 0.19f);
            }

            return MakeNormal("Wet road microsurface", height, size);
        }

        private Texture2D MakeWaterNormal()
        {
            const int size = 128;
            Vector2[] origins = new Vector2[15];
            for (int i = 0; i < origins.Length; i++)
            {
                origins[i] = new Vector2(Range(0f, 1f), Range(0f, 1f));
            }

            float[] height = new float[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float ripple = 0f;
                    foreach (Vector2 origin in origins)
                    {
                        float u = Mathf.Abs(origin.x - x / (float)size);
                        float v = Mathf.Abs(origin.y - y / (float)size);
                        u = Mathf.Min(u, 1f - u);
                        v = Mathf.Min(v, 1f - v);
                        float radius = Mathf.Sqrt(u * u + v * v);
                        if (radius < 0.19f)
                        {
                            ripple += Mathf.Sin(radius * 138f) * Mathf.SmoothStep(0f, 1f, radius * 65f) *
                                (1f - Mathf.SmoothStep(0f, 1f, radius / 0.19f));
                        }
                    }

                    height[y * size + x] = ripple * 0.13f;
                }
            }

            return MakeNormal("Rain rings", height, size);
        }

        private Texture2D MakeNormal(string name, float[] height, int size)
        {
            Texture2D texture = Track(new Texture2D(size, size, TextureFormat.RGBA32, true, true));
            texture.name = name;
            texture.anisoLevel = 8;
            Color[] pixels = new Color[height.Length];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = height[y * size + ((x + 1) % size)] - height[y * size + ((x + size - 1) % size)];
                    float dy = height[((y + 1) % size) * size + x] - height[((y + size - 1) % size) * size + x];
                    Vector3 n = new Vector3(dx, dy, 1f).normalized;
                    pixels[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        internal Material Lit(string name, Color color, float smoothness, Color emission = default)
        {
            Material material = Track(new Material(Shader.Find("Universal Render Pipeline/Lit")));
            material.name = name;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            if (emission != default)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
            }

            return material;
        }

        internal void Box(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            MeshDraft draft = new MeshDraft();
            draft.Box(position, Quaternion.identity, size);
            Finish(draft, name, parent, material);
        }

        internal void Finish(MeshDraft draft, string name, Transform parent, Material material, bool castsShadows = true)
        {
            GameObject child = NewChild(name, parent);
            Mesh mesh = Track(draft.ToMesh(name));
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castsShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.lightProbeUsage = LightProbeUsage.Off;
        }

        internal static GameObject NewChild(string name, Transform parent)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private T Track<T>(T asset) where T : Object
        {
            assets.Add(asset);
            return asset;
        }

        private float Range(float minimum, float maximum)
        {
            return Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
        }
    }

    internal sealed class MeshDraft
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uv = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();

        internal int Count => vertices.Count;

        internal void Vertex(Vector3 vertex, Vector2 texcoord)
        {
            vertices.Add(vertex);
            uv.Add(texcoord);
        }

        internal void Quad(int a, int b, int c, int d)
        {
            Triangle(a, b, c);
            Triangle(a, c, d);
        }

        private void Triangle(int a, int b, int c)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        internal void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int first = Count;
            Vertex(a, Vector2.zero);
            Vertex(b, Vector2.up);
            Vertex(c, Vector2.one);
            Vertex(d, Vector2.right);
            Quad(first, first + 1, first + 2, first + 3);
        }

        internal void Face(Vector3 a, Vector3 b, Vector3 c)
        {
            int first = Count;
            Vertex(a, Vector2.zero);
            Vertex(b, Vector2.up);
            Vertex(c, Vector2.one);
            Triangle(first, first + 1, first + 2);
        }

        internal void Box(Vector3 center, Quaternion rotation, Vector3 size)
        {
            Vector3 x = rotation * Vector3.right * (size.x * 0.5f);
            Vector3 y = rotation * Vector3.up * (size.y * 0.5f);
            Vector3 z = rotation * Vector3.forward * (size.z * 0.5f);
            Vector3 a = center - x - y - z;
            Vector3 b = center + x - y - z;
            Vector3 c = center + x + y - z;
            Vector3 d = center - x + y - z;
            Vector3 e = a + 2f * z;
            Vector3 f = b + 2f * z;
            Vector3 g = c + 2f * z;
            Vector3 h = d + 2f * z;
            Face(a, d, c, b);
            Face(e, f, g, h);
            Face(a, e, h, d);
            Face(b, c, g, f);
            Face(d, h, g, c);
            Face(a, b, f, e);
        }

        internal void Ring(Vector3 point, float height, float lower, float upper, int sides, float phase)
        {
            int first = Count;
            for (int i = 0; i <= sides; i++)
            {
                float angle = phase + i * 2f * Mathf.PI / sides;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vertex(point + radial * lower, new Vector2(i / (float)sides, 0f));
                Vertex(point + radial * upper + Vector3.up * height, new Vector2(i / (float)sides, 1f));
                if (i > 0)
                {
                    int at = first + i * 2;
                    Quad(at - 2, at - 1, at + 1, at);
                }
            }
        }

        internal Mesh ToMesh(string name)
        {
            Mesh mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
