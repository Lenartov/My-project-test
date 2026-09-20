using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealtimeCardPrototype
{
    [RequireComponent(typeof(CardView))]
    public sealed class SplashAttackCard : MonoBehaviour, IPowerBoostTarget
    {
        [SerializeField] private CardHitProfile hitProfile = default;
        [SerializeField] private float dragPlaneHeight = 0.18f;
        [SerializeField] private float followSharpness = 72f;
        [SerializeField] private float maxDragSpeed = 40f;
        [SerializeField] private float splashRadius = 3.1f;
        [SerializeField] private int manaCost = 3;

        public event Action<IHandCard> Played;
        public Func<int, bool> TryPayMana;

        private static readonly Collider[] SplashHits = new Collider[24];
        private readonly HashSet<EnemyDummyCard> splashEnemies = new HashSet<EnemyDummyCard>();
        private CardView cardView;
        private Rigidbody body;
        private Camera mainCamera;
        private Vector3 handPosition;
        private Quaternion handRotation;
        private Vector3 targetPosition;
        private Vector3 lastPosition;
        private Vector3 smoothedVelocity;
        private bool isDragging;
        private bool hasBeenPlayed;
        private bool isPlayable = true;
        private readonly HashSet<int> appliedPowerSources = new HashSet<int>();
        private int powerStacks;
        private const int MaxPowerStacks = 3;
        private const float PowerPerStack = 0.5f;

        public bool IsDragging => isDragging;
        public int ManaCost => manaCost;
        private float PowerMultiplier => 1f + powerStacks * PowerPerStack;
        public Transform Transform => transform;

        event Action<IHandCard> IHandCard.LeftHand
        {
            add { }
            remove { }
        }

        private void Awake()
        {
            cardView = GetComponent<CardView>();
            body = cardView.Body;
            mainCamera = Camera.main;

            if (hitProfile.damage <= 0f)
            {
                hitProfile = CardHitProfile.BasicAttack();
                hitProfile.damage *= 1.2f;
                hitProfile.postureDamage *= 1.1f;
                hitProfile.impulse *= 1.05f;
            }
        }

        private void Update()
        {
            if (hasBeenPlayed || !isPlayable)
            {
                return;
            }

            if (isDragging)
            {
                targetPosition = ReadPointerWorldPosition();
            }
        }

        private void FixedUpdate()
        {
            if (hasBeenPlayed || !isDragging)
            {
                return;
            }

            Vector3 previousPosition = body.position;
            Vector3 nextPosition = Vector3.Lerp(body.position, targetPosition, 1f - Mathf.Exp(-followSharpness * Time.fixedDeltaTime));
            Vector3 stepVelocity = (nextPosition - previousPosition) / Time.fixedDeltaTime;

            if (stepVelocity.magnitude > maxDragSpeed)
            {
                stepVelocity = stepVelocity.normalized * maxDragSpeed;
                nextPosition = previousPosition + stepVelocity * Time.fixedDeltaTime;
            }

            body.MovePosition(nextPosition);
            smoothedVelocity = Vector3.Lerp(smoothedVelocity, stepVelocity, 0.75f);
            lastPosition = previousPosition;
        }

        private void OnMouseDown()
        {
            if (hasBeenPlayed || !isPlayable)
            {
                return;
            }

            BeginDrag();
        }

        private void OnMouseUp()
        {
            if (hasBeenPlayed)
            {
                return;
            }

            EndDrag();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (hasBeenPlayed || !isDragging)
            {
                return;
            }

            EnemyDummyCard mainEnemy = collision.collider.GetComponentInParent<EnemyDummyCard>();
            if (mainEnemy == null || smoothedVelocity.magnitude < hitProfile.minSpeedForHit)
            {
                return;
            }

            if (TryPayMana == null || !TryPayMana(manaCost))
            {
                Debug.Log($"Not enough mana to play {name}. Cost: {manaCost}");
                EndDrag();
                return;
            }

            ContactPoint contact = collision.GetContact(0);
            hasBeenPlayed = true;
            CardHitProfile boostedProfile = hitProfile;
            boostedProfile.damage *= PowerMultiplier;

            DamageResult directHit = mainEnemy.ReceiveHit(boostedProfile, lastPosition, smoothedVelocity, contact.point);
            ApplySplashDamage(directHit, mainEnemy, contact.point);
            Played?.Invoke(this);
            Destroy(gameObject);
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
        }

        public void SetPlayable(bool playable)
        {
            if (isPlayable == playable)
            {
                return;
            }

            isPlayable = playable;
            cardView.SetAvailable(playable);

            if (!isPlayable && isDragging)
            {
                EndDrag();
            }
        }

        private void ApplySplashDamage(DamageResult directHit, EnemyDummyCard mainEnemy, Vector3 blastPoint)
        {
            splashEnemies.Clear();
            int hitCount = Physics.OverlapSphereNonAlloc(blastPoint, splashRadius, SplashHits);

            for (int i = 0; i < hitCount; i++)
            {
                EnemyDummyCard enemy = SplashHits[i].GetComponentInParent<EnemyDummyCard>();
                if (enemy == null || ReferenceEquals(enemy, mainEnemy) || splashEnemies.Contains(enemy))
                {
                    continue;
                }

                Vector3 closestPoint = SplashHits[i].ClosestPoint(blastPoint);
                Vector3 planarOffset = closestPoint - blastPoint;
                planarOffset.y = 0f;
                float distance = planarOffset.magnitude;
                if (distance > splashRadius)
                {
                    continue;
                }

                float normalizedDistance = Mathf.Clamp01(distance / splashRadius);
                float damageScale = 1f - normalizedDistance;
                splashEnemies.Add(enemy);
                enemy.ReceiveSplashHit(directHit.Damage * damageScale, directHit.PostureDamage * damageScale, blastPoint);
            }
        }

        private void BeginDrag()
        {
            mainCamera = Camera.main;
            isDragging = true;
            targetPosition = ReadPointerWorldPosition();
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private void EndDrag()
        {
            isDragging = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
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
