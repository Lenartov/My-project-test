using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealtimeCardPrototype
{
    [RequireComponent(typeof(CardView))]
    public sealed class GravitonCard : MonoBehaviour, IHandCard
    {
        [SerializeField] private float dragPlaneHeight = 0.72f;
        [SerializeField] private float followSharpness = 72f;
        [SerializeField] private float maxDragSpeed = 40f;
        [SerializeField] private float returnSharpness = 28f;
        [SerializeField] private float activeLifetime = 2f;
        [SerializeField] private float effectRadius = 3.4f;
        [SerializeField] private float pullAcceleration = 55f;
        [SerializeField] private float tickInterval = 0.5f;
        [SerializeField] private float nearTickDamage = 5f;
        [SerializeField] private float farTickDamage = 2f;
        [SerializeField] private int manaCost = 4;

        public event Action<IHandCard> LeftHand;
        public event Action<IHandCard> Played;
        public Func<int, bool> TryPayMana;

        private static readonly Collider[] AreaHits = new Collider[32];
        private readonly HashSet<EnemyDummyCard> enemiesInTick = new HashSet<EnemyDummyCard>();
        private CardView cardView;
        private Rigidbody body;
        private BoxCollider cardCollider;
        private Camera mainCamera;
        private Vector3 handPosition;
        private Quaternion handRotation;
        private Vector3 targetPosition;
        private float lifetimeTimer;
        private float damageTimer;
        private bool isDragging;
        private bool isActiveOnField;
        private bool hasBeenPlayed;
        private bool isPlayable = true;
        private Transform chargeFill;
        private Transform areaDisc;
        private const float ChargeBarWidth = 0.72f;

        public bool IsDragging => isDragging;
        public int ManaCost => manaCost;
        public Transform Transform => transform;

        private void Awake()
        {
            cardView = GetComponent<CardView>();
            body = cardView.Body;
            cardCollider = cardView.Collider;
            mainCamera = Camera.main;
            CreateChargeBar();
            CreateAreaDisc();
        }

        private void Update()
        {
            if (hasBeenPlayed)
            {
                return;
            }

            if (isDragging)
            {
                targetPosition = ReadPointerWorldPosition();
            }

            if (isActiveOnField)
            {
                lifetimeTimer += Time.deltaTime;
                damageTimer += Time.deltaTime;
                UpdateChargeBar();

                while (damageTimer >= tickInterval)
                {
                    damageTimer -= tickInterval;
                    ApplyDamageTick();
                }

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

            if (isActiveOnField)
            {
                ApplyPull();
            }
            else
            {
                Vector3 nextPosition = Vector3.Lerp(body.position, handPosition, 1f - Mathf.Exp(-returnSharpness * Time.fixedDeltaTime));
                body.MovePosition(nextPosition);
                body.MoveRotation(Quaternion.Slerp(body.rotation, handRotation, Time.fixedDeltaTime * returnSharpness));
            }
        }

        private void OnMouseDown()
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

            LeftHand?.Invoke(this);
            BeginDrag();
        }

        private void OnMouseUp()
        {
            if (hasBeenPlayed || !isDragging)
            {
                return;
            }

            isDragging = false;
            isActiveOnField = true;
            lifetimeTimer = 0f;
            damageTimer = 0f;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            cardCollider.isTrigger = true;
            if (areaDisc != null)
            {
                areaDisc.gameObject.SetActive(true);
            }

            UpdateChargeBar();
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

        private void BeginDrag()
        {
            mainCamera = Camera.main;
            isDragging = true;
            targetPosition = ReadPointerWorldPosition();
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            cardCollider.isTrigger = true;
        }

        private void ApplyPull()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, effectRadius, AreaHits);
            for (int i = 0; i < hitCount; i++)
            {
                EnemyDummyCard enemy = AreaHits[i].GetComponentInParent<EnemyDummyCard>();
                if (enemy == null)
                {
                    continue;
                }

                float normalizedDistance = GetPlanarDistance01(enemy.transform.position);
                if (normalizedDistance > 1f)
                {
                    continue;
                }

                enemy.ApplyGravitonPull(transform.position, pullAcceleration);
            }
        }

        private void ApplyDamageTick()
        {
            enemiesInTick.Clear();
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, effectRadius, AreaHits);
            for (int i = 0; i < hitCount; i++)
            {
                EnemyDummyCard enemy = AreaHits[i].GetComponentInParent<EnemyDummyCard>();
                if (enemy == null || enemiesInTick.Contains(enemy))
                {
                    continue;
                }

                float normalizedDistance = GetPlanarDistance01(enemy.transform.position);
                if (normalizedDistance > 1f)
                {
                    continue;
                }

                float damage = Mathf.Lerp(nearTickDamage, farTickDamage, normalizedDistance);
                enemiesInTick.Add(enemy);
                enemy.ReceiveGravitonTick(damage);
            }
        }

        private float GetPlanarDistance01(Vector3 position)
        {
            Vector3 offset = position - transform.position;
            offset.y = 0f;
            return offset.magnitude / Mathf.Max(0.001f, effectRadius);
        }

        private void FinishPlayed()
        {
            hasBeenPlayed = true;
            isDragging = false;
            isActiveOnField = false;
            Played?.Invoke(this);
            Destroy(gameObject);
        }

        private void CreateChargeBar()
        {
            GameObject root = new GameObject("Graviton Timer Bar");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, 0.116f, 0.54f);

            GameObject background = GameObject.CreatePrimitive(PrimitiveType.Cube);
            background.name = "Graviton Bar Background";
            background.transform.SetParent(root.transform, false);
            background.transform.localScale = new Vector3(ChargeBarWidth, 0.016f, 0.052f);
            background.GetComponent<Renderer>().sharedMaterial = PrototypeMaterials.Create(new Color(0.015f, 0.012f, 0.025f), 0.1f);
            DisableAndDestroyCollider(background.GetComponent<Collider>());

            GameObject fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fill.name = "Graviton Bar Fill";
            fill.transform.SetParent(root.transform, false);
            fill.transform.localScale = new Vector3(ChargeBarWidth, 0.02f, 0.058f);
            fill.GetComponent<Renderer>().sharedMaterial = PrototypeMaterials.Create(new Color(0.62f, 0.3f, 1f), 0.18f);
            DisableAndDestroyCollider(fill.GetComponent<Collider>());

            chargeFill = fill.transform;
            UpdateChargeBar();
        }

        private void CreateAreaDisc()
        {
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Graviton Radius";
            disc.transform.SetParent(transform, false);
            disc.transform.localPosition = new Vector3(0f, -0.105f, 0f);
            disc.transform.localScale = new Vector3(effectRadius * 2f, 0.008f, effectRadius * 2f);
            disc.GetComponent<Renderer>().sharedMaterial = PrototypeMaterials.Create(new Color(0.05f, 0.025f, 0.09f, 0.32f), 0.2f);
            DisableAndDestroyCollider(disc.GetComponent<Collider>());
            areaDisc = disc.transform;
            areaDisc.gameObject.SetActive(false);
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
