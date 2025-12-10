using System.Collections;
using UnityEngine;
using DG.Tweening;

namespace ArcadeVP
{
    public class ArcadeVehicleController : MonoBehaviour
    {
        public enum groundCheck { rayCast, sphereCaste };
        public enum MovementMode { Velocity, AngularVelocity };
        public MovementMode movementMode;
        public groundCheck GroundCheck;
        public LayerMask drivableSurface;

        public float MaxSpeed, accelaration, turn, gravity = 7f, downforce = 5f;

        [Header("Drift Settings")]
        public bool kartLike = false;
        public float driftMultiplier = 1.5f;

        [Header("Visual Drift Tilt")]
        public float maxDriftYaw = 20f;
        public float driftVisualLerpSpeed = 5f;
        float currentDriftYaw;

        public Rigidbody rb;
        public Rigidbody carBody;

        [HideInInspector] public RaycastHit hit;

        public AnimationCurve frictionCurve;
        public AnimationCurve turnCurve;
        public PhysicsMaterial frictionMaterial; // use PhysicMaterial if that's what your project has

        [Header("Visuals")]
        public Transform BodyMesh;
        public Transform[] FrontWheels = new Transform[2];
        public Transform[] RearWheels = new Transform[2];
        public float BodyTilt;

        [Header("Skidmarks")]
        public float skidWidth = 0.22f;

        [HideInInspector] public Vector3 carVelocity;

        [Header("Audio")]
        public AudioSource engineSound;
        public float minPitch = 0.8f;
        public float MaxPitch = 2.0f;
        public AudioSource SkidSound;

        // ---------------- NOS ------------------
        [Header("NOS / Boost")]
        public float nosAmount = 0f;
        public float nosMax = 100f;
        public float nosGainRate = 25f;
        public float nosUseRate = 40f;
        public float nosSpeedMultiplier = 1.35f;
        public float nosAccelMultiplier = 1.5f;
        public float minNosToBoost = 5f;

        [HideInInspector] public bool isBoosting;
        [HideInInspector] public bool boostInput;

        // ---------------- Jump Timing / Charge ------------------
        [Header("Jump Timing")]
        public float jumpCooldown = 0.2f;
        public float coyoteTime = 0.15f;          // after leaving ground
        public float jumpBufferTime = 0.15f;      // press slightly before allowed

        [Header("Jump / Boost Gauge")]
        public float jumpBoostMax = 100f;
        public float jumpBoostGainRate = 25f;
        public float jumpBoostMinCost = 20f;
        public float jumpBoostMaxCost = 50f;

        [SerializeField]
        float jumpBoostCurrent = 0f;
        public float JumpBoostNormalized => jumpBoostCurrent / jumpBoostMax;

        [Header("Chargeable Jump")]
        public float minJumpForce = 6f;
        public float maxJumpForce = 14f;
        public float maxChargeTime = 0.7f;        // time to reach max power
        public float preLaunchSquatTime = 0.05f;  // minimal charge before we can actually leave ground

        // internal jump state
        bool isChargingJump;
        float jumpChargeTimer;
        float jumpCooldownTimer;
        float lastGroundedTime;
        float jumpPressBufferTimer;               // buffered press

        // input tracking
        bool jumpHeld;                            // current hold state

        // for tricks
        public bool hasLaunchedThisJump { get; private set; }
        bool wasGroundedLastFrame;

        // ---------------- Jump Visuals ------------------
        [Header("Jump Visuals")]
        public float jumpSquashDistance = 0.25f;  // how much the body lowers when charging
        public float jumpSquashTime = 0.10f;      // how fast it squats
        public float jumpReleaseTime = 0.08f;     // how fast it pops back

        Vector3 bodyMeshBaseLocalPos;
        Tween jumpSquashTween;

        // ---------------- Air-time gravity ramp ------------------
        [Header("Air Gravity")]
        public float airGravityBase = 9.81f;
        public float airGravityRamp = 2.0f;
        public float airGravityMaxMultiplier = 3f;
        float airTime;

        [Header("Air Upright Assist")]
        public float airUprightStrength = 2f;

        // ---------------- States ------------------
        float radius;
        float steeringInput;
        float accelInput;
        float brakeInput;

        Vector3 origin;

        [HideInInspector] public float slip;
        [HideInInspector] public float driftIntensity;
        [HideInInspector] public bool isDrifting;
        [HideInInspector] public float steeringInputPublic;

        bool airControlLocked;

        void Start()
        {
            radius = rb.GetComponent<SphereCollider>().radius;
            if (movementMode == MovementMode.AngularVelocity)
                Physics.defaultMaxAngularSpeed = 100;

            if (BodyMesh != null)
                bodyMeshBaseLocalPos = BodyMesh.localPosition;
        }

        void Update()
        {
            Visuals();
            AudioUpdate();
        }

        public void ProvideInputs(float steer, float accel, float brake)
        {
            steeringInput = steer;
            accelInput = accel;
            brakeInput = brake;
            steeringInputPublic = steer;
        }

        public void SetBoost(bool held) => boostInput = held;

        // Call this with the HELD state from your input (true while button is down)
        public void SetJump(bool held)
        {
            // rising edge: newly pressed this frame -> start/refresh buffer
            if (held && !jumpHeld)
            {
                jumpPressBufferTimer = jumpBufferTime;
            }

            jumpHeld = held;
        }

        public void SetAirControlLocked(bool locked)
        {
            airControlLocked = locked;
        }

        // ---------------- AUDIO ------------------
        void AudioUpdate()
        {
            engineSound.pitch = Mathf.Lerp(minPitch, MaxPitch, Mathf.Abs(carVelocity.z) / MaxSpeed);
            SkidSound.mute = !(Mathf.Abs(carVelocity.x) > 10 && grounded());
        }

        // ---------------- PHYSICS ------------------
        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            carVelocity = carBody.transform.InverseTransformDirection(carBody.linearVelocity);

            ComputeDrift();
            HandleNOS(dt);

            bool onGround = grounded();

            // grounded / air timing
            if (onGround)
            {
                if (!wasGroundedLastFrame)
                {
                    // just landed -> reset jump state for next one
                    hasLaunchedThisJump = false;
                    isChargingJump = false;
                    jumpChargeTimer = 0f;
                    EndJumpChargeVisual();
                }

                lastGroundedTime = Time.time;

                // kill downward velocity when hugging ground
                Vector3 v = rb.linearVelocity;
                if (v.y < 0f)
                {
                    v.y = 0f;
                    rb.linearVelocity = v;
                }

                airTime = 0f;
            }
            else
            {
                airTime += dt;
            }

            // tick timers
            if (jumpPressBufferTimer > 0f)
                jumpPressBufferTimer -= dt;
            if (jumpCooldownTimer > 0f)
                jumpCooldownTimer -= dt;

            HandleJump(onGround, dt);
            ApplyDrivingPhysics(onGround, dt);

            wasGroundedLastFrame = onGround;
        }

        void ComputeDrift()
        {
            float side = Mathf.Abs(carVelocity.x);
            float fwd = Mathf.Abs(carVelocity.z);
            float speed = carVelocity.magnitude;

            slip = side / (fwd + 0.1f);
            driftIntensity = Mathf.Clamp01(Mathf.InverseLerp(0.25f, 0.65f, slip));

            isDrifting = grounded() && speed > 15f && slip > 0.30f;

            if (Mathf.Abs(carVelocity.x) > 0 && frictionCurve != null && frictionMaterial != null)
            {
                frictionMaterial.dynamicFriction =
                    frictionCurve.Evaluate(Mathf.Abs(carVelocity.x / 100));
            }
        }

        // ---------------- NOS ------------------
        void HandleNOS(float dt)
        {
            if (isDrifting)
                nosAmount = Mathf.Clamp(nosAmount + driftIntensity * nosGainRate * dt, 0, nosMax);

            if (boostInput && nosAmount > minNosToBoost && grounded())
            {
                if (!isBoosting)
                    GameSignals.RaiseBoostStarted();

                isBoosting = true;
                nosAmount -= nosUseRate * dt;
                if (nosAmount <= 0)
                {
                    nosAmount = 0;
                    isBoosting = false;
                    GameSignals.RaiseBoostEnded();
                }
            }
            else
            {
                if (isBoosting)
                    GameSignals.RaiseBoostEnded();
                isBoosting = false;
            }
        }

        // ---------------- CHARGEABLE JUMP ------------------
        void HandleJump(bool onGround, float dt)
        {
            // can we start a new charge? (grounded OR within coyote window)
            bool withinCoyote = Time.time - lastGroundedTime <= coyoteTime;
            bool canStartNewCharge = onGround || withinCoyote;

            bool bufferedPress = jumpPressBufferTimer > 0f;

            // --- START CHARGE ---
            if (bufferedPress && !isChargingJump && jumpCooldownTimer <= 0f && canStartNewCharge)
            {
                isChargingJump = true;
                jumpChargeTimer = 0f;
                jumpPressBufferTimer = 0f; // consume buffer

                StartJumpChargeVisual();
            }

            // --- CHARGING ---
            if (isChargingJump)
            {
                jumpChargeTimer += dt;

                float charge01 = Mathf.Clamp01(jumpChargeTimer / maxChargeTime);
                float jumpForceThisFrame = Mathf.Lerp(minJumpForce, maxJumpForce, charge01);

                float jumpCost = Mathf.Lerp(jumpBoostMinCost, jumpBoostMaxCost, charge01);
                //Debug.Log(jumpCost);

                bool readyToLaunch = jumpChargeTimer >= preLaunchSquatTime;
                bool released = !jumpHeld;

                // require enough NOS to jump at all (optional but recommended)
                if (readyToLaunch && released)
                {
                    if (nosAmount < jumpBoostMinCost)
                    {
                        // not enough energy to jump, just cancel charge
                        isChargingJump = false;
                        jumpChargeTimer = 0f;
                        EndJumpChargeVisual();
                        return;
                    }

                    DoJump(jumpForceThisFrame, jumpCost);
                    EndJumpChargeVisual();
                }

                // tap too fast before squat finished -> cancel charge + visuals
                if (!jumpHeld && jumpChargeTimer < preLaunchSquatTime)
                {
                    isChargingJump = false;
                    jumpChargeTimer = 0f;
                    EndJumpChargeVisual();
                }

                // optional: if we left the ground very early during the tiny squat, cancel
                if (!onGround && jumpChargeTimer < preLaunchSquatTime)
                {
                    isChargingJump = false;
                    jumpChargeTimer = 0f;
                    EndJumpChargeVisual();
                }
            }
        }

       void DoJump(float force, float cost)
        {
            jumpCooldownTimer = jumpCooldown;

            Vector3 vel = rb.linearVelocity;
            if (vel.y < 0) vel.y = 0;
            vel.y += force;
            rb.linearVelocity = vel;

            // spend NOS as energy
            nosAmount -= cost;
            if (nosAmount < 0f) nosAmount = 0f;

            hasLaunchedThisJump = true;
            isChargingJump = false;
            jumpChargeTimer = 0f;
        }

        // ---------------- JUMP VISUAL HELPERS ------------------
        void StartJumpChargeVisual()
        {
            if (BodyMesh == null) return;

            if (jumpSquashTween != null && jumpSquashTween.IsActive())
                jumpSquashTween.Kill();

            Vector3 targetPos = bodyMeshBaseLocalPos + Vector3.down * jumpSquashDistance;

            jumpSquashTween = BodyMesh.DOLocalMove(
                targetPos,
                jumpSquashTime
            ).SetEase(Ease.OutQuad);
        }

        void EndJumpChargeVisual()
        {
            if (BodyMesh == null) return;

            if (jumpSquashTween != null && jumpSquashTween.IsActive())
                jumpSquashTween.Kill();

            jumpSquashTween = BodyMesh.DOLocalMove(
                bodyMeshBaseLocalPos,
                jumpReleaseTime
            ).SetEase(Ease.OutQuad);
        }

        // ---------------- DRIVING PHYSICS ------------------
        void ApplyDrivingPhysics(bool onGround, float dt)
        {
            float speedRatio = carVelocity.magnitude / Mathf.Max(1f, MaxSpeed);
            float turnMul = turnCurve != null
                ? turnCurve.Evaluate(speedRatio)
                : 1f;

            if (kartLike && brakeInput > 0.1f)
                turnMul *= driftMultiplier;

            float turnTorque = steeringInput * turn * 100f * turnMul;
            if (!airControlLocked)
            {
                carBody.AddTorque(Vector3.up * turnTorque);
            }

            if (onGround)
            {
                rb.AddForce(-transform.up * downforce * rb.mass);

                carBody.MoveRotation(Quaternion.Slerp(
                    carBody.rotation,
                    Quaternion.FromToRotation(carBody.transform.up, hit.normal) * carBody.rotation,
                    0.12f));
            }
            else
            {
                // upright assist
                Quaternion uprightTarget = Quaternion.FromToRotation(
                    carBody.transform.up,
                    Vector3.up
                ) * carBody.rotation;

                carBody.MoveRotation(Quaternion.Slerp(
                    carBody.rotation,
                    uprightTarget,
                    airUprightStrength * dt));

                // progressive gravity
                float gravityMul = 1f + airTime * airGravityRamp;
                gravityMul = Mathf.Clamp(gravityMul, 1f, airGravityMaxMultiplier);

                Vector3 gravityForce = Vector3.down * airGravityBase * gravityMul * rb.mass;
                rb.AddForce(gravityForce, ForceMode.Acceleration);
            }

            // Acceleration
            float speedBoostMul = isBoosting ? nosSpeedMultiplier : 1f;
            float accelBoostMul = isBoosting ? nosAccelMultiplier : 1f;

            if (movementMode == MovementMode.Velocity)
            {
                if (Mathf.Abs(accelInput) > 0.1f)
                {
                    rb.linearVelocity = Vector3.Lerp(
                        rb.linearVelocity,
                        carBody.transform.forward * accelInput * (MaxSpeed * speedBoostMul),
                        (accelaration * accelBoostMul) / 10f * Time.deltaTime);
                }
            }
            else if (movementMode == MovementMode.AngularVelocity)
            {
                if (Mathf.Abs(accelInput) > 0.1f)
                {
                    rb.angularVelocity = Vector3.Lerp(
                        rb.angularVelocity,
                        carBody.transform.right * accelInput * (MaxSpeed * speedBoostMul) / radius,
                        (accelaration * accelBoostMul) * Time.deltaTime);
                }
            }
        }

        // ---------------- VISUALS ------------------
        void Visuals()
        {
            // front wheels
            foreach (Transform fw in FrontWheels)
            {
                if (!fw) continue;

                fw.localRotation = Quaternion.Slerp(
                    fw.localRotation,
                    Quaternion.Euler(fw.localRotation.eulerAngles.x, steeringInput * 30f, fw.localRotation.eulerAngles.z),
                    0.7f * Time.deltaTime / Time.fixedDeltaTime);

                if (fw.childCount > 0)
                    fw.GetChild(0).localRotation = rb.transform.localRotation;
            }

            // rear wheels
            if (RearWheels.Length == 2)
            {
                if (RearWheels[0]) RearWheels[0].localRotation = rb.transform.localRotation;
                if (RearWheels[1]) RearWheels[1].localRotation = rb.transform.localRotation;
            }

            // body tilt only (rotation; position handled by jump visuals)
            if (carVelocity.z > 1)
            {
                BodyMesh.localRotation = Quaternion.Slerp(
                    BodyMesh.localRotation,
                    Quaternion.Euler(
                        Mathf.Lerp(0, -5, carVelocity.z / MaxSpeed),
                        BodyMesh.localRotation.eulerAngles.y,
                        BodyTilt * steeringInput),
                    0.4f * Time.deltaTime / Time.fixedDeltaTime);
            }
            else
            {
                BodyMesh.localRotation = Quaternion.Slerp(
                    BodyMesh.localRotation,
                    Quaternion.Euler(0, 0, 0),
                    0.4f * Time.deltaTime / Time.fixedDeltaTime);
            }
        }

        // ---------------- GROUND CHECK ------------------
        public bool grounded()
        {
            origin = rb.position + radius * Vector3.up;
            float maxDist = radius + 0.2f;

            if (GroundCheck == groundCheck.rayCast)
            {
                return Physics.Raycast(rb.position, Vector3.down, out hit, maxDist, drivableSurface);
            }
            else if (GroundCheck == groundCheck.sphereCaste)
            {
                return Physics.SphereCast(
                    origin,
                    radius + 0.1f,
                    Vector3.down,
                    out hit,
                    maxDist,
                    drivableSurface
                );
            }

            return false;
        }

        // ---------------- DEBUG GUI ------------------
        void OnGUI()
        {
            GUI.color = Color.white;
            GUI.Box(new Rect(20, 20, 260, 150), "Curve + NOS Debug");

            float frictionX = Mathf.Abs(carVelocity.x) / Mathf.Max(1f, MaxSpeed);
            float frictionY = frictionCurve != null ? frictionCurve.Evaluate(frictionX) : 0f;
            GUI.Label(new Rect(30, 45, 240, 20), $"Friction X: {frictionX:F2}  Y: {frictionY:F2}");

            float turnX = carVelocity.magnitude / Mathf.Max(1f, MaxSpeed);
            float turnY = turnCurve != null ? turnCurve.Evaluate(turnX) : 0f;
            GUI.Label(new Rect(30, 70, 240, 20), $"Turn X: {turnX:F2}  Y: {turnY:F2}");

            GUI.Label(new Rect(30, 95, 240, 20), $"Fwd: {carVelocity.z:F1}  Side: {carVelocity.x:F1}");

            GUI.Label(new Rect(30, 120, 240, 20), $"NOS: {nosAmount:F0}/{nosMax:F0}  Boost: {isBoosting}");
        }
    }
}
