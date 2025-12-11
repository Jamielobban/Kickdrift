using UnityEngine;
using UnityEngine.ProBuilder.Shapes;

[System.Serializable]
public class MovementModule
{
    [Header("Ground Check")]
    public ArcadeVehicleController.GroundCheckMode groundCheckMode =
        ArcadeVehicleController.GroundCheckMode.RayCast;
    public LayerMask drivableSurface;
    public LayerMask sphereGroundSurface;  // for sphere fallback (ground only)

    public float groundRayExtra = 0.3f;   // extra distance past sphere radius

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

    [Header("Launch Delay")]
    [Tooltip("After losing the ramp ray, ignore sphere hits for this long so you cleanly launch.")]
    public float launchIgnoreTime = 0.12f;

    // runtime
    [System.NonSerialized] public ArcadeVehicleController car;
    [System.NonSerialized] public float steeringInput;
    [System.NonSerialized] public float accelInput;
    [System.NonSerialized] public float brakeInput;
    [System.NonSerialized] public bool airControlLocked;

    float radius;
    LayerMask groundMask;
    LayerMask sphereMask;
    float lastRayGroundTime;   // last time the ramp ray was grounded

    public void Init(ArcadeVehicleController car, float sphereRadius)
    {
        this.car = car;
        radius   = sphereRadius;

        groundMask = drivableSurface;
        sphereMask = sphereGroundSurface;
    }

    // -------------------------------------------------
    // GROUND CHECK: RAY FIRST, THEN SPHERE FALLBACK
    // -------------------------------------------------
    public bool UpdateGrounded(out RaycastHit hit)
    {
        // --- 1) primary raycast: follows the ramp (good takeoff behaviour) ---
        Vector3 upCar   = car.carBody.transform.up;
        Vector3 downCar = -upCar;

        float maxDist   = radius + groundRayExtra;
        Vector3 rayOrigin = car.rb.position;

        bool rayHit = Physics.Raycast(
            rayOrigin,
            downCar,
            out RaycastHit rayHitInfo,
            maxDist,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        if (rayHit)
        {
            hit = rayHitInfo;

            Debug.DrawLine(rayOrigin, rayOrigin + downCar * maxDist, Color.green);
            Debug.DrawRay(hit.point, hit.normal * 0.5f, Color.yellow);

            car.groundHit = hit;
            lastRayGroundTime = Time.time;
            return true;
        }

        // --- 2) fallback spherecast: world-down, good for sideways / roof landings ---
        Vector3 sphereOrigin = car.rb.position + Vector3.up * radius;
        Vector3 sphereDown   = Vector3.down;

        bool sphereHit = Physics.SphereCast(
            sphereOrigin,
            radius + 0.1f,
            sphereDown,
            out RaycastHit sphereHitInfo,
            maxDist,
            sphereMask,
            QueryTriggerInteraction.Ignore
        );

        if (sphereHit)
        {
            Vector3 vel = car.rb.linearVelocity;

            // if we're still moving upward, ignore this hit – we’re launching off something
            if (vel.y > 0f && Time.time - lastRayGroundTime < launchIgnoreTime)
            {
                Debug.DrawLine(sphereOrigin, sphereOrigin + sphereDown * maxDist, Color.red);
                hit = default;
                return false;
            }

            // accept the sphere hit (sideways / roof etc.)
            hit = sphereHitInfo;

            Debug.DrawLine(sphereOrigin, sphereOrigin + sphereDown * maxDist, Color.cyan);
            Debug.DrawRay(hit.point, hit.normal * 0.5f, Color.magenta);

            car.groundHit = hit;
            return true;
        }

        // --- 3) nothing hit ---
        hit = default;
        Debug.DrawLine(rayOrigin, rayOrigin + downCar * maxDist, Color.red);
        return false;
    }

    // -------------------------------------------------
    // MOVEMENT / FORCES
    // -------------------------------------------------
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
                // 1 on flat, ~0.3 on vertical walls
                wallSteerMul = Mathf.Clamp01(car.groundHit.normal.y + 0.3f);
            }

            car.carBody.AddTorque(car.carBody.transform.up * turnTorque * wallSteerMul);
        }

        if (onGround)
        {
            Vector3 groundNormal = car.groundHit.normal != Vector3.zero
                ? car.groundHit.normal
                : car.transform.up;

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
                    driveDir = Vector3.ProjectOnPlane(driveDir, car.groundHit.normal).normalized;

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
