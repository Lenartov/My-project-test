using UnityEngine;

namespace RealtimeCardPrototype
{
    public sealed class FieldPlacementBody : MonoBehaviour
    {
        [SerializeField] private float radius = 0.74f;
        [SerializeField] private float maxAnchorOffset = 0.23f;

        private Rigidbody body;
        private Vector3 anchorPosition;
        private bool isActive;
        private bool isDragging;

        public bool IsActive => isActive;
        public bool IsDragging => isDragging;
        public float Radius => radius;
        public Vector3 Position => body != null ? body.position : transform.position;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            anchorPosition = transform.position;
        }

        private void FixedUpdate()
        {
            if (!isActive)
            {
                return;
            }

            if (isDragging)
            {
                anchorPosition = Position;
            }

            CombatTargetRegistry.ResolveFieldPlacement(this);
            ClampToAnchor();
        }

        public void Configure(float separationRadius, float maxOffset)
        {
            radius = Mathf.Max(0.05f, separationRadius);
            maxAnchorOffset = Mathf.Max(0.05f, maxOffset);
        }

        public void Activate(bool dragging)
        {
            isActive = true;
            isDragging = dragging;
            anchorPosition = Position;
            CombatTargetRegistry.RegisterFieldPlacement(this);
        }

        public void SetDragging(bool dragging)
        {
            isDragging = dragging;
            if (!dragging)
            {
                anchorPosition = Position;
            }
        }

        public void Deactivate()
        {
            isActive = false;
            isDragging = false;
            CombatTargetRegistry.UnregisterFieldPlacement(this);
        }

        public void ApplySeparation(Vector3 offset)
        {
            if (!isActive || offset.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector3 target = Position + offset;
            target.y = Position.y;

            if (!isDragging)
            {
                target = ClampPositionToAnchor(target);
            }

            MoveTo(target);
        }

        private void ClampToAnchor()
        {
            if (isDragging)
            {
                return;
            }

            Vector3 clamped = ClampPositionToAnchor(Position);
            if ((clamped - Position).sqrMagnitude > 0.0001f)
            {
                MoveTo(clamped);
            }
        }

        private Vector3 ClampPositionToAnchor(Vector3 position)
        {
            Vector3 offset = position - anchorPosition;
            offset.y = 0f;
            if (offset.magnitude <= maxAnchorOffset)
            {
                return position;
            }

            Vector3 clamped = anchorPosition + offset.normalized * maxAnchorOffset;
            clamped.y = position.y;
            return clamped;
        }

        private void MoveTo(Vector3 position)
        {
            if (body != null)
            {
                body.MovePosition(Vector3.Lerp(body.position, position, 0.85f));
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, position, 0.85f);
            }
        }

        private void OnDestroy()
        {
            CombatTargetRegistry.UnregisterFieldPlacement(this);
        }
    }
}
