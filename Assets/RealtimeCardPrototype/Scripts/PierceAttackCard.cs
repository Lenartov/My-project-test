using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealtimeCardPrototype
{
    [RequireComponent(typeof(CardView))]
    public sealed class PierceAttackCard : MonoBehaviour, IPowerBoostTarget
    {
        [SerializeField] private CardHitProfile hitProfile = default;
        [SerializeField] private float dragPlaneHeight = 0.18f;
        [SerializeField] private float followSharpness = 72f;
        [SerializeField] private float maxDragSpeed = 40f;
        [SerializeField] private float returnSharpness = 28f;
        [SerializeField] private float activeLifetime = 0.8f;
        [SerializeField] private int manaCost = 2;
        [SerializeField] private bool canPickUpFromField = true;

        public event Action<IHandCard> LeftHand;
        public event Action<IHandCard> Played;
        public Func<int, bool> TryPayMana;

        private readonly HashSet<EnemyDummyCard> enemiesInside = new HashSet<EnemyDummyCard>();
        private CardView cardView;
        private Rigidbody body;
        private BoxCollider cardCollider;
        private Camera mainCamera;
        private Vector3 handPosition;
        private Quaternion handRotation;
        private Vector3 targetPosition;
        private Vector3 lastPosition;
        private Vector3 smoothedVelocity;
        private Vector3 pointerVelocity;
        private float lifetimeTimer;
        private bool isDragging;
        private bool isActiveOnField;
        private bool hasBeenPlayed;
        private bool hasPointerSample;
        private bool isPlayable = true;
        private readonly HashSet<int> appliedPowerSources = new HashSet<int>();
        private int powerStacks;
        private Transform chargeFill;
        private const int MaxPowerStacks = 3;
        private const float PowerPerStack = 0.5f;
        private const float ChargeBarWidth = 0.72f;

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

            if (hitProfile.damage <= 0f)
            {
                hitProfile = CardHitProfile.BasicAttack();
                hitProfile.damage *= 0.35f;
                hitProfile.postureDamage *= 0.5f;
                hitProfile.impulse *= 0.45f;
            }

            CreateChargeBar();
            EnsurePiercingCollider();
        }

        private void Update()
        {
            if (hasBeenPlayed)
            {
                return;
            }

            if (isDragging)
            {
                UpdatePointerTarget();
                EnsurePiercingCollider();
            }

            if (isActiveOnField)
            {
                EnsurePiercingCollider();
                lifetimeTimer += Time.deltaTime;
                UpdateChargeBar();
                if (lifetimeTimer >= activeLifetime)
                {
                    FinishPlayed();
                }
            }
        }

        private void FixedUpdate()
        {
            if (hasBeenPlayed)
            {
                return;
            }

            Vector3 previousPosition = body.position;

            if (isDragging)
            {
                Vector3 nextPosition = Vector3.Lerp(body.position, targetPosition, 1f - Mathf.Exp(-followSharpness * Time.fixedDeltaTime));
                Vector3 stepVelocity = (nextPosition - previousPosition) / Time.fixedDeltaTime;

                if (stepVelocity.magnitude > maxDragSpeed)
                {
                    stepVelocity = stepVelocity.normalized * maxDragSpeed;
                    nextPosition = previousPosition + stepVelocity * Time.fixedDeltaTime;
                }

                body.MovePosition(nextPosition);
                Vector3 sampleVelocity = pointerVelocity.sqrMagnitude > 0.01f
                    ? Vector3.Lerp(stepVelocity, pointerVelocity, 0.65f)
                    : stepVelocity;
                smoothedVelocity = Vector3.Lerp(smoothedVelocity, sampleVelocity, 0.75f);
            }
            else if (!isActiveOnField)
            {
                Vector3 nextPosition = Vector3.Lerp(body.position, handPosition, 1f - Mathf.Exp(-returnSharpness * Time.fixedDeltaTime));
                body.MovePosition(nextPosition);
                body.MoveRotation(Quaternion.Slerp(body.rotation, handRotation, Time.fixedDeltaTime * returnSharpness));
                smoothedVelocity = (nextPosition - previousPosition) / Time.fixedDeltaTime;
            }

            lastPosition = previousPosition;
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

            BeginDrag();
        }

        private void OnMouseUp()
        {
            if (hasBeenPlayed || !isDragging)
            {
                return;
            }

            if (isActiveOnField)
            {
                EndDragOnField();
            }
            else
            {
                EndDragToHand();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasBeenPlayed || !isDragging)
            {
                return;
            }

            EnemyDummyCard enemy = other.GetComponentInParent<EnemyDummyCard>();
            Vector3 hitVelocity = GetHitVelocity();
            if (enemy == null || enemiesInside.Contains(enemy) || hitVelocity.magnitude < hitProfile.minSpeedForHit)
            {
                return;
            }

            if (!isActiveOnField)
            {
                isActiveOnField = true;
                lifetimeTimer = 0f;
                EnsurePiercingCollider();

                if (TryPayMana == null || !TryPayMana(manaCost))
                {
                    isActiveOnField = false;
                    lifetimeTimer = 0f;
                    Debug.Log($"Not enough mana to play {name}. Cost: {manaCost}");
                    EndDragToHand();
                    UpdateChargeBar();
                    return;
                }

                UpdateChargeBar();
                LeftHand?.Invoke(this);
            }

            enemiesInside.Add(enemy);
            Vector3 contactPoint = other.ClosestPoint(transform.position);
            CardHitProfile boostedProfile = hitProfile;
            boostedProfile.damage *= PowerMultiplier;
            enemy.ReceiveHit(boostedProfile, lastPosition, hitVelocity, contactPoint);
        }

        private void OnTriggerExit(Collider other)
        {
            EnemyDummyCard enemy = other.GetComponentInParent<EnemyDummyCard>();
            if (enemy != null)
            {
                enemiesInside.Remove(enemy);
            }
        }

        public void ConfigureProfile(CardHitProfile profile)
        {
            hitProfile = profile;
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

            if (isDragging || isActiveOnField)
            {
                EnsurePiercingCollider();
            }
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

            if (isActiveOnField)
            {
                return;
            }

            cardView.SetAvailable(playable);

            if (!isPlayable && isDragging && !isActiveOnField)
            {
                EndDragToHand();
            }
        }

        private void BeginDrag()
        {
            mainCamera = Camera.main;
            isDragging = true;
            targetPosition = ReadPointerWorldPosition();
            pointerVelocity = Vector3.zero;
            smoothedVelocity = Vector3.zero;
            hasPointerSample = true;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            EnsurePiercingCollider();
        }

        private void EndDragToHand()
        {
            isDragging = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            pointerVelocity = Vector3.zero;
            hasPointerSample = false;
            EnsurePiercingCollider();
            enemiesInside.Clear();
        }

        private void EndDragOnField()
        {
            isDragging = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            pointerVelocity = Vector3.zero;
            hasPointerSample = false;
            EnsurePiercingCollider();
        }

        private void FinishPlayed()
        {
            hasBeenPlayed = true;
            isDragging = false;
            isActiveOnField = false;
            Played?.Invoke(this);
            Destroy(gameObject);
        }

        private void UpdatePointerTarget()
        {
            Vector3 nextTarget = ReadPointerWorldPosition();

            if (hasPointerSample)
            {
                float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
                pointerVelocity = Vector3.ClampMagnitude((nextTarget - targetPosition) / deltaTime, maxDragSpeed);
            }
            else
            {
                pointerVelocity = Vector3.zero;
                hasPointerSample = true;
            }

            targetPosition = nextTarget;
        }

        private Vector3 GetHitVelocity()
        {
            Vector3 velocity = smoothedVelocity.sqrMagnitude >= pointerVelocity.sqrMagnitude
                ? smoothedVelocity
                : pointerVelocity;

            if (velocity.sqrMagnitude < 0.01f)
            {
                velocity = (body.position - lastPosition) / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            }

            return Vector3.ClampMagnitude(velocity, maxDragSpeed);
        }

        private void EnsurePiercingCollider()
        {
            if (cardCollider != null && !cardCollider.isTrigger)
            {
                cardCollider.isTrigger = true;
            }

            if (body != null && !body.isKinematic)
            {
                body.isKinematic = true;
            }
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
            background.GetComponent<Renderer>().sharedMaterial = PrototypeMaterials.Create(new Color(0.025f, 0.04f, 0.045f), 0.1f);
            DisableAndDestroyCollider(background.GetComponent<Collider>());

            GameObject fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fill.name = "Charge Bar Fill";
            fill.transform.SetParent(root.transform, false);
            fill.transform.localScale = new Vector3(ChargeBarWidth, 0.02f, 0.058f);
            fill.GetComponent<Renderer>().sharedMaterial = PrototypeMaterials.Create(new Color(0.12f, 0.9f, 1f), 0.18f);
            DisableAndDestroyCollider(fill.GetComponent<Collider>());

            chargeFill = fill.transform;
            UpdateChargeBar();
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
    }

}
