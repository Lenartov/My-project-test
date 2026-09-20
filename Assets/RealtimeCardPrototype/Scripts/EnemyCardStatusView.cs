using UnityEngine;

namespace RealtimeCardPrototype
{
    internal enum EnemyCombatSide
    {
        Front = 0,
        Back = 1,
        Left = 2,
        Right = 3
    }

    internal enum EnemyEdgeRole
    {
        Normal,
        Blocking,
        Exposed
    }

    internal sealed class EnemyCardStatusView
    {
        private const int SideCount = 4;
        private const float BarWidth = 0.92f;
        private const float EnemyTextScale = 0.8f;

        private readonly Transform owner;
        private readonly BoxCollider cardCollider;
        private readonly Renderer[] sideMarkers = new Renderer[SideCount];
        private Transform poisonLevelOneFill;
        private Transform poisonLevelTwoFill;
        private Transform poisonLevelThreeFill;
        private Transform balanceFill;
        private TextMesh balanceText;
        private Transform shockFill;
        private TextMesh shockText;

        public EnemyCardStatusView(Transform owner, BoxCollider cardCollider)
        {
            this.owner = owner;
            this.cardCollider = cardCollider;
        }

        public void Create()
        {
            CreateSideMarkers();
            CreatePoisonBar();
            CreateBalanceBar();
            CreateShockBar();
        }

        public void Refresh(
            EnemyEdgeRole[] sideRoles,
            int exposedSideIndex,
            float exposedSideRemaining,
            float balance,
            float maxBalance,
            int poisonLevel,
            float poisonStackProgress,
            float poisonStackStepSeconds,
            float shockRemaining,
            float shockDuration)
        {
            RefreshSideMarkers(sideRoles, exposedSideIndex, exposedSideRemaining);
            RefreshBalance(balance, maxBalance);
            RefreshPoison(poisonLevel, poisonStackProgress, poisonStackStepSeconds);
            RefreshShock(shockRemaining, shockDuration);
        }

        private void CreateSideMarkers()
        {
            Vector3 size = cardCollider.size;
            sideMarkers[(int)EnemyCombatSide.Front] = CreateMarker("Front Edge", new Vector3(0f, 0.106f, -size.z * 0.52f), new Vector3(size.x * 0.82f, 0.026f, 0.08f));
            sideMarkers[(int)EnemyCombatSide.Back] = CreateMarker("Back Edge", new Vector3(0f, 0.106f, size.z * 0.52f), new Vector3(size.x * 0.82f, 0.026f, 0.08f));
            sideMarkers[(int)EnemyCombatSide.Left] = CreateMarker("Left Edge", new Vector3(-size.x * 0.52f, 0.105f, 0f), new Vector3(0.08f, 0.025f, size.z * 0.86f));
            sideMarkers[(int)EnemyCombatSide.Right] = CreateMarker("Right Edge", new Vector3(size.x * 0.52f, 0.105f, 0f), new Vector3(0.08f, 0.025f, size.z * 0.86f));
        }

        private Renderer CreateMarker(string name, Vector3 localPosition, Vector3 localScale)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = name;
            marker.transform.SetParent(owner, false);
            marker.transform.localPosition = localPosition;
            marker.transform.localScale = localScale;
            DisableCollider(marker.GetComponent<Collider>());
            Renderer renderer = marker.GetComponent<Renderer>();
            renderer.sharedMaterial = PrototypeMaterials.Create(GetRoleColor(EnemyEdgeRole.Normal), 0.2f);
            return renderer;
        }

        private void RefreshSideMarkers(EnemyEdgeRole[] sideRoles, int exposedSideIndex, float exposedSideRemaining)
        {
            for (int i = 0; i < SideCount; i++)
            {
                EnemyEdgeRole role = i == exposedSideIndex && exposedSideRemaining > 0f
                    ? EnemyEdgeRole.Exposed
                    : sideRoles[i];
                sideMarkers[i].gameObject.SetActive(role != EnemyEdgeRole.Normal);
                if (role != EnemyEdgeRole.Normal)
                {
                    SetRendererColor(sideMarkers[i], GetRoleColor(role));
                }
            }
        }

        private void CreateBalanceBar()
        {
            Transform root = CreateBarRoot("Break Bar", new Vector3(0f, 0.175f, -0.54f));
            CreateBarBackground(root, "Break Bar Background", new Color(0.075f, 0.026f, 0.008f), 0.028f, 0.08f);
            balanceFill = CreateBarFill(root, "Break Bar Fill", new Color(1f, 0.38f, 0.04f), 0.04f, 0.09f);
            balanceText = CreateBarLabel(root, "Break Label", new Color(1f, 0.72f, 0.18f), 0.042f);
        }

        private void CreatePoisonBar()
        {
            Transform root = CreateBarRoot("Poison Bars", new Vector3(0f, 0.16f, 0.58f));
            CreateBarBackground(root, "Poison Bar Background", new Color(0.025f, 0.045f, 0.03f), 0.018f, 0.055f);
            poisonLevelOneFill = CreatePoisonFill(root, "Poison Level 1", new Color(0.04f, 0.32f, 0.09f), 0.002f);
            poisonLevelTwoFill = CreatePoisonFill(root, "Poison Level 2", new Color(0.1f, 0.58f, 0.16f), 0.006f);
            poisonLevelThreeFill = CreatePoisonFill(root, "Poison Level 3", new Color(0.22f, 1f, 0.28f), 0.01f);
        }

        private void CreateShockBar()
        {
            Transform root = CreateBarRoot("Shock Mark Bar", new Vector3(0f, 0.17f, 0.47f));
            CreateBarBackground(root, "Shock Bar Background", new Color(0.015f, 0.03f, 0.06f), 0.016f, 0.05f);
            shockFill = CreateBarFill(root, "Shock Bar Fill", new Color(0.16f, 0.58f, 1f), 0.02f, 0.055f);
            shockText = CreateBarLabel(root, "Shock Label", new Color(0.5f, 0.82f, 1f), 0.036f);
        }

        private Transform CreateBarRoot(string name, Vector3 localPosition)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(owner, false);
            root.transform.localPosition = localPosition;
            return root.transform;
        }

        private void CreateBarBackground(Transform parent, string name, Color color, float height, float depth)
        {
            GameObject background = GameObject.CreatePrimitive(PrimitiveType.Cube);
            background.name = name;
            background.transform.SetParent(parent, false);
            background.transform.localScale = new Vector3(BarWidth, height, depth);
            background.GetComponent<Renderer>().sharedMaterial = PrototypeMaterials.Create(color, 0.1f);
            DisableCollider(background.GetComponent<Collider>());
        }

        private Transform CreateBarFill(Transform parent, string name, Color color, float height, float depth)
        {
            GameObject fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fill.name = name;
            fill.transform.SetParent(parent, false);
            fill.transform.localScale = new Vector3(0f, height, depth);
            fill.GetComponent<Renderer>().sharedMaterial = PrototypeMaterials.Create(color, 0.18f);
            DisableCollider(fill.GetComponent<Collider>());
            return fill.transform;
        }

        private TextMesh CreateBarLabel(Transform parent, string name, Color color, float characterSize)
        {
            GameObject label = new GameObject(name);
            label.transform.SetParent(parent, false);
            label.transform.localPosition = new Vector3(0f, 0.022f, 0.07f);
            label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            TextMesh text = label.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = characterSize * EnemyTextScale;
            text.fontSize = 64;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            label.GetComponent<MeshRenderer>().sortingOrder = 12;
            return text;
        }

        private Transform CreatePoisonFill(Transform parent, string name, Color color, float yOffset)
        {
            Transform fill = CreateBarFill(parent, name, color, 0.02f, 0.06f);
            fill.localPosition = new Vector3(-BarWidth * 0.5f, yOffset, 0f);
            return fill;
        }

        private void RefreshBalance(float balance, float maxBalance)
        {
            float amount = maxBalance > 0f ? Mathf.Clamp01(balance / maxBalance) : 0f;
            SetFill(balanceFill, amount);
            balanceText.text = $"BREAK {balance:0}/{maxBalance:0}";
        }

        private void RefreshPoison(int level, float progress, float stepSeconds)
        {
            if (level <= 0 || stepSeconds <= 0f)
            {
                SetFill(poisonLevelOneFill, 0f);
                SetFill(poisonLevelTwoFill, 0f);
                SetFill(poisonLevelThreeFill, 0f);
                return;
            }

            SetFill(poisonLevelOneFill, 1f);
            SetFill(poisonLevelTwoFill, Mathf.Clamp01(progress / stepSeconds));
            SetFill(poisonLevelThreeFill, Mathf.Clamp01((progress - stepSeconds) / stepSeconds));
        }

        private void RefreshShock(float remaining, float duration)
        {
            float amount = duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;
            SetFill(shockFill, amount);
            shockText.text = amount > 0f ? $"ZAP {remaining:0.0}" : string.Empty;
        }

        private void SetFill(Transform fill, float amount)
        {
            float width = BarWidth * Mathf.Clamp01(amount);
            fill.localScale = new Vector3(width, fill.localScale.y, fill.localScale.z);
            fill.localPosition = new Vector3(-BarWidth * 0.5f + width * 0.5f, fill.localPosition.y, fill.localPosition.z);
        }

        private void SetRendererColor(Renderer renderer, Color color)
        {
            renderer.material.color = color;
            if (renderer.material.HasProperty("_BaseColor"))
            {
                renderer.material.SetColor("_BaseColor", color);
            }
        }

        private static Color GetRoleColor(EnemyEdgeRole role)
        {
            switch (role)
            {
                case EnemyEdgeRole.Blocking:
                    return new Color(0.22f, 0.42f, 1f);
                case EnemyEdgeRole.Exposed:
                    return new Color(0.18f, 1f, 0.24f);
                default:
                    return Color.clear;
            }
        }

        private static void DisableCollider(Collider collider)
        {
            collider.enabled = false;
            Object.Destroy(collider);
        }
    }
}
