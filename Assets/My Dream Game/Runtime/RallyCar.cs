using UnityEngine;

namespace MyDreamGame
{
    internal sealed class RallyCar
    {
        private const float Mass = 1270f;
        private const float Inertia = 2010f;
        private const float FrontAxle = 1.16f;
        private const float RearAxle = 1.43f;
        private const float HalfTrack = 0.76f;
        private const float Gravity = 9.81f;
        private const float AirTemperature = 9f;
        private static readonly float[] Gears = { 3.10f, 2.20f, 1.65f, 1.28f, 1.02f };

        private Vector3 velocity;
        private float yawRate;
        private float steering;
        private float acceleration;
        private float lateralAcceleration;
        private float reverseHold;
        private float shiftCut;
        private float pedal;
        private float brakePressure;

        internal Vector3 Position { get; private set; }
        internal float Heading { get; private set; }
        internal float Distance { get; private set; }
        internal float Lateral { get; private set; }
        internal float Speed => velocity.magnitude;
        internal float Rpm { get; private set; }
        internal int Gear { get; private set; }
        internal float Damage { get; private set; }
        internal float Slip { get; private set; }
        internal float Grip { get; private set; }
        internal float Impact { get; private set; }
        internal float Steering => steering;
        internal float Acceleration => acceleration;
        internal float LeftWater { get; private set; }
        internal float RightWater { get; private set; }
        internal float Gravel { get; private set; }
        internal float SteeringPull { get; private set; }
        internal float BrakeTemperature { get; private set; }
        internal float BrakeFade => 0.29f * Mathf.InverseLerp(325f, 515f, BrakeTemperature);

        internal void Reset(RallyCourse course)
        {
            Distance = 0f;
            Lateral = 0f;
            RallyCourse.Frame start = course.At(0f);
            Position = start.Center;
            Heading = start.Heading;
            velocity = Vector3.zero;
            yawRate = 0f;
            steering = 0f;
            acceleration = 0f;
            lateralAcceleration = 0f;
            reverseHold = 0f;
            shiftCut = 0f;
            pedal = 0f;
            brakePressure = 0f;
            Gear = 1;
            Rpm = 950f;
            Damage = 0f;
            Slip = 0f;
            Grip = 0.77f;
            Impact = 0f;
            LeftWater = 0f;
            RightWater = 0f;
            Gravel = 0f;
            SteeringPull = 0f;
            BrakeTemperature = 125f;
        }

        internal void Step(RallyCourse course, float throttle, float brake, float steeringInput, float dt)
        {
            Impact = 0f;
            Vector3 forward = new Vector3(Mathf.Sin(Heading), 0f, Mathf.Cos(Heading));
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            float u = Vector3.Dot(velocity, forward);
            float v = Vector3.Dot(velocity, right);
            if (throttle == 0f && brake > 0f && u < 0.2f && Mathf.Abs(v) < 0.7f)
            {
                reverseHold += dt;
            }
            else
            {
                reverseHold = 0f;
            }

            bool reversing = reverseHold > 0.65f;
            pedal = Mathf.MoveTowards(pedal, reversing ? -brake : throttle, dt * 7f);
            brakePressure = Mathf.MoveTowards(brakePressure, reversing ? 0f : brake, dt * 11f);
            UpdateGear(u, reversing, dt);
            float speed = Speed;
            float lockAngle = Mathf.Lerp(0.50f, 0.105f, Mathf.InverseLerp(1f, 34f, speed));
            float steerRate = steeringInput == 0f ? 2.1f : 1.3f;
            steering = Mathf.MoveTowards(steering, steeringInput * lockAngle, steerRate * dt);

            RallyCourse.Frame frame = course.At(Distance);
            float forwardAlongRoad = Vector3.Dot(forward, frame.Forward);
            float forwardAcrossRoad = Vector3.Dot(forward, frame.Right);
            float frontDistance = Distance + FrontAxle * forwardAlongRoad;
            float rearDistance = Distance - RearAxle * forwardAlongRoad;
            float frontLateral = Lateral + FrontAxle * forwardAcrossRoad;
            float rearLateral = Lateral - RearAxle * forwardAcrossRoad;
            float halfTrackAlongRoad = -HalfTrack * forwardAcrossRoad;
            float halfTrackAcrossRoad = HalfTrack * forwardAlongRoad;
            RallyCourse.Surface frontLeft = course.SurfaceAt(frontDistance - halfTrackAlongRoad, frontLateral - halfTrackAcrossRoad, speed);
            RallyCourse.Surface frontRight = course.SurfaceAt(frontDistance + halfTrackAlongRoad, frontLateral + halfTrackAcrossRoad, speed);
            RallyCourse.Surface rearLeft = course.SurfaceAt(rearDistance - halfTrackAlongRoad, rearLateral - halfTrackAcrossRoad, speed);
            RallyCourse.Surface rearRight = course.SurfaceAt(rearDistance + halfTrackAlongRoad, rearLateral + halfTrackAcrossRoad, speed);
            LeftWater = Mathf.Max(frontLeft.Water, rearLeft.Water);
            RightWater = Mathf.Max(frontRight.Water, rearRight.Water);
            Gravel = Mathf.Max(Mathf.Max(frontLeft.Gravel, frontRight.Gravel), Mathf.Max(rearLeft.Gravel, rearRight.Gravel));
            Grip = (frontLeft.Grip + frontRight.Grip + rearLeft.Grip + rearRight.Grip) * 0.25f;

            float pitchTransfer = Mass * 0.48f * acceleration / (FrontAxle + RearAxle);
            float rollTransfer = Mass * 0.46f * lateralAcceleration / (2f * HalfTrack);
            float frontLoad = Mathf.Clamp(Mass * Gravity * RearAxle / (FrontAxle + RearAxle) - pitchTransfer, 3300f, 9300f);
            float rearLoad = Mass * Gravity - frontLoad;
            float leftFrontLoad = Mathf.Clamp(frontLoad * 0.5f + rollTransfer * 0.57f, frontLoad * 0.14f, frontLoad * 0.86f);
            float leftRearLoad = Mathf.Clamp(rearLoad * 0.5f + rollTransfer * 0.43f, rearLoad * 0.14f, rearLoad * 0.86f);
            float frontCondition = 1f - Damage * 0.21f;
            float frontLeftLimit = frontLeft.Grip * leftFrontLoad * frontCondition;
            float frontRightLimit = frontRight.Grip * (frontLoad - leftFrontLoad) * frontCondition;
            float rearLeftLimit = rearLeft.Grip * leftRearLoad;
            float rearRightLimit = rearRight.Grip * (rearLoad - leftRearLoad);
            float wheelSpeed = Mathf.Max(2.6f, Mathf.Abs(u));
            float frontSteer = steering + SteeringPull;
            float steerSlip = frontSteer * Mathf.Clamp(u / 2.6f, -1f, 1f);
            float frontSlip = steerSlip - Mathf.Atan2(v + FrontAxle * yawRate, wheelSpeed);
            float rearSlip = -Mathf.Atan2(v - RearAxle * yawRate, wheelSpeed);
            Slip = Mathf.Max(Mathf.Abs(frontSlip), Mathf.Abs(rearSlip)) * Mathf.InverseLerp(1f, 12f, speed);

            float brakeEffort = Mathf.Clamp(u * 2.5f, -1f, 1f) * brakePressure * (1f - BrakeFade);
            float torque = DriveForce();
            float frontRequest = torque * 0.22f - brakeEffort * 4050f;
            float rearRequest = torque * 0.28f - brakeEffort * 2350f;
            float waterDrag = u * Mathf.Abs(u) * 0.34f;
            Vector2 frontLeftForce = TireForces(frontRequest, frontSlip, frontLeftLimit, 26500f, waterDrag * frontLeft.Water);
            Vector2 frontRightForce = TireForces(frontRequest, frontSlip, frontRightLimit, 26500f, waterDrag * frontRight.Water);
            Vector2 rearLeftForce = TireForces(rearRequest, rearSlip, rearLeftLimit, 28500f, waterDrag * rearLeft.Water);
            Vector2 rearRightForce = TireForces(rearRequest, rearSlip, rearRightLimit, 28500f, waterDrag * rearRight.Water);
            Vector2 frontForce = frontLeftForce + frontRightForce;
            Vector2 rearForce = rearLeftForce + rearRightForce;
            float steerSin = Mathf.Sin(frontSteer);
            float steerCos = Mathf.Cos(frontSteer);
            float frontForward = frontForce.x * steerCos - frontForce.y * steerSin;
            float frontSide = frontForce.x * steerSin + frontForce.y * steerCos;
            Vector2 frontDifference = frontLeftForce - frontRightForce;
            float differentialTorque = HalfTrack * (frontDifference.x * steerCos - frontDifference.y * steerSin +
                rearLeftForce.x - rearRightForce.x);
            float brakingForce = Mathf.Min(Mathf.Abs(brakeEffort) * 8100f, frontLeftLimit + frontRightLimit) +
                Mathf.Min(Mathf.Abs(brakeEffort) * 4700f, rearLeftLimit + rearRightLimit);
            float cooling = (BrakeTemperature - AirTemperature) * (0.006f + speed * 0.00055f);
            BrakeTemperature += (brakingForce * Mathf.Abs(u) / 7900f - cooling) * dt;

            float grade = (course.At(Distance + 2f).Center.y - course.At(Distance - 2f).Center.y) * 0.25f;
            Vector3 slopeForce = -Gravity * (frame.Forward * grade + frame.Right * frame.Bank);
            float rolling = u * (17f + 0.58f * Mathf.Abs(u)) + Mathf.Clamp(u * 4f, -1f, 1f) * (145f + 145f * Gravel);
            Vector3 netAcceleration = forward * ((frontForward + rearForce.x - rolling) / Mass) +
                right * ((frontSide + rearForce.y) / Mass) + slopeForce;
            acceleration = Mathf.Lerp(acceleration, Vector3.Dot(netAcceleration, forward), 9f * dt);
            lateralAcceleration = Mathf.Lerp(lateralAcceleration, Vector3.Dot(netAcceleration, right), 10f * dt);
            velocity += netAcceleration * dt;
            yawRate += (FrontAxle * frontSide - RearAxle * rearForce.y + differentialTorque) / Inertia * dt;
            Heading = Mathf.Repeat(Heading + yawRate * dt + Mathf.PI, Mathf.PI * 2f) - Mathf.PI;
            Position += velocity * dt;
            ResolveCourseContact(course);
        }

        private void UpdateGear(float speed, bool reversing, float dt)
        {
            shiftCut = Mathf.Max(0f, shiftCut - dt);
            if (reversing)
            {
                Gear = -1;
            }
            else if (Gear < 0)
            {
                Gear = 1;
            }

            float ratio = Gear < 0 ? 3.0f : Gears[Gear - 1];
            float shaftRpm = Mathf.Abs(speed) * ratio * 121f;
            Rpm = Mathf.Max(950f + 1550f * Mathf.Abs(pedal) * Mathf.InverseLerp(5f, 0f, Mathf.Abs(speed)), shaftRpm);
            if (Gear > 0 && shiftCut == 0f)
            {
                if (Rpm > 6450f && Gear < Gears.Length)
                {
                    Gear++;
                    shiftCut = 0.20f;
                }
                else if (Rpm < 3100f && Gear > 1)
                {
                    Gear--;
                    shiftCut = 0.16f;
                }
            }
        }

        private float DriveForce()
        {
            float torqueCurve = Mathf.Lerp(0.60f, 1f, Mathf.InverseLerp(950f, 4100f, Rpm));
            torqueCurve *= 1f - Mathf.InverseLerp(6000f, 7150f, Rpm);
            float ratio = Gear < 0 ? 3.0f : Gears[Gear - 1];
            float shiftFactor = shiftCut > 0f ? 0.12f : 1f;
            float reverseLimiter = Gear < 0 ? 0.45f * Mathf.InverseLerp(6.5f, 3.5f, Speed) : 1f;
            return pedal * 2540f * ratio * torqueCurve * shiftFactor * reverseLimiter * (1f - 0.7f * Damage);
        }

        private static Vector2 TireForces(float request, float slip, float limit, float stiffness, float waterDrag)
        {
            // Lateral and longitudinal forces share the same contact patch.
            float side = Mathf.Clamp(slip * stiffness, -limit, limit);
            side *= Mathf.Lerp(1f, 0.75f, Mathf.InverseLerp(0.12f, 0.47f, Mathf.Abs(slip)));
            float combined = Mathf.Sqrt(request * request + side * side);
            float scale = limit / Mathf.Max(limit, combined);
            return new Vector2(request * scale - waterDrag, side * scale);
        }

        private void ResolveCourseContact(RallyCourse course)
        {
            float distance = Distance;
            course.Locate(Position, ref distance, out float lateral);
            RallyCourse.Frame frame = course.At(distance);
            float relativeHeading = Heading - frame.Heading;
            float bodyExtent = 0.88f * Mathf.Abs(Mathf.Cos(relativeHeading)) +
                1.92f * Mathf.Abs(Mathf.Sin(relativeHeading));
            float clearance = RallyCourse.BarrierOffset - bodyExtent;
            Vector3 position = Position;
            if (Mathf.Abs(lateral) > clearance)
            {
                float side = Mathf.Sign(lateral);
                Vector3 normal = frame.Right * side;
                float closingSpeed = Mathf.Max(0f, Vector3.Dot(velocity, normal));
                position -= normal * (Mathf.Abs(lateral) - clearance);
                lateral = side * clearance;
                if (closingSpeed > 0.8f)
                {
                    Impact = closingSpeed;
                    float damage = Mathf.Max(0f, closingSpeed - 1.1f) * 0.059f;
                    Damage = Mathf.Clamp01(Damage + damage);
                    float noseIntoRail = side * Mathf.Sin(relativeHeading);
                    float frontShare = Mathf.InverseLerp(-0.23f, 0.58f, noseIntoRail);
                    SteeringPull = Mathf.Clamp(SteeringPull + side * damage * frontShare * 0.074f, -0.048f, 0.048f);
                    velocity = (velocity - normal * (closingSpeed * 1.12f)) * Mathf.Lerp(0.96f, 0.42f, closingSpeed / 15f);
                    yawRate *= 0.35f;
                }
                else
                {
                    velocity = (velocity - normal * closingSpeed) * 0.994f;
                }
            }

            if (distance <= course.Start + 4f)
            {
                distance = course.Start + 4f;
                frame = course.At(distance);
                position = frame.Point(lateral);
                velocity -= frame.Forward * Mathf.Min(0f, Vector3.Dot(velocity, frame.Forward));
            }

            position.y = frame.Point(lateral).y;
            Position = position;
            Distance = distance;
            Lateral = lateral;
        }
    }
}
