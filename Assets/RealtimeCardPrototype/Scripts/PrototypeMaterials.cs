using UnityEngine;

namespace RealtimeCardPrototype
{
    public static class PrototypeMaterials
    {
        public static Material Create(Color color, float smoothness = 0.35f)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader);
            material.name = $"Prototype_{ColorUtility.ToHtmlStringRGB(color)}";
            material.color = color;

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            return material;
        }
    }
}
