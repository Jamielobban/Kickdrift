using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

[System.Serializable]
public class GrindModule
{
    [System.NonSerialized] public ArcadeVehicleController car;

    [Header("State")]
    public bool isGrinding;
    GrindRail rail;

    // Spline param [0..1]
    float t;
    float splineLength;
    float grindSpeed;

    // Hybrid balance meter [-1..+1]
    float balance;
    float wobbleSeed;

    // last computed (for visuals/debug)
    float lateralSigned;

    [Header("Speed")]
    public float minGrindSpeed = 8f;
    public float maxGrindSpeed = 45f;
    public float accelAlongRail = 18f;
    public float brakeDamp = 10f;

    [Header("Balance Mini-Game (Hybrid)")]
    [Tooltip("How fast steering corrects balance.")]
    public float balanceCorrection = 1.8f;

    [Tooltip("How much random wobble pushes balance.")]
    public float wobbleStrength = 1.0f;

    [Tooltip("Wobble frequency.")]
    public float wobbleFreq = 2.2f;

    [Tooltip("How much lateral error pushes balance (hybrid part).")]
    public float lateralToBalance = 2.0f;

    [Tooltip("Fail when |balance| > 1.")]
    public float balanceFail = 1.0f;

    [Header("Bail / Exit")]
    public float exitUpImpulse = 3.0f;
    public float exitSideImpulse = 2.5f;

    [Header("Visual Tilt")]
    [Tooltip("Roll (degrees) of the BODY MESH while balancing.")]
    public float maxVisualRoll = 18f;

    // Internal
    bool wantExit;

    public void Init(ArcadeVehicleController car)
    {
        this.car = car;
        wobbleSeed = UnityEngine.Random.value * 1000f;
        Debug.Log(wobbleSeed);
    }

    public void RequestExit() => wantExit = true;

    public void TryEnter(GrindRail newRail)
    {
        if (!newRail || !newRail.container) return;

        rail = newRail;
        isGrinding = true;
        wantExit = false;

        // Find nearest t to current sphere position
        t = FindNearestT(rail.container, car.rb.position);

        // Cache spline length (approx)
        splineLength = Mathf.Max(0.01f, (float)rail.container.CalculateLength());

        // Initialize grind speed from current velocity projected on tangent
        Vector3 tangent = GetTangentWorld(rail.container, t);
        float proj = Vector3.Dot(car.rb.linearVelocity, tangent);
        if (!rail.allowReverse) proj = Mathf.Abs(proj);

        grindSpeed = Mathf.Clamp(Mathf.Abs(proj), minGrindSpeed, maxGrindSpeed) * Mathf.Sign(proj == 0 ? 1 : proj);

        // Reset balance to neutral on entry (or keep a little of existing lean if you prefer)
        balance = 0f;
    }

    public void Exit(bool failed)
    {
        if (!isGrinding) return;

        // Preserve forward tangent speed, add hop + sideways kick based on balance
        Vector3 tangent = GetTangentWorld(rail.container, t);
        if (!rail.allowReverse) tangent = tangent.normalized * Mathf.Sign(grindSpeed);

        Vector3 up = Vector3.up;

        // Side direction relative to tangent
        Vector3 side = Vector3.Cross(up, tangent).normalized;

        float sideSign = Mathf.Sign(balance == 0f ? lateralSigned : balance);
        Vector3 v = tangent.normalized * Mathf.Clamp(Mathf.Abs(grindSpeed), minGrindSpeed, maxGrindSpeed);

        v += up * exitUpImpulse;
        v += side * (exitSideImpulse * sideSign);

        car.rb.linearVelocity = v;

        // Clear state
        rail = null;
        isGrinding = false;
        wantExit = false;
        lateralSigned = 0f;
        balance = 0f;
    }

    public void Tick(float dt, bool onGround)
    {
        if (!isGrinding || rail == null || rail.container == null)
            return;

        // Optional: if you only want grinding while airborne, you can force exit on ground
        // if (onGround) Exit(false);

        // ---- Inputs -> while grinding, steer is "balance", accel/brake adjust speed ----
        float steer = car.movement.steeringInput;
        float accel = car.movement.accelInput;
        float brake = car.movement.brakeInput;

        if (wantExit || brake > 0.85f)
        {
            Exit(false);
            return;
        }

        // 1) Advance along spline by speed
        float absSpeed = Mathf.Clamp(Mathf.Abs(grindSpeed), minGrindSpeed, maxGrindSpeed);
        absSpeed += accelAlongRail * Mathf.Clamp01(accel) * dt;
        absSpeed -= brakeDamp * Mathf.Clamp01(brake) * dt;
        absSpeed = Mathf.Clamp(absSpeed, minGrindSpeed, maxGrindSpeed);

        float dir = Mathf.Sign(grindSpeed);
        if (!rail.allowReverse) dir = 1f;

        grindSpeed = absSpeed * dir;

        float dtT = (absSpeed / splineLength) * dt;
        t += dtT * dir;

        if (rail.loop)
        {
            t = Mathf.Repeat(t, 1f);
        }
        else
        {
            if (t <= 0f || t >= 1f)
            {
                Exit(false);
                return;
            }
            t = Mathf.Clamp01(t);
        }

        // 2) Compute target point & tangent on spline
        Vector3 targetPos = GetPositionWorld(rail.container, t);
        Vector3 tangent = GetTangentWorld(rail.container, t);

        if (tangent.sqrMagnitude < 0.0001f)
            tangent = car.carBody.transform.forward;

        tangent.Normalize();

        // 3) Compute lateral error (signed)
        Vector3 toRail = targetPos - car.rb.position;

        Vector3 up = Vector3.up;
        Vector3 side = Vector3.Cross(up, tangent).normalized;

        // signed lateral (meters)
        lateralSigned = Vector3.Dot(toRail, side);
        float lateralAbs = Mathf.Abs(lateralSigned);

        // 4) Snap sphere to rail centerline (physics position correction)
        float snap = 1f - Mathf.Exp(-rail.snapStrength * dt);
        car.rb.MovePosition(Vector3.Lerp(car.rb.position, targetPos, snap));

        // 5) Velocity forced along tangent (keeps it “rail locked”)
        car.rb.linearVelocity = tangent * (absSpeed * dir);

        // 6) Hybrid balance update (wobble + lateral pushes + player correction)
        float wobble = (Mathf.PerlinNoise(wobbleSeed, Time.time * wobbleFreq) - 0.5f) * 2f; // [-1..1]
        float speed01 = Mathf.InverseLerp(minGrindSpeed, maxGrindSpeed, absSpeed);

        float lateral01 = Mathf.Clamp(lateralSigned / Mathf.Max(0.001f, rail.maxLateralError), -1f, 1f);

        float push =
            wobble * wobbleStrength * (0.4f + 0.6f * speed01)
          + lateral01 * lateralToBalance;

        float correct = -steer * balanceCorrection;

        balance += (push + correct) * dt;

        // Clamp slightly beyond for “oh no” feeling, fail at threshold
        balance = Mathf.Clamp(balance, -1.2f, 1.2f);

        // 7) Fail conditions (hybrid)
        if (Mathf.Abs(balance) > balanceFail || lateralAbs > rail.maxLateralError)
        {
            Exit(true);
            return;
        }

        // 8) Visual tilt (BodyMesh only) – do NOT tilt physics RB
        if (car.BodyMesh != null)
        {
            float roll = balance * maxVisualRoll;

            // preserve existing yaw/pitch from your visuals, only inject roll
            var e = car.BodyMesh.localEulerAngles;
            car.BodyMesh.localRotation = Quaternion.Euler(e.x, e.y, roll);
        }

        // 9) Rotate the carBody to face along the rail (optional but looks good)
        Quaternion targetRot = Quaternion.LookRotation(tangent, up);
        float a = 1f - Mathf.Exp(-14f * dt);
        car.carBody.MoveRotation(Quaternion.Slerp(car.carBody.rotation, targetRot, a));
    }

    // -----------------------------
    // Nearest point helpers
    // -----------------------------
    float FindNearestT(SplineContainer c, Vector3 worldPos)
    {
        // Convert to spline local space
        float3 local = c.transform.InverseTransformPoint(worldPos);

        // Get nearest point on the spline in local space
        // Unity Splines API: SplineUtility.GetNearestPoint(Spline, float3, out float3, out float)
        var spline = c.Spline;

        SplineUtility.GetNearestPoint(spline, local, out float3 nearest, out float nearestT);

        return Mathf.Clamp01(nearestT);
    }

    Vector3 GetPositionWorld(SplineContainer c, float t)
    {
        float3 local = c.EvaluatePosition(t);
        return c.transform.TransformPoint((Vector3)local);
    }

    Vector3 GetTangentWorld(SplineContainer c, float t)
    {
        float3 localTan = c.EvaluateTangent(t);
        return c.transform.TransformDirection((Vector3)localTan);
    }
}
