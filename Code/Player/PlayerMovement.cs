using System;
using Sandbox;
using Sandbox.Citizen;

public sealed class PlayerMovement : Component, Component.ITriggerListener
{
    [Property] public float GroundControl { get; set; } = 4.0f;
    [Property] public float Speed { get; set; } = 160.0f;
    [Property] public float AirControl { get; set; } = 0.1f;
    [Property] public float MaxForce { get; set; } = 50.0f;

    // Object References
    [Property] public GameObject Body { get; set; }

    // Member Variables
    [Sync] public bool isDead { get; set; } = false;
    private float ragdollTimer;
    public Vector3 WishVelocity = Vector3.Zero;
    private CharacterController characterController;
    private CitizenAnimationHelper animationHelper;

    protected override void OnAwake()
    {
        characterController = Components.Get<CharacterController>();
        animationHelper = Components.Get<CitizenAnimationHelper>();
    }

    protected override void OnUpdate()
    {
        if (isDead)
        {
            HandleDeath();
            return;
        }

        RotateBody();
        UpdateAnimation();
    }

    protected override void OnFixedUpdate()
    {
        BuildWishVelocity();
        Move();
    }

    private void HandleDeath()
    {
        ragdollTimer += Time.Delta;
        if ( ragdollTimer > 2.0f && GameObject.GetComponent<ModelPhysics>() == null )
        {
            Death();
        }
    }

    private void BuildWishVelocity()
    {
        WishVelocity = Vector3.Zero;
        var rot = GameObject.LocalRotation;

        if (isDead) return;

        if (Input.Down("Forward"))
        {
            SetWishVelocity(rot.Forward, 0);
        }
        if (Input.Down("Backward"))
        {
            SetWishVelocity(rot.Forward, 180);
        }
        if (Input.Down("Left"))
        {
            SetWishVelocity(rot.Forward, 90);
        }
        if (Input.Down("Right"))
        {
            SetWishVelocity(rot.Forward, 270);
        }
        else
        {
            UpdateRagdoll();
        }

        WishVelocity = WishVelocity.WithZ(0);
        if (!WishVelocity.IsNearZeroLength) WishVelocity = WishVelocity.Normal;
        WishVelocity *= Speed;
    }

    private void SetWishVelocity(Vector3 direction, float yaw)
    {
        WishVelocity = direction;
        GameObject.WorldRotation = Rotation.FromYaw(yaw);
    }

    private void UpdateRagdoll()
    {
        var ragdoll = GameObject.GetComponent<ModelPhysics>();
        if (ragdoll.IsValid())
        {
            ragdoll.WorldPosition = GameObject.WorldPosition;
            ragdoll.WorldRotation = GameObject.WorldRotation;
        }
    }

    private void Move()
    {
        var gravity = Scene.PhysicsWorld.Gravity;

        if (characterController.IsOnGround)
        {
            ApplyGroundMovement();
        }
        else
        {
            ApplyAirMovement(gravity);
        }

        characterController.Move();
        ApplyGravity(gravity);
    }

    private void ApplyGroundMovement()
    {
        characterController.Velocity = characterController.Velocity.WithZ(0);
        characterController.Accelerate(WishVelocity);
        characterController.ApplyFriction(GroundControl);
    }

    private void ApplyAirMovement(Vector3 gravity)
    {
        characterController.Velocity += gravity * Time.Delta * 0.5f;
        characterController.Accelerate(WishVelocity.ClampLength(MaxForce));
        characterController.ApplyFriction(AirControl);
    }

    private void ApplyGravity(Vector3 gravity)
    {
        if (!characterController.IsOnGround)
        {
            characterController.Velocity += gravity * Time.Delta * 0.5f;
        }
        else
        {
            characterController.Velocity = characterController.Velocity.WithZ(0);
        }
    }

    private void RotateBody()
    {
        if (Body is null || IsProxy) return;

        var targetAngle = new Angles(0, GameObject.WorldRotation.Yaw(), 0).ToRotation();
        Body.WorldRotation = Rotation.FromYaw(targetAngle.Yaw());
    }

    private void UpdateAnimation()
    {
        if (animationHelper is null) return;

        animationHelper.WithWishVelocity(WishVelocity);
        animationHelper.WithVelocity(characterController.Velocity);
        animationHelper.AimAngle = GameObject.WorldRotation;
        animationHelper.IsGrounded = characterController.IsOnGround;
        animationHelper.WithLook(GameObject.WorldRotation.Forward, 1f, 0.75f, 0.5f);
        animationHelper.MoveStyle = CitizenAnimationHelper.MoveStyles.Run;
    }

    public void Death()
    {
        characterController.Velocity = Vector3.Zero;
        characterController.Punch(Vector3.Up * 1000);
        var ragdoll = GameObject.AddComponent<ModelPhysics>();
        var modelRender = GameObject.GetComponentInChildren<SkinnedModelRenderer>();
        if (modelRender.IsValid())
        {
            Log.Info("called");
            ragdoll.Renderer = modelRender;
            ragdoll.Model = modelRender.Model;
        }
    }

    // Collision Stuff
    public void OnTriggerEnter(Collider other)
    {
        if (isDead) return;
        HandlePowerUp(other);
        HandleExplosion(other);
    }

    private void HandlePowerUp(Collider other)
    {
        var powerUp = other.GameObject.GetComponent<PowerUp>();
        if (powerUp != null)
        {
            powerUp.HandlePowerUp();
        }
    }

    private void HandleExplosion(Collider other)
    {
        if (other.GameObject.Tags.Has("Explosion"))
        {
            isDead = true;
            characterController.Punch(Vector3.Up * 3000);
            Body.GetComponent<SkinnedModelRenderer>().PlaybackRate = 0f;
        }
    }

    public void OnTriggerExit(Collider other)
    {
        if (other.GameObject.Tags.Has("bomb"))
        {
            other.GameObject.GetComponent<BoxCollider>().IsTrigger = false;
        }
    }

    [Button("Add force")]
    public void AddForce()
    {
        characterController.Punch(Vector3.Up * 1000);
    }
}
