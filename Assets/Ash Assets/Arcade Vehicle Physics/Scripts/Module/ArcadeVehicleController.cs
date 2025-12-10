using UnityEngine;
using DG.Tweening;

    public class ArcadeVehicleController : MonoBehaviour
    {
        public enum GroundCheckMode { RayCast, SphereCast }
        public enum MovementMode { Velocity, AngularVelocity };

        [Header("Refs")]
        public Rigidbody rb;          // sphere
        public Rigidbody carBody;     // visual/physics body
        public Transform BodyMesh;
        public Transform[] FrontWheels = new Transform[2];
        public Transform[] RearWheels  = new Transform[2];

        [Header("Skidmarks")]
        public float skidWidth = 0.22f;   

        [Header("Core Movement")]
        public MovementMode movementMode;
        public MovementModule movement = new MovementModule();

        [Header("Drift")]
        public DriftModule drift = new DriftModule();

        [Header("NOS")]
        public NosModule nos = new NosModule();

        [Header("Jump")]
        public JumpModule jump = new JumpModule();

        [Header("Audio")]
        public AudioSource engineSound;
        public float minPitch = 0.8f;
        public float MaxPitch = 2.0f;
        public AudioSource SkidSound;

        [Header("Visual Drift Tilt")]
        public float BodyTilt = 10f;

        // runtime shared state
        [HideInInspector] public RaycastHit groundHit;
        [HideInInspector] public bool isGrounded;
        [HideInInspector] public Vector3 carVelocity;   // local space velocity
        [HideInInspector] public float slip;
        [HideInInspector] public float driftIntensity;
        [HideInInspector] public bool isDrifting;
        [HideInInspector] public bool isBoosting;
        [HideInInspector] public bool boostInput;
        [HideInInspector] public bool hasLaunchedThisJump;   // used by other scripts

        float steeringInput;
        float accelInput;
        float brakeInput;
        public float steeringInputPublic;

        float radius;
        float airTime;
        bool airControlLocked;

        void Awake()
        {
            radius = rb.GetComponent<SphereCollider>().radius;

            // init modules with this car ref
            movement.Init(this, radius);
            drift.Init(this);
            nos.Init(this);
            jump.Init(this);
        }

        void Update()
        {
            Visuals();
            AudioUpdate();
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            // local velocity
            carVelocity = carBody.transform.InverseTransformDirection(carBody.linearVelocity);

            // ground check + basic ground/air state
            isGrounded = movement.UpdateGrounded(out groundHit);

            if (isGrounded)
                airTime = 0f;
            else
                airTime += dt;

            // drift + NOS + jump + movement
            drift.Tick(dt, isGrounded);
            nos.Tick(dt, isGrounded);
            jump.Tick(dt, isGrounded);
            movement.Tick(dt, isGrounded);

            // optional extra gravity
            movement.ApplyGravity(dt, isGrounded, ref airTime);
        }

        // ------------ INPUT API ------------
        public void ProvideInputs(float steer, float accel, float brake)
        {
            steeringInput = steer;
            accelInput = accel;
            brakeInput = brake;
            steeringInputPublic = steer;

            movement.steeringInput = steer;
            movement.accelInput    = accel;
            movement.brakeInput    = brake;
        }

        public void SetBoost(bool held)
        {
            boostInput = held;
            nos.boostInput = held;
        }

        public void SetJump(bool held)
        {
            jump.SetJumpHeld(held);
        }

        public void SetAirControlLocked(bool locked)
        {
            airControlLocked = locked;
            movement.airControlLocked = locked;
        }

        // ------------ AUDIO ------------
        void AudioUpdate()
        {
            if (engineSound != null)
                engineSound.pitch = Mathf.Lerp(minPitch, MaxPitch, Mathf.Abs(carVelocity.z) / movement.MaxSpeed);

            if (SkidSound != null)
                SkidSound.mute = !(Mathf.Abs(carVelocity.x) > 10 && isGrounded);
        }

        // ------------ VISUALS ------------
        void Visuals()
        {
            // front wheels
            foreach (Transform fw in FrontWheels)
            {
                if (!fw) continue;

                fw.localRotation = Quaternion.Slerp(
                    fw.localRotation,
                    Quaternion.Euler(
                        fw.localRotation.eulerAngles.x,
                        steeringInput * 30f,
                        fw.localRotation.eulerAngles.z),
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

            if (!BodyMesh) return;

            // body tilt from steering + speed
            if (carVelocity.z > 1)
            {
                BodyMesh.localRotation = Quaternion.Slerp(
                    BodyMesh.localRotation,
                    Quaternion.Euler(
                        Mathf.Lerp(0, -5, carVelocity.z / movement.MaxSpeed),
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

            // jump squash is handled inside JumpModule (it drives BodyMesh.localPosition)
        }

        public bool grounded()
        {
            // for old scripts that call car.grounded()
            return isGrounded;
        }

        // ------------ DEBUG GUI ------------
        void OnGUI()
        {
            GUI.color = Color.white;
            GUI.Box(new Rect(20, 20, 260, 150), "Curve + NOS Debug");

            float frictionX = Mathf.Abs(carVelocity.x) / Mathf.Max(1f, movement.MaxSpeed);
            float frictionY = drift.frictionCurve != null ? drift.frictionCurve.Evaluate(frictionX) : 0f;
            GUI.Label(new Rect(30, 45, 240, 20), $"Friction X: {frictionX:F2}  Y: {frictionY:F2}");

            float turnX = carVelocity.magnitude / Mathf.Max(1f, movement.MaxSpeed);
            float turnY = movement.turnCurve != null ? movement.turnCurve.Evaluate(turnX) : 0f;
            GUI.Label(new Rect(30, 70, 240, 20), $"Turn X: {turnX:F2}  Y: {turnY:F2}");

            GUI.Label(new Rect(30, 95, 240, 20), $"Fwd: {carVelocity.z:F1}  Side: {carVelocity.x:F1}");

            GUI.Label(new Rect(30, 120, 240, 20), $"NOS: {nos.nosAmount:F0}/{nos.nosMax:F0}  Boost: {isBoosting}");
        }
    }
