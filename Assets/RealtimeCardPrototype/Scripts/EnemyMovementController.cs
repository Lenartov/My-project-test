using UnityEngine;

namespace RealtimeCardPrototype
{
    internal sealed class EnemyMovementController
    {
        private readonly Rigidbody body;
        private readonly Vector3 anchorPosition;
        private readonly Quaternion anchorRotation;
        private readonly float returnDamping;
        private readonly float maxKnockbackOffset;
        private readonly float limitRelaxDistanceFraction;
        private readonly float limitReturnSpring;
        private readonly float maxPlanarSpeed;
        private readonly float knockbackMinDamage;
        private readonly float knockbackMaxDamage;
        private readonly float maxKnockbackImpulse;

        public EnemyMovementController(
            Rigidbody body,
            Vector3 anchorPosition,
            Quaternion anchorRotation,
            float returnDamping,
            float maxKnockbackOffset,
            float limitRelaxDistanceFraction,
            float limitReturnSpring,
            float maxPlanarSpeed,
            float knockbackMinDamage,
            float knockbackMaxDamage,
            float maxKnockbackImpulse)
        {
            this.body = body;
            this.anchorPosition = anchorPosition;
            this.anchorRotation = anchorRotation;
            this.returnDamping = returnDamping;
            this.maxKnockbackOffset = maxKnockbackOffset;
            this.limitRelaxDistanceFraction = limitRelaxDistanceFraction;
            this.limitReturnSpring = limitReturnSpring;
            this.maxPlanarSpeed = maxPlanarSpeed;
            this.knockbackMinDamage = knockbackMinDamage;
            this.knockbackMaxDamage = knockbackMaxDamage;
            this.maxKnockbackImpulse = maxKnockbackImpulse;
        }

        public void TickFixed()
        {
            ApplyAnchorBounds();
            if (body.linearVelocity.magnitude > maxPlanarSpeed)
            {
                body.linearVelocity = body.linearVelocity.normalized * maxPlanarSpeed;
            }

            body.MoveRotation(Quaternion.Slerp(body.rotation, anchorRotation, Time.fixedDeltaTime * 4f));
        }

        public void ApplyGravitonPull(Vector3 center, float acceleration)
        {
            if (acceleration <= 0f)
            {
                return;
            }

            Vector3 direction = center - body.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            direction.Normalize();
            Vector3 planarOffset = body.position - anchorPosition;
            planarOffset.y = 0f;
            if (planarOffset.magnitude >= maxKnockbackOffset * 0.98f && Vector3.Dot(planarOffset.normalized, direction) > 0f)
            {
                return;
            }

            body.AddForce(direction * acceleration, ForceMode.Acceleration);
        }

        public void ApplyDamageKnockback(float damage, Vector3 impulseDirection)
        {
            float amount = Mathf.InverseLerp(knockbackMinDamage, knockbackMaxDamage, damage);
            if (amount <= 0f || impulseDirection.sqrMagnitude <= 0.001f)
            {
                return;
            }

            Vector3 direction = impulseDirection;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            direction.Normalize();
            Vector3 planarOffset = body.position - anchorPosition;
            planarOffset.y = 0f;
            if (planarOffset.magnitude >= maxKnockbackOffset * 0.98f && Vector3.Dot(planarOffset.normalized, direction) > 0f)
            {
                amount *= 0.25f;
            }

            body.AddForce(direction * (maxKnockbackImpulse * amount), ForceMode.Impulse);
        }

        private void ApplyAnchorBounds()
        {
            Vector3 position = body.position;
            Vector3 planarOffset = position - anchorPosition;
            planarOffset.y = 0f;
            Vector3 velocity = body.linearVelocity;
            Vector3 planarVelocity = velocity;
            planarVelocity.y = 0f;
            float distance = planarOffset.magnitude;

            if (distance > maxKnockbackOffset && distance > 0.001f)
            {
                Vector3 offsetDirection = planarOffset / distance;
                Vector3 clampedPosition = anchorPosition + offsetDirection * maxKnockbackOffset;
                clampedPosition.y = position.y;
                body.position = clampedPosition;
                position = clampedPosition;
                planarOffset = offsetDirection * maxKnockbackOffset;
                distance = maxKnockbackOffset;

                float outwardVelocity = Vector3.Dot(planarVelocity, offsetDirection);
                if (outwardVelocity > 0f)
                {
                    velocity -= offsetDirection * outwardVelocity;
                    body.linearVelocity = velocity;
                    planarVelocity = velocity;
                    planarVelocity.y = 0f;
                }
            }

            if (distance >= maxKnockbackOffset * 0.96f && distance > 0.001f && distance > maxKnockbackOffset * limitRelaxDistanceFraction)
            {
                Vector3 offsetDirection = planarOffset / distance;
                Vector3 targetPosition = anchorPosition + offsetDirection * (maxKnockbackOffset * limitRelaxDistanceFraction);
                Vector3 toTarget = targetPosition - position;
                toTarget.y = 0f;
                body.AddForce(toTarget * limitReturnSpring - planarVelocity * returnDamping, ForceMode.Acceleration);
            }
        }
    }
}
