using System;
using UnityEngine;

namespace RealtimeCardPrototype
{
    [RequireComponent(typeof(CardView))]
    public abstract class FieldStructureCard : MonoBehaviour, IHandCard, IEnemyAttackTarget
    {
        private const float HealthBarWidth = 0.72f;

        [SerializeField] private float dragPlaneHeight = 0.2f;
        [SerializeField] private float followSharpness = 72f;
        [SerializeField] private float maxDragSpeed = 40f;
        [SerializeField] private float returnSharpness = 28f;

        public event Action<IHandCard> LeftHand;
        public event Action<IHandCard> Played;
        public Func<int, bool> TryPayMana;
        public Action<int> RefundMana;

        private CardView cardView;
        private Rigidbody body;
        private BoxCollider cardCollider;
        private Camera mainCamera;
        private FieldPlacementBody fieldPlacement;
        private Vector3 handPosition;
        private Quaternion handRotation;
        private Vector3 targetPosition;
        private float currentHealth;
        private int manaCost;
        private bool isDragging;
        private bool isActiveOnField;
        private bool hasBeenPlayed;
        private bool hasLeftHand;
        private bool isPlayable = true;
        private Transform healthFill;
        private TextMesh healthText;

        public bool IsDragging => isDragging;
        public int ManaCost => manaCost;
        public Transform Transform => transform;
        public Transform TargetTransform => transform;
        public Vector3 TargetPoint => transform.position + Vector3.up * 0.42f;
        public virtual int TargetPriority => 0;
        public bool IsTargetable => isActiveOnField && !hasBeenPlayed && currentHealth > 0f;
        public virtual bool CanBeDirectTarget => true;
        public virtual bool CanInterceptAttacks => false;
        public virtual float InterceptRadius => 0f;
        protected float CurrentHealth => currentHealth;
        protected bool IsActiveOnField => isActiveOnField;
        protected bool HasLeftHand => hasLeftHand;

        protected virtual float InitialMaxHealth => 3f;
        protected virtual int DefaultManaCost => 1;
        protected virtual Color HealthBarFillColor => new Color(1f, 0.72f, 0.08f);
        protected virtual Color HealthBarBackgroundColor => new Color(0.09f, 0.025f, 0.03f);
        protected virtual bool ReservesHandSlotUntilCommitted => false;

        protected virtual void Awake()
        {
            cardView = GetComponent<CardView>();
            body = cardView.Body;
            cardCollider = cardView.Collider;
            fieldPlacement = gameObject.AddComponent<FieldPlacementBody>();
            fieldPlacement.Configure(0.74f, 0.23f);
            mainCamera = Camera.main;
            manaCost = DefaultManaCost;
            currentHealth = InitialMaxHealth;
            CreateHealthBar();
        }

        protected virtual void Update()
        {
            if (!hasBeenPlayed && isDragging)
            {
                targetPosition = ReadPointerWorldPosition();
            }
        }

        protected virtual void FixedUpdate()
        {
            if (hasBeenPlayed)
            {
                return;
            }

            if (isDragging)
            {
                Vector3 nextPosition = Vector3.Lerp(body.position, targetPosition, 1f - Mathf.Exp(-followSharpness * Time.fixedDeltaTime));
                Vector3 stepVelocity = (nextPosition - body.position) / Time.fixedDeltaTime;

                if (stepVelocity.magnitude > maxDragSpeed)
                {
                    stepVelocity = stepVelocity.normalized * maxDragSpeed;
                    nextPosition = body.position + stepVelocity * Time.fixedDeltaTime;
                }

                body.MovePosition(nextPosition);
                return;
            }

            if (!isActiveOnField)
            {
                Vector3 nextPosition = Vector3.Lerp(body.position, handPosition, 1f - Mathf.Exp(-returnSharpness * Time.fixedDeltaTime));
                body.MovePosition(nextPosition);
                body.MoveRotation(Quaternion.Slerp(body.rotation, handRotation, Time.fixedDeltaTime * returnSharpness));
            }
        }

        protected void OnMouseDown()
        {
            if (hasBeenPlayed || isActiveOnField || !isPlayable)
            {
                return;
            }

            if (TryPayMana == null || !TryPayMana(manaCost))
            {
                Debug.Log($"Not enough mana to play {name}. Cost: {manaCost}");
                return;
            }

            isActiveOnField = true;
            cardCollider.isTrigger = true;
            OnStructureActivated();
            UpdateHealthBar();
            CombatTargetRegistry.RegisterStructure(this);
            fieldPlacement.Activate(true);
            if (!ReservesHandSlotUntilCommitted)
            {
                LeaveHand();
            }

            BeginDrag();
        }

        protected virtual void OnMouseUp()
        {
            if (hasBeenPlayed || !isDragging)
            {
                return;
            }

            isDragging = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            fieldPlacement.SetDragging(false);
        }

        public void ReceiveEnemyAttack(float damage)
        {
            ApplyStructureDamage(damage);
        }

        public void ApplyStructureDamage(float damage)
        {
            AbsorbStructureDamage(damage, false);
        }

        public void ConfigureManaCost(int cost)
        {
            manaCost = Mathf.Max(0, cost);
        }

        public void SetHandSlot(Vector3 position, Quaternion rotation)
        {
            handPosition = position;
            handRotation = rotation;

            if (!isDragging && !hasBeenPlayed && !isActiveOnField)
            {
                transform.SetPositionAndRotation(position, rotation);
            }
        }

        public void SetPlayable(bool playable)
        {
            if (isPlayable == playable)
            {
                return;
            }

            isPlayable = playable;
            cardView.SetAvailable(playable);
        }

        protected virtual void OnStructureActivated()
        {
        }

        protected virtual void OnStructureDeactivated()
        {
        }

        protected float AbsorbStructureDamage(float damage, bool consumeAfterAbsorb)
        {
            if (hasBeenPlayed || !isActiveOnField)
            {
                return Mathf.Max(0f, damage);
            }

            float incomingDamage = Mathf.Max(0f, damage);
            float absorbedDamage = Mathf.Min(currentHealth, incomingDamage);
            currentHealth -= absorbedDamage;
            UpdateHealthBar();

            if (consumeAfterAbsorb || currentHealth <= 0f)
            {
                FinishPlayed();
            }

            return incomingDamage - absorbedDamage;
        }

        private void BeginDrag()
        {
            mainCamera = Camera.main;
            isDragging = true;
            fieldPlacement.SetDragging(true);
            targetPosition = ReadPointerWorldPosition();
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private void LeaveHand()
        {
            if (hasLeftHand)
            {
                return;
            }

            hasLeftHand = true;
            LeftHand?.Invoke(this);
        }

        protected void FinishPlayed()
        {
            hasBeenPlayed = true;
            isDragging = false;
            isActiveOnField = false;
            OnStructureDeactivated();
            CombatTargetRegistry.UnregisterStructure(this);
            fieldPlacement.Deactivate();
            Played?.Invoke(this);
            Destroy(gameObject);
        }

        protected void CommitHandCard()
        {
            LeaveHand();
        }

        protected void ReturnUncommittedCardToHand()
        {
            if (!isActiveOnField || hasBeenPlayed || hasLeftHand)
            {
                throw new InvalidOperationException("Only an uncommitted field card can return to the hand.");
            }

            isDragging = false;
            isActiveOnField = false;
            cardCollider.isTrigger = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            OnStructureDeactivated();
            CombatTargetRegistry.UnregisterStructure(this);
            fieldPlacement.Deactivate();
            RefundMana?.Invoke(manaCost);
        }

        protected virtual void OnDestroy()
        {
            OnStructureDeactivated();
            CombatTargetRegistry.UnregisterStructure(this);
            if (fieldPlacement != null)
            {
                fieldPlacement.Deactivate();
            }
        }

        private void CreateHealthBar()
        {
            GameObject root = new GameObject("HP Bar");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, 0.116f, -0.56f);

            GameObject background = GameObject.CreatePrimitive(PrimitiveType.Cube);
            background.name = "HP Bar Background";
            background.transform.SetParent(root.transform, false);
            background.transform.localScale = new Vector3(HealthBarWidth, 0.016f, 0.052f);
            background.GetComponent<Renderer>().sharedMaterial = PrototypeMaterials.Create(HealthBarBackgroundColor, 0.1f);
            DisableAndDestroyCollider(background.GetComponent<Collider>());

            GameObject fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fill.name = "HP Bar Fill";
            fill.transform.SetParent(root.transform, false);
            fill.transform.localScale = new Vector3(HealthBarWidth, 0.02f, 0.058f);
            fill.GetComponent<Renderer>().sharedMaterial = PrototypeMaterials.Create(HealthBarFillColor, 0.18f);
            DisableAndDestroyCollider(fill.GetComponent<Collider>());

            GameObject labelObject = new GameObject("HP Label");
            labelObject.transform.SetParent(root.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.022f, 0.082f);
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            healthText = labelObject.AddComponent<TextMesh>();
            healthText.anchor = TextAnchor.MiddleCenter;
            healthText.alignment = TextAlignment.Center;
            healthText.characterSize = 0.038f;
            healthText.fontSize = 64;
            healthText.fontStyle = FontStyle.Bold;
            healthText.color = Color.white;
            labelObject.GetComponent<MeshRenderer>().sortingOrder = 11;

            healthFill = fill.transform;
            UpdateHealthBar();
        }

        private void UpdateHealthBar()
        {
            if (healthFill == null)
            {
                return;
            }

            float amount = InitialMaxHealth > 0f ? Mathf.Clamp01(currentHealth / InitialMaxHealth) : 0f;
            float width = HealthBarWidth * amount;
            healthFill.localScale = new Vector3(width, healthFill.localScale.y, healthFill.localScale.z);
            healthFill.localPosition = new Vector3(-HealthBarWidth * 0.5f + width * 0.5f, healthFill.localPosition.y, healthFill.localPosition.z);

            if (healthText != null)
            {
                healthText.text = $"HP {currentHealth:0}/{InitialMaxHealth:0}";
            }
        }

        private Vector3 ReadPointerWorldPosition()
        {
            if (mainCamera == null)
            {
                return transform.position;
            }

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            Plane dragPlane = new Plane(Vector3.up, new Vector3(0f, dragPlaneHeight, 0f));
            if (dragPlane.Raycast(ray, out float distance))
            {
                Vector3 point = ray.GetPoint(distance);
                point.y = dragPlaneHeight;
                return point;
            }

            return transform.position;
        }

        private void DisableAndDestroyCollider(Collider collider)
        {
            if (collider == null)
            {
                return;
            }

            collider.enabled = false;
            Destroy(collider);
        }
    }
}
