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
    public float groundRayExtra = 0.3f;    // extra distance past sphere radius

    [Header("Grounding Tuning")]
    [Range(0.7f, 1.0f)] public float castRadiusMul = 0.92f; // shrink casts only
    public float castSkin = 0.03f;                          // tiny extra distance
    public float landGraceTime = 0.06f;                     // seconds (2–4 fixed frames)
    public bool softenImpactOnLand = true;
    public float maxDownVelOnLand = 2f;                     // clamp to -2 m/s

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

    [Header("Edge Clear Assist")]
    public bool edgeClearAssist = true;
    public float edgeAssistTimeWindow = 0.18f; // seconds before landing
    public float edgeAssistStrength = 1.8f;    // m/s tangent push
    public int edgeAssistMissThreshold = 2;    // only when ground missing
    public float edgeAssistWorstDropThreshold = 0.6f; // also treat as edge when drop is huge


    [Header("Edge Clear Assist Timing")]
    public float edgeAssistMinTime = 0.06f;   // don't fire at impact
    public float edgeAssistMaxTime = 0.30f;   // pre-impact window
    public bool edgeAssistOncePerAir = true;

    bool edgeAssistUsedThisAir;
    public void ResetEdgeAssistLatch() => edgeAssistUsedThisAir = false;
    // runtime
    [System.NonSerialized] public ArcadeVehicleController car;
    [System.NonSerialized] public float steeringInput;
    [System.NonSerialized] public float accelInput;
    [System.NonSerialized] public float brakeInput;
    [System.NonSerialized] public bool airControlLocked;
    

    float radius;
    LayerMask groundMask;
    LayerMask sphereMask;
    float lastRayGroundTime;        // last time the ramp ray was grounded

    bool wasGrounded;
    float landTimer;

    public void Init(ArcadeVehicleController car, float sphereRadius)
    {
        this.car = car;
        radius   = sphereRadius;

        groundMask = drivableSurface;
        sphereMask = sphereGroundSurface;
    }

    float CastRadius => radius * castRadiusMul;

    // -------------------------------------------------
    // GROUND CHECK: RAY FIRST, THEN SPHERE FALLBACK
    // -------------------------------------------------
    public bool UpdateGrounded(out RaycastHit hit)
    {
        Vector3 downCar = -car.carBody.transform.up;

        float maxDist    = radius + groundRayExtra;
        Vector3 rayOrigin = car.rb.position;

        // --- 1) ramp ray (car-down) ---
        bool rayHit = Physics.Raycast(
            rayOrigin,
            downCar,
            out RaycastHit rayHitInfo,
            maxDist + castSkin,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        if (rayHit)
        {
            hit = rayHitInfo;

            Debug.DrawLine(rayOrigin, rayOrigin + downCar * (maxDist + castSkin), Color.green);
            Debug.DrawRay(hit.point, hit.normal * 0.5f, Color.yellow);

            car.groundHit = hit;
            lastRayGroundTime = Time.time;
            return true;
        }

        // --- 2) sphere fallback (world-down) ---
        Vector3 sphereOrigin = car.rb.position + Vector3.up * radius;
        Vector3 sphereDown   = Vector3.down;

        bool sphereHit = Physics.SphereCast(
            sphereOrigin,
            CastRadius,
            sphereDown,
            out RaycastHit sphereHitInfo,
            maxDist + castSkin,
            sphereMask,
            QueryTriggerInteraction.Ignore
        );

        if (sphereHit)
        {
            Vector3 vel = car.rb.linearVelocity;

            // clean launch: ignore sphere shortly after ray ground while going up
            if (vel.y > 0f && Time.time - lastRayGroundTime < launchIgnoreTime)
            {
                Debug.DrawLine(sphereOrigin, sphereOrigin + sphereDown * (maxDist + castSkin), Color.red);
                hit = default;
                return false;
            }

            hit = sphereHitInfo;

            Debug.DrawLine(sphereOrigin, sphereOrigin + sphereDown * (maxDist + castSkin), Color.cyan);
            Debug.DrawRay(hit.point, hit.normal * 0.5f, Color.magenta);

            car.groundHit = hit;
            return true;
        }

        hit = default;
        Debug.DrawLine(rayOrigin, rayOrigin + downCar * (maxDist + castSkin), Color.red);
        return false;
    }

    // -------------------------------------------------
    // MOVEMENT / FORCES
    // -------------------------------------------------
    public void Tick(float dt, bool onGround)
    {
        // track landing for grace
        if (!wasGrounded && onGround)
        {
            landTimer = landGraceTime;

            if (softenImpactOnLand)
            {
                Vector3 v = car.rb.linearVelocity;
                if (v.y < -maxDownVelOnLand)
                {
                    v.y = -maxDownVelOnLand;
                    car.rb.linearVelocity = v;
                }
            }
        }

        if (landTimer > 0f) landTimer -= dt;
        wasGrounded = onGround;

        float speedRatio = car.carVelocity.magnitude / Mathf.Max(1f, MaxSpeed);
        float turnMul = turnCurve != null ? turnCurve.Evaluate(speedRatio) : 1f;

        if (kartLike && brakeInput > 0.1f)
            turnMul *= driftMultiplier;

        float turnTorque = steeringInput * turn * 100f * turnMul;

        if (!airControlLocked)
        {
            float wallSteerMul = 1f;
            if (onGround)
                wallSteerMul = Mathf.Clamp01(car.groundHit.normal.y + 0.3f);

            car.carBody.AddTorque(car.carBody.transform.up * turnTorque * wallSteerMul);
        }

        if (onGround)
        {
            Vector3 groundNormal = car.groundHit.normal != Vector3.zero
                ? car.groundHit.normal
                : car.transform.up;

            // soften first-contact forces to avoid “solver bounce”
            float grace01 = (landGraceTime > 0f) ? Mathf.Clamp01(landTimer / landGraceTime) : 0f;

            float align = Mathf.Lerp(0.12f, 0.04f, grace01);      // smaller while grace active
            float dfMul = Mathf.Lerp(1.00f, 0.40f, grace01);      // less downforce while grace active

            car.rb.AddForce(-groundNormal * downforce * dfMul * car.rb.mass, ForceMode.Acceleration);

            car.carBody.MoveRotation(Quaternion.Slerp(
                car.carBody.rotation,
                Quaternion.FromToRotation(car.carBody.transform.up, groundNormal) * car.carBody.rotation,
                align));
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

    public Vector3 GetAirGravity(float airTime)
    {
        float gravityMul = 1f + airTime * airGravityRamp;
        gravityMul = Mathf.Clamp(gravityMul, 1f, airGravityMaxMultiplier);
        return Vector3.down * airGravityBase * gravityMul; // acceleration
    }

    public void ApplyGravity(float dt, bool onGround, ref float airTime)
    {
        if (onGround)
        {
            airTime = 0f;
            return;
        }

        airTime += dt;

        Vector3 gravityAccel = GetAirGravity(airTime);
        car.rb.AddForce(gravityAccel * car.rb.mass, ForceMode.Acceleration);
    }

    // -------------------------------------------------
    // LANDING PREDICTION (still uses same cast radius)
    // -------------------------------------------------
    public struct PredictedLandingData
    {
        public Vector3 point;
        public Vector3 normal;
        public float time;
        public Collider collider;
        public int layer;
        public float surfaceAngle;
        public float impactAngle;
    }

    void DebugDrawWireSphere(Vector3 center, float r, Color col, int seg = 10)
    {
        float step = 360f / seg;

        Vector3 prevXY = center + new Vector3(r, 0, 0);
        Vector3 prevXZ = center + new Vector3(r, 0, 0);
        Vector3 prevYZ = center + new Vector3(0, r, 0);

        for (int i = 1; i <= seg; i++)
        {
            float a = step * i * Mathf.Deg2Rad;

            Vector3 nextXY = center + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0);
            Vector3 nextXZ = center + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
            Vector3 nextYZ = center + new Vector3(0, Mathf.Cos(a) * r, Mathf.Sin(a) * r);

            Debug.DrawLine(prevXY, nextXY, col);
            Debug.DrawLine(prevXZ, nextXZ, col);
            Debug.DrawLine(prevYZ, nextYZ, col);

            prevXY = nextXY; prevXZ = nextXZ; prevYZ = nextYZ;
        }
    }

    public bool TryPredictLanding(
        float currentAirTime,
        out PredictedLandingData data,
        float maxTime = 2f,
        int steps = 80,
        int drawEvery = 8,
        bool drawSpheres = true)
    {
        data = default;

        Vector3 p = car.rb.position;
        Vector3 v = car.rb.linearVelocity;

        float simAirTime = Mathf.Max(0f, currentAirTime);
        float dt = maxTime / Mathf.Max(1, steps);

        LayerMask mask = sphereMask.value != 0 ? sphereMask : groundMask;

        float castR = CastRadius;

        for (int i = 0; i < steps; i++)
        {
            Vector3 g = GetAirGravity(simAirTime);

            Vector3 pNext = p + v * dt + 0.5f * g * dt * dt;
            Vector3 vNext = v + g * dt;

            if (drawSpheres && drawEvery > 0 && (i % drawEvery) == 0)
            {
                Color c = new Color(1f, 0f, 1f, 0.25f);
                DebugDrawWireSphere(p, castR, c);
                Debug.DrawLine(p, pNext, c);
            }

            Vector3 seg = pNext - p;
            float dist = seg.magnitude;

            if (dist > 0.0001f)
            {
                Vector3 dir = seg / dist;

                if (Physics.SphereCast(
                        p, castR, dir,
                        out RaycastHit hit,
                        dist, mask,
                        QueryTriggerInteraction.Ignore))
                {
                    float frac = hit.distance / dist;

                    data.point = hit.point;
                    data.normal = hit.normal;
                    data.time = i * dt + frac * dt;
                    data.collider = hit.collider;
                    data.layer = hit.collider.gameObject.layer;

                    data.surfaceAngle = Vector3.Angle(hit.normal, Vector3.up);
                    data.impactAngle  = Vector3.Angle(hit.normal, car.carBody.transform.up);

                    if (drawSpheres)
                    {
                        DebugDrawWireSphere(hit.point, castR, Color.red);
                        Debug.DrawRay(hit.point, hit.normal * 1.2f, Color.red);
                        Debug.DrawLine(car.rb.position, hit.point, Color.red);
                    }

                    return true;
                }
            }

            p = pNext;
            v = vNext;
            simAirTime += dt;
        }

        return false;
    }

   public bool IsBadLandingEdge(
        in PredictedLandingData land,
        out string reason,
        out int missCount,
        out float worstDrop,
        float ringRadius = 0.45f,
        float rayUp = 0.35f,
        float rayDown = 1.5f,
        float heightDrop = 0.25f,
        int requiredBadSamples = 3)
    {
        reason = "";
        missCount = 0;
        worstDrop = 0f;

        if (land.collider == null)
            return false;

        // same layers as your landing prediction (ground-only if provided)
        LayerMask mask = sphereMask.value != 0 ? sphereMask : groundMask;

        Vector3 n = land.normal.normalized;

        // build a stable tangent basis around the landing normal
        Vector3 t = Vector3.Cross(n, Vector3.up);
        if (t.sqrMagnitude < 0.0001f)
            t = Vector3.Cross(n, car.carBody.transform.right);
        t.Normalize();

        Vector3 b = Vector3.Cross(n, t).normalized;

        Vector3 center = land.point;
        float centerY = center.y;

        int bad = 0;

        // sample 8 points around a ring (works on steep surfaces too)
        for (int i = 0; i < 8; i++)
        {
            float ang = i * (Mathf.PI * 2f / 8f);
            Vector3 offset = (t * Mathf.Cos(ang) + b * Mathf.Sin(ang)) * ringRadius;

            Vector3 origin = center + offset + n * rayUp; // start slightly above surface
            Vector3 dir = -n;                              // ray "into" the surface

            bool ok = Physics.Raycast(
                origin,
                dir,
                out RaycastHit h,
                rayUp + rayDown,
                mask,
                QueryTriggerInteraction.Ignore
            );

            // debug rays in-scene (no console spam)
            Debug.DrawLine(
                origin,
                origin + dir * (rayUp + rayDown),
                ok ? new Color(0f, 1f, 0f, 0.25f) : new Color(1f, 0f, 0f, 0.25f)
            );

            if (!ok)
            {
                missCount++;
                bad++;
                continue;
            }

            float drop = centerY - h.point.y;              // positive = ring point is lower
            worstDrop = Mathf.Max(worstDrop, drop);

            if (drop > heightDrop)
                bad++;
        }


        bool missingGroundEdge = (missCount >= 2);
        bool bigDropEdge = (worstDrop >= edgeAssistWorstDropThreshold);
        bool manyBadSamples = (bad >= requiredBadSamples);

        if (missingGroundEdge || bigDropEdge || manyBadSamples)
        {
            if (missingGroundEdge)
                reason = $"Edge: nearby ground missing (miss={missCount}/8)";
            else if (bigDropEdge)
                reason = $"Edge: big drop nearby (worstDrop={worstDrop:F2}m)";
            else
                reason = $"Edge: unstable area (bad={bad}/8, worstDrop={worstDrop:F2}m)";

            return true;
        }

        return false;
    }

   public void ApplyEdgeClearAssist(
        in PredictedLandingData land,
        int missCount,
        float worstDrop)
    {
        if (!edgeClearAssist) return;

        // fire only once per airtime
        if (edgeAssistOncePerAir && edgeAssistUsedThisAir)
            return;

        // only PRE-impact window (prevents post-bounce correction)
        if (land.time < edgeAssistMinTime) return;
        if (land.time > edgeAssistMaxTime) return;

        // must be a real "bad landing"
        bool triggerByMiss = missCount >= edgeAssistMissThreshold;
        bool triggerByDrop = worstDrop >= edgeAssistWorstDropThreshold;
        if (!triggerByMiss && !triggerByDrop)
            return;

        Rigidbody rb = car.rb;
        Vector3 v = rb.linearVelocity;
        Vector3 n = land.normal.normalized;

        // ----------------------------
        // CORE IDEA:
        // reshape velocity so it SLIDES along the ramp
        // instead of slamming into it
        // ----------------------------

        // remove velocity component going INTO the ramp
        Vector3 slideVel = Vector3.ProjectOnPlane(v, n);

        // downhill direction on the ramp
        Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, n).normalized;

        // if downhill is invalid (almost flat), bail
        if (downhill.sqrMagnitude < 0.001f)
            return;

        // blend sliding direction with downhill bias
        float fallSpeed = Mathf.Max(0f, -Vector3.Dot(v, Vector3.up));
        float bias01 = Mathf.Clamp01(fallSpeed / 20f); // scale with fall speed

        Vector3 desiredVel = Vector3.Lerp(
            slideVel,
            downhill * slideVel.magnitude,
            0.4f + 0.4f * bias01
        );

        // gently reshape velocity (NO snap)
        rb.linearVelocity = Vector3.Lerp(
            rb.linearVelocity,
            desiredVel,
            0.35f
        );

        edgeAssistUsedThisAir = true;

        // -------- DEBUG VIS --------
        Debug.DrawRay(land.point, downhill * 2f, Color.cyan, 0.1f);
        Debug.DrawRay(land.point, slideVel.normalized * 2f, Color.yellow, 0.1f);
    }


}
