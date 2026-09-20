using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealtimeCardPrototype
{
    [RequireComponent(typeof(CardView))]
    public sealed class PoisonHoverCard : MonoBehaviour, IPowerBoostTarget
    {
        [SerializeField] private float dragPlaneHeight = 0.62f;
        [SerializeField] private float followSharpness = 78f;
        [SerializeField] private float maxDragSpeed = 40f;
        [SerializeField] private float returnSharpness = 28f;
        [SerializeField] private float activeLifetime = 1.1f;
        [SerializeField] private int manaCost = 2;
        [SerializeField] private bool canPickUpFromField = true;
        [SerializeField] private Vector3 hoverHalfExtents = new Vector3(0.62f, 0.58f, 0.82f);

        public event Action<IHandCard> LeftHand;
        public event Action<IHandCard> Played;
        public Func<int, bool> TryPayMana;

        private CardView cardView;
        private Rigidbody body;
        private BoxCollider cardCollider;
        private Camera mainCamera;
        private Vector3 handPosition;
        private Quaternion handRotation;
        private Vector3 targetPosition;
        private float lifetimeTimer;
        private bool isDragging;
        private bool isActiveOnField;
        private bool hasBeenPlayed;
        private bool isPlayable = true;
        private Transform chargeFill;
        private readonly HashSet<int> appliedPowerSources = new HashSet<int>();
        private int powerStacks;
        private const float ChargeBarWidth = 0.72f;
        private const int MaxPowerStacks = 3;
        private const float PowerPerStack = 0.5f;

        public bool IsDragging => isDragging;
        public int ManaCost => manaCost;
        private float PowerMultiplier => 1f + powerStacks * PowerPerStack;
        public Transform Transform => transform;

        private void Awake()
        {
            cardView = GetComponent<CardView>();
            body = cardView.Body;
            cardCollider = cardView.Collider;
            mainCamera = Camera.main;
            CreateChargeBar();
        }

        private void Update()
        {
            if (hasBeenPlayed || !isActiveOnField)
            {
                return;
            }

            lifetimeTimer += Time.deltaTime;

            if (isDragging)
            {
                targetPosition = ReadPointerWorldPosition();
            }

            ApplyPoisonHover(Time.deltaTime);
            UpdateChargeBar();

            if (lifetimeTimer >= activeLifetime)
            {
                FinishPlayed();
            }
        }

        private void FixedUpdate()
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
            }
            else if (!isActiveOnField)
            {
                Vector3 nextPosition = Vector3.Lerp(body.position, handPosition, 1f - Mathf.Exp(-returnSharpness * Time.fixedDeltaTime));
                body.MovePosition(nextPosition);
                body.MoveRotation(Quaternion.Slerp(body.rotation, handRotation, Time.fixedDeltaTime * returnSharpness));
            }
        }

        private void OnMouseDown()
        {
            if (hasBeenPlayed)
            {
                return;
            }

            if (isActiveOnField)
            {
                if (canPickUpFromField)
                {
                    BeginDrag();
                }

                return;
            }

            if (!isPlayable)
            {
                return;
            }

            if (TryPayMana == null || !TryPayMana(manaCost))
            {
                Debug.Log($"Not enough mana to play {name}. Cost: {manaCost}");
                return;
            }

            isActiveOnField = true;
            lifetimeTimer = 0f;
            LeftHand?.Invoke(this);
            UpdateChargeBar();
            BeginDrag();
        }

        private void OnMouseUp()
        {
            if (hasBeenPlayed || !isDragging)
            {
                return;
            }

            EndDragOnField();
        }

        public void ConfigureManaCost(int cost)
        {
            manaCost = Mathf.Max(0, cost);
        }

        public void AddPowerStack(int sourceId)
        {
            if (powerStacks >= MaxPowerStacks || appliedPowerSources.Contains(sourceId))
            {
                return;
            }

            appliedPowerSources.Add(sourceId);
            powerStacks = Mathf.Min(MaxPowerStacks, powerStacks + 1);
            cardView.SetPowerOutline(powerStacks);
        }

        public void SetHandSlot(Vector3 position, Quaternion rotation)
        {
            handPosition = position;
            handRotation = rotation;

            if (!isDragging && !hasBeenPlayed)
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

        private void BeginDrag()
        {
            mainCamera = Camera.main;
            isDragging = true;
            targetPosition = ReadPointerWorldPosition();
            targetPosition.y = dragPlaneHeight;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            cardCollider.isTrigger = true;
        }

        private void EndDragOnField()
        {
            isDragging = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;

            Vector3 placedPosition = body.position;
            placedPosition.y = dragPlaneHeight;
            body.position = placedPosition;
        }

        private void FinishPlayed()
        {
            hasBeenPlayed = true;
            isDragging = false;
            isActiveOnField = false;
            Played?.Invoke(this);
            Destroy(gameObject);
        }

        private void ApplyPoisonHover(float deltaTime)
        {
            Collider[] hits = Physics.OverlapBox(transform.position, hoverHalfExtents, Quaternion.identity);

            for (int i = 0; i < hits.Length; i++)
            {
                EnemyDummyCard enemy = hits[i].GetComponentInParent<EnemyDummyCard>();
                if (enemy == null)
                {
                    continue;
                }

                enemy.ApplyPoisonHover(deltaTime, PowerMultiplier);
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

        private void CreateChargeBar()
        {
            GameObject root = new GameObject("Charge Bar");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, 0.116f, 0.54f);

            GameObject background = GameObject.CreatePrimitive(PrimitiveType.Cube);
            background.name = "Charge Bar Background";
            background.transform.SetParent(root.transform, false);
            background.transform.localScale = new Vector3(ChargeBarWidth, 0.016f, 0.052f);
            background.GetComponent<Renderer>().sharedMaterial = PrototypeMaterials.Create(new Color(0.025f, 0.04f, 0.028f), 0.1f);
            DisableAndDestroyCollider(background.GetComponent<Collider>());

            GameObject fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fill.name = "Charge Bar Fill";
            fill.transform.SetParent(root.transform, false);
            fill.transform.localScale = new Vector3(ChargeBarWidth, 0.02f, 0.058f);
            fill.GetComponent<Renderer>().sharedMaterial = PrototypeMaterials.Create(new Color(0.2f, 1f, 0.28f), 0.18f);
            DisableAndDestroyCollider(fill.GetComponent<Collider>());

            chargeFill = fill.transform;
            UpdateChargeBar();
        }

        private void UpdateChargeBar()
        {
            if (chargeFill == null)
            {
                return;
            }

            float amount = isActiveOnField ? Mathf.Clamp01(1f - lifetimeTimer / activeLifetime) : 1f;
            float width = ChargeBarWidth * amount;
            chargeFill.localScale = new Vector3(width, chargeFill.localScale.y, chargeFill.localScale.z);
            chargeFill.localPosition = new Vector3(-ChargeBarWidth * 0.5f + width * 0.5f, chargeFill.localPosition.y, chargeFill.localPosition.z);
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
