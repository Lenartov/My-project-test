using System.Collections.Generic;
using UnityEngine;

namespace MyDreamGame
{
    internal sealed class RallyGhost
    {
        private readonly Transform model;
        private readonly Material[] materials;

        internal RallyGhost(Transform parent, RallyScenery scenery, List<Object> assets)
        {
            model = RallyScenery.NewChild("Reference car", parent).transform;
            Shader shader = Shader.Find("MyDreamGame/ReferenceCar");
            Material bodyPaint = MakeMaterial(shader, new Color(0.42f, 0.70f, 0.83f), assets);
            Material windows = MakeMaterial(shader, new Color(0.13f, 0.27f, 0.33f), assets);
            Material tires = MakeMaterial(shader, new Color(0.11f, 0.18f, 0.23f), assets);
            Material tailLamps = MakeMaterial(shader, new Color(0.93f, 0.45f, 0.36f), assets);
            materials = new[] { bodyPaint, windows, tires, tailLamps };
            CreateModel(scenery, bodyPaint, windows, tires, tailLamps);
            Hide();
        }

        internal void Show(RallyRecording recording, RallyCourse course, float time, Vector3 driverPosition)
        {
            RallyRecording.Pose pose = recording.PoseAtTime(time);
            RallyCourse.Frame frame = course.At(pose.Distance);
            Vector3 position = frame.Point(pose.Lateral);
            float gap = (driverPosition - position).magnitude;
            float opacity = 0.49f * Mathf.SmoothStep(0f, 1f, (gap - 3.3f) / 5.5f) *
                (1f - Mathf.SmoothStep(0f, 1f, (gap - 82f) / 42f));
            if (opacity < 0.01f)
            {
                Hide();
                return;
            }

            float grade = (course.At(pose.Distance + 2f).Center.y - course.At(pose.Distance - 2f).Center.y) * 0.25f;
            float relative = pose.Heading - frame.Heading;
            float pitch = -(grade * Mathf.Cos(relative) + frame.Bank * Mathf.Sin(relative)) * Mathf.Rad2Deg;
            float roll = (frame.Bank * Mathf.Cos(relative) - grade * Mathf.Sin(relative)) * Mathf.Rad2Deg;
            model.SetPositionAndRotation(position, Quaternion.Euler(pitch, pose.Heading * Mathf.Rad2Deg, roll));
            foreach (Material material in materials)
            {
                material.SetFloat("_Opacity", opacity);
            }

            model.gameObject.SetActive(true);
        }

        internal void Hide()
        {
            model.gameObject.SetActive(false);
        }

        private void CreateModel(RallyScenery scenery, Material bodyPaint, Material windows, Material tires, Material tailLamps)
        {
            MeshDraft shell = new MeshDraft();
            Vector3 rearLeft = new Vector3(-0.74f, 0.70f, -1.82f);
            Vector3 rearRight = new Vector3(0.74f, 0.70f, -1.82f);
            Vector3 frontLeft = new Vector3(-0.78f, 0.68f, 1.88f);
            Vector3 frontRight = new Vector3(0.78f, 0.68f, 1.88f);
            Vector3[] shoulder = { rearLeft, rearRight, frontRight, frontLeft };
            Vector3[] sill =
            {
                new Vector3(-0.79f, 0.35f, -1.82f), new Vector3(0.79f, 0.35f, -1.82f),
                new Vector3(0.78f, 0.29f, 1.88f), new Vector3(-0.78f, 0.29f, 1.88f)
            };
            shell.Face(rearLeft, frontLeft, frontRight, rearRight);
            for (int i = 0; i < sill.Length; i++)
            {
                int next = (i + 1) % sill.Length;
                shell.Face(sill[i], shoulder[i], shoulder[next], sill[next]);
            }

            Vector3 rearDeck = new Vector3(0.69f, 0.70f, -1.51f);
            Vector3 rearRoof = new Vector3(0.59f, 1.24f, -1.15f);
            Vector3 frontRoof = new Vector3(0.57f, 1.27f, 0.15f);
            Vector3 cowl = new Vector3(0.69f, 0.70f, 0.79f);
            Vector3 leftRearDeck = Mirror(rearDeck);
            Vector3 leftRearRoof = Mirror(rearRoof);
            Vector3 leftFrontRoof = Mirror(frontRoof);
            Vector3 leftCowl = Mirror(cowl);
            shell.Face(leftRearRoof, leftFrontRoof, frontRoof, rearRoof);
            shell.Box(new Vector3(0f, 1.18f, -1.48f), Quaternion.identity, new Vector3(1.29f, 0.045f, 0.26f));
            shell.Box(new Vector3(0f, 0.39f, 1.88f), Quaternion.identity, new Vector3(1.57f, 0.12f, 0.09f));
            scenery.Finish(shell, "Reference bodywork", model, bodyPaint, false);

            MeshDraft glass = new MeshDraft();
            glass.Face(leftRearDeck, leftRearRoof, rearRoof, rearDeck);
            glass.Face(rearDeck, rearRoof, frontRoof, cowl);
            glass.Face(leftCowl, leftFrontRoof, leftRearRoof, leftRearDeck);
            glass.Face(cowl, frontRoof, leftFrontRoof, leftCowl);
            scenery.Finish(glass, "Reference glazing", model, windows, false);

            MeshDraft wheels = new MeshDraft();
            for (int side = -1; side <= 1; side += 2)
            {
                AddWheel(wheels, new Vector3(side * 0.75f, 0.30f, 1.16f));
                AddWheel(wheels, new Vector3(side * 0.75f, 0.30f, -1.43f));
            }

            scenery.Finish(wheels, "Reference tyres", model, tires, false);
            MeshDraft lamps = new MeshDraft();
            lamps.Box(new Vector3(-0.62f, 0.58f, -1.83f), Quaternion.identity, new Vector3(0.24f, 0.09f, 0.06f));
            lamps.Box(new Vector3(0.62f, 0.58f, -1.83f), Quaternion.identity, new Vector3(0.24f, 0.09f, 0.06f));
            scenery.Finish(lamps, "Reference tail lamps", model, tailLamps, false);
        }

        private static void AddWheel(MeshDraft wheel, Vector3 center)
        {
            const int sides = 16;
            Vector3 axle = Vector3.right * 0.105f;
            for (int i = 0; i < sides; i++)
            {
                float a = i * (2f * Mathf.PI / sides);
                float b = (i + 1) * (2f * Mathf.PI / sides);
                Vector3 start = center + new Vector3(0f, Mathf.Cos(a) * 0.30f, Mathf.Sin(a) * 0.30f);
                Vector3 end = center + new Vector3(0f, Mathf.Cos(b) * 0.30f, Mathf.Sin(b) * 0.30f);
                wheel.Face(start - axle, end - axle, end + axle, start + axle);
                wheel.Face(center - axle, end - axle, start - axle);
                wheel.Face(center + axle, start + axle, end + axle);
            }
        }

        private static Vector3 Mirror(Vector3 point)
        {
            return new Vector3(-point.x, point.y, point.z);
        }

        private static Material MakeMaterial(Shader shader, Color color, List<Object> assets)
        {
            Material material = new Material(shader);
            material.SetColor("_Tint", color);
            assets.Add(material);
            return material;
        }
    }
}
