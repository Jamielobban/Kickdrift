using UnityEngine;
using DG.Tweening;

[System.Serializable]
    public class MovementModule
    {
        [Header("Ground Check")]
        public ArcadeVehicleController.GroundCheckMode groundCheckMode = ArcadeVehicleController.GroundCheckMode.RayCast;
        public LayerMask drivableSurface;
        public float groundRayExtra = 0.3f;

        [Header("Movement")]
        public float MaxSpeed = 60f;
        public float accelaration = 30f;
        public float turn = 8f;
        public AnimationCurve turnCurve;
        public bool kartLike = false;
        public float driftMultiplier = 1.5f;

        [Header("Forces")]
        public float downforce = 5f;
        public float airGravityBase = 9.81f;
        public float airGravityRamp = 2f;
        public float airGravityMaxMultiplier = 3f;
        public bool enableAirUprightAssist = false;
        public float airUprightStrength = 2f;

        // runtime
        [System.NonSerialized] public ArcadeVehicleController car;
        [System.NonSerialized] public float steeringInput;
        [System.NonSerialized] public float accelInput;
        [System.NonSerialized] public float brakeInput;
        [System.NonSerialized] public bool airControlLocked;

        float radius;

        public void Init(ArcadeVehicleController car, float sphereRadius)
        {
            this.car = car;
            radius = sphereRadius;
        }

        public bool UpdateGrounded(out RaycastHit hit)
        {
            // ray from slightly above center along car up
            Vector3 up   = car.carBody.transform.up;
            Vector3 down = -up;

            float maxDist = radius + groundRayExtra;
            Vector3 origin = car.rb.position + up * radius;

            bool hitSomething = false;
            hit = default;

            if (groundCheckMode == ArcadeVehicleController.GroundCheckMode.RayCast)
            {
                hitSomething = Physics.Raycast(origin, down, out hit, maxDist, drivableSurface);
            }
            else
            {
                hitSomething = Physics.SphereCast(origin, radius + 0.1f, down, out hit, maxDist, drivableSurface);
            }

            // debug
            Debug.DrawLine(origin,
                           origin + down * maxDist,
                           hitSomething ? Color.green : Color.red);
            if (hitSomething)
                Debug.DrawRay(hit.point, hit.normal * 0.5f, Color.yellow);

            if (hitSomething)
                car.groundHit = hit;

            return hitSomething;
        }

        public void Tick(float dt, bool onGround)
        {
            float speedRatio = car.carVelocity.magnitude / Mathf.Max(1f, MaxSpeed);
            float turnMul = turnCurve != null
                ? turnCurve.Evaluate(speedRatio)
                : 1f;

            if (kartLike && brakeInput > 0.1f)
                turnMul *= driftMultiplier;

            float turnTorque = steeringInput * turn * 100f * turnMul;

            if (!airControlLocked)
            {
                float wallSteerMul = 1f;
                if (onGround)
                {
                    // 1 on flat ground, ~0.3 on vertical wall
                    wallSteerMul = Mathf.Clamp01(car.groundHit.normal.y + 0.3f);
                }

                car.carBody.AddTorque(car.carBody.transform.up * turnTorque * wallSteerMul);
            }

            if (onGround)
            {
                Vector3 groundNormal = car.groundHit.normal != Vector3.zero ? car.groundHit.normal : car.transform.up;
                car.rb.AddForce(-groundNormal * downforce * car.rb.mass, ForceMode.Acceleration);

                car.carBody.MoveRotation(Quaternion.Slerp(
                    car.carBody.rotation,
                    Quaternion.FromToRotation(car.carBody.transform.up, groundNormal) * car.carBody.rotation,
                    0.12f));
            }
            else
            {
                if (enableAirUprightAssist && airUprightStrength > 0f)
                {
                    Quaternion uprightTarget = Quaternion.FromToRotation(
                        car.carBody.transform.up,
                        Vector3.up
                    ) * car.carBody.rotation;

                    car.carBody.MoveRotation(Quaternion.Slerp(
                        car.carBody.rotation,
                        uprightTarget,
                        airUprightStrength * dt));
                }
            }

            // Acceleration
            float speedBoostMul = car.isBoosting ? car.nos.nosSpeedMultiplier : 1f;
            float accelBoostMul = car.isBoosting ? car.nos.nosAccelMultiplier : 1f;

            if (car.movementMode == ArcadeVehicleController.MovementMode.Velocity)
            {
                if (onGround && Mathf.Abs(accelInput) > 0.1f)
                {
                    Vector3 driveDir = car.carBody.transform.forward;

                    if (car.groundHit.normal != Vector3.zero)
                    {
                        driveDir = Vector3.ProjectOnPlane(driveDir, car.groundHit.normal).normalized;
                    }

                    car.rb.linearVelocity = Vector3.Lerp(
                        car.rb.linearVelocity,
                        driveDir * accelInput * (MaxSpeed * speedBoostMul),
                        (accelaration * accelBoostMul) / 10f * Time.deltaTime);
                }
            }
            else if (car.movementMode == ArcadeVehicleController.MovementMode.AngularVelocity)
            {
                if (Mathf.Abs(accelInput) > 0.1f)
                {
                    car.rb.angularVelocity = Vector3.Lerp(
                        car.rb.angularVelocity,
                        car.carBody.transform.right * accelInput * (MaxSpeed * speedBoostMul) / radius,
                        (accelaration * accelBoostMul) * Time.deltaTime);
                }
            }
        }

        public void ApplyGravity(float dt, bool onGround, ref float airTime)
        {
            if (onGround) return;

            float gravityMul = 1f + airTime * airGravityRamp;
            gravityMul = Mathf.Clamp(gravityMul, 1f, airGravityMaxMultiplier);

            Vector3 gravityForce = Vector3.down * airGravityBase * gravityMul * car.rb.mass;
            car.rb.AddForce(gravityForce, ForceMode.Acceleration);
        }
    }