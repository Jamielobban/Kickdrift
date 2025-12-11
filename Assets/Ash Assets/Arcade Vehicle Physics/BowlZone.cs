using UnityEngine;

    [RequireComponent(typeof(Collider))]
    public class BowlZone : MonoBehaviour
    {
        [Tooltip("Center of the bowl. If null, uses this transform position.")]
        public Transform bowlCenter;

        [Tooltip("How strong the bowl gravity is.")]
        public float bowlGravity = 15f;

        [Tooltip("How much radial velocity to damp each second (0..1).")]
        public float radialDamp = 0.5f;

        public Vector3 GetCenter()
        {
            return bowlCenter ? bowlCenter.position : transform.position;
        }

        // Called from the car's movement when in-air inside the bowl
        public void ApplyBowlGravity(Rigidbody rb, float dt)
        {
            Vector3 center = GetCenter();
            Vector3 toBody = rb.position - center;

            if (toBody.sqrMagnitude < 0.0001f)
                return;

            Vector3 radialDir = toBody.normalized;  // from center -> car
            Vector3 gravityDir = -radialDir;       // pull back toward center

            // custom gravity toward bowl center
            rb.AddForce(gravityDir * bowlGravity, ForceMode.Acceleration);

            // damp radial velocity so we stay on an arc instead of just falling in
            Vector3 v = rb.linearVelocity;
            float radialVel = Vector3.Dot(v, radialDir);
            v -= radialDir * radialVel * radialDamp * dt;
            rb.linearVelocity = v;
        }

        // For rotation: which way should "up" be at this point?
        public Vector3 GetUpDirection(Vector3 worldPos)
        {
            Vector3 center = GetCenter();
            Vector3 toBody = worldPos - center;
            if (toBody.sqrMagnitude < 0.0001f)
                return Vector3.up;

            return toBody.normalized; // away from bowl center
        }
    }

