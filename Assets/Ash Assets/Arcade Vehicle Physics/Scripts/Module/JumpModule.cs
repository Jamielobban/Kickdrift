using UnityEngine;
using DG.Tweening;

[System.Serializable]
public class JumpModule
{
    [Header("Jump Timing")]
    public float jumpCooldown = 0.2f;
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.15f;

    [Header("Chargeable Jump")]
    public float minJumpForce = 6f;
    public float maxJumpForce = 14f;
    public float maxChargeTime = 0.7f;
    public float preLaunchSquatTime = 0.05f;

    [Header("Jump Visuals")]
    public float jumpSquashDistance = 0.25f;
    public float jumpSquashTime = 0.10f;
    public float jumpReleaseTime = 0.08f;

    [System.NonSerialized] public ArcadeVehicleController car;

    float jumpCooldownTimer;
    float lastGroundedTime;
    float jumpPressBufferTimer;
    float jumpChargeTimer;
    bool isChargingJump;
    bool jumpHeld;
    bool wasGroundedLastFrame;

    Tween jumpSquashTween;
    Vector3 bodyMeshBaseLocalPos;
    bool basePosCached;

    public void Init(ArcadeVehicleController car)
    {
        this.car = car;
    }

    public void SetJumpHeld(bool held)
    {
        if (held && !jumpHeld)
        {
            jumpPressBufferTimer = jumpBufferTime;
        }

        jumpHeld = held;
    }

    public void Tick(float dt, bool onGround)
    {
        // cache base body position once
        if (!basePosCached && car.BodyMesh != null)
        {
            bodyMeshBaseLocalPos = car.BodyMesh.localPosition;
            basePosCached = true;
        }

        // landing detection to reset hasLaunchedThisJump
        if (onGround && !wasGroundedLastFrame)
        {
            car.hasLaunchedThisJump = false;
        }

        if (onGround)
            lastGroundedTime = Time.time;

        if (jumpPressBufferTimer > 0f)
            jumpPressBufferTimer -= dt;
        if (jumpCooldownTimer > 0f)
            jumpCooldownTimer -= dt;

        bool withinCoyote = Time.time - lastGroundedTime <= coyoteTime;
        bool canStartNewCharge = onGround || withinCoyote;

        bool bufferedPress = jumpPressBufferTimer > 0f;

        // start charge
        if (bufferedPress && !isChargingJump && jumpCooldownTimer <= 0f && canStartNewCharge)
        {
            isChargingJump = true;
            jumpChargeTimer = 0f;
            jumpPressBufferTimer = 0f;

            StartJumpChargeVisual();
        }

        if (isChargingJump)
        {
            jumpChargeTimer += dt;

            float charge01 = Mathf.Clamp01(jumpChargeTimer / maxChargeTime);
            float jumpForceThisFrame = Mathf.Lerp(minJumpForce, maxJumpForce, charge01);

            bool readyToLaunch = jumpChargeTimer >= preLaunchSquatTime;
            bool released = !jumpHeld;

            if (readyToLaunch && released)
            {
                DoJump(jumpForceThisFrame);
                EndJumpChargeVisual();
            }

            if (!jumpHeld && jumpChargeTimer < preLaunchSquatTime)
            {
                CancelCharge();
            }

            if (!onGround && jumpChargeTimer < preLaunchSquatTime)
            {
                CancelCharge();
            }
        }

        wasGroundedLastFrame = onGround;
    }

    void CancelCharge()
    {
        isChargingJump = false;
        jumpChargeTimer = 0f;
        EndJumpChargeVisual();
    }

    void DoJump(float force)
    {
        jumpCooldownTimer = jumpCooldown;

        Vector3 vel = car.rb.linearVelocity;
        if (vel.y < 0) vel.y = 0;
        vel.y += force;
        car.rb.linearVelocity = vel;

        car.hasLaunchedThisJump = true;  // <-- this is what your other scripts expect

        isChargingJump = false;
        jumpChargeTimer = 0f;
    }

    void StartJumpChargeVisual()
    {
        if (!car.BodyMesh) return;

        if (jumpSquashTween != null && jumpSquashTween.IsActive())
            jumpSquashTween.Kill();

        Vector3 targetPos = bodyMeshBaseLocalPos + Vector3.down * jumpSquashDistance;

        jumpSquashTween = car.BodyMesh.DOLocalMove(
            targetPos,
            jumpSquashTime
        ).SetEase(Ease.OutQuad);
    }

    void EndJumpChargeVisual()
    {
        if (!car.BodyMesh) return;

        if (jumpSquashTween != null && jumpSquashTween.IsActive())
            jumpSquashTween.Kill();

        jumpSquashTween = car.BodyMesh.DOLocalMove(
            bodyMeshBaseLocalPos,
            jumpReleaseTime
        ).SetEase(Ease.OutQuad);
    }
}