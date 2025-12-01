using UnityEngine;

namespace ArcadeVP
{
    public class InputManager_ArcadeVP : MonoBehaviour
    {
        public ArcadeVehicleController arcadeVehicleController;
        public CarTrickController trickController;
        public CarBailHandler bailHandler;

        private Driving controls;

        // driving inputs
        float steerInput;   // horizontal axis (left/right)
        float accelInput;   // vertical axis (forward/back)
        float driftInput;   // reserved if you re-enable drift later

        bool boostHeld;
        bool jumpHeld;

        // trick one-frame flags (buttons)
        bool trickSpinPressed;   // A
        bool trickRollPressed;   // B
        bool trickFlipPressed;   // Y

        [Header("Trick Input")]
        [Tooltip("How far the stick must be tilted to count as a direction.")]
        public float trickStickDeadzone = 0.25f;

        void Awake()
        {
            controls = new Driving();

            // --- Steering (horizontal stick) ---
            controls.DrivingMap.Steer.performed += ctx =>
            {
                steerInput = ctx.ReadValue<float>();
            };
            controls.DrivingMap.Steer.canceled += ctx =>
            {
                steerInput = 0f;
            };

            // --- Acceleration (vertical stick) ---
            controls.DrivingMap.Accel.performed += ctx =>
            {
                accelInput = ctx.ReadValue<float>();
            };
            controls.DrivingMap.Accel.canceled += ctx =>
            {
                accelInput = 0f;
            };

            // --- Boost (hold) ---
            controls.DrivingMap.Boost.performed += ctx => boostHeld = true;
            controls.DrivingMap.Boost.canceled  += ctx => boostHeld = false;

            // --- Jump (hold, for charge jump) ---
            controls.DrivingMap.Jump.performed += ctx => jumpHeld = true;
            controls.DrivingMap.Jump.canceled  += ctx => jumpHeld = false;

            // --- Trick Spin (A) ---
            controls.DrivingMap.TrickSpin.performed += ctx =>
            {
                trickSpinPressed = true;
                //Debug.Log("TrickSpin pressed");
            };

            // --- Trick Roll (B) ---
            controls.DrivingMap.TrickRoll.performed += ctx =>
            {
                trickRollPressed = true;
                //Debug.Log("TrickRoll pressed");
            };

            // --- Trick Flip (Y) ---
            controls.DrivingMap.TrickFlip.performed += ctx =>
            {
                trickFlipPressed = true;
                //Debug.Log("TrickFlip pressed");
            };
        }

        void OnEnable()  => controls.Enable();
        void OnDisable() => controls.Disable();

        void Update()
        {
            if (arcadeVehicleController == null)
                return;

            // If we're in a bail state, freeze control
            if (bailHandler != null && bailHandler.IsBailing)
            {
                arcadeVehicleController.ProvideInputs(0f, 0f, 0f);
                arcadeVehicleController.SetBoost(false);
                arcadeVehicleController.SetJump(false);
                return;
            }

            // ---- DRIVING INPUT ----
            arcadeVehicleController.ProvideInputs(steerInput, accelInput, driftInput);
            arcadeVehicleController.SetBoost(boostHeld);
            arcadeVehicleController.SetJump(jumpHeld);

            // ---- TRICK INPUT (only in air) ----
            bool inAir = !arcadeVehicleController.grounded();

            if (inAir && trickController != null)
            {
                HandleTrickInputs();
            }

            // reset one-frame trick button flags
            trickSpinPressed = false;
            trickRollPressed = false;
            trickFlipPressed = false;
        }

        void HandleTrickInputs()
        {
            float horiz = steerInput; // -1..1 (left/right)
            float vert  = accelInput; // -1..1 (forward/back)

            float dead = trickStickDeadzone;

            // --- A: Yaw Spin (right / left) ---
            if (trickSpinPressed)
            {
                if (Mathf.Abs(horiz) > dead)
                {
                    float dir = Mathf.Sign(horiz); // right = +1, left = -1
                    trickController.TryStartTrick(TrickType.YawSpin, dir);
                    //Debug.Log($"Spin trick, dir = {dir}");
                }
                return;
            }

            // --- B: Barrel Roll (right / left) ---
            if (trickRollPressed)
            {
                if (Mathf.Abs(horiz) > dead)
                {
                    float dir = -Mathf.Sign(horiz); // right roll / left roll
                    trickController.TryStartTrick(TrickType.BarrelRoll, dir);
                    //Debug.Log($"Roll trick, dir = {dir}");
                }
                return;
            }

            // --- Y: Flip (forward / back) ---
            if (trickFlipPressed)
            {
                if (Mathf.Abs(vert) > dead)
                {
                    float dir = Mathf.Sign(vert); // up = +1 (frontflip), down = -1 (backflip)
                    trickController.TryStartTrick(TrickType.Flip, dir);
                    //Debug.Log($"Flip trick, dir = {dir}");
                }

                return;
            }
        }
    }
}
