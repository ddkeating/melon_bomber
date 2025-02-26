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
    [Property] public GameObject Head { get; set; }
    [Property] public GameObject Body { get; set; }

    // Member Variables
    public bool isDead = false;
    private float ragdollTimer;
    public Vector3 WishVelocity = Vector3.Zero;
    private CharacterController characterController;
    private CitizenAnimationHelper animationHelper;
    protected override void OnAwake()
    {
        if (Network.IsProxy) return;
        characterController = Components.Get<CharacterController>();
        animationHelper = Components.Get<CitizenAnimationHelper>();
    }

    protected override void OnUpdate()
    {
        {
            if ( isDead )
            {
                ragdollTimer += Time.Delta;
                if ( ragdollTimer > 2.0f && GameObject.GetComponent<ModelPhysics>() == null )
                {
                    Death();
                }
                return;
            }
            RotateBody();
        }
        if ( isDead ) return;
        UpdateAnimation();
    }

    protected override void OnFixedUpdate()
    {
        BuildWishVelocity();
        Move();
    }

    void BuildWishVelocity()
{
    WishVelocity = Vector3.Zero;

    var rot = GameObject.LocalRotation;

    // Determine direction based on input.
    if ( !isDead )
    {
        // Only move forward or backward (no simultaneous forward and backward).
        if ( Input.Down( "Forward" ) )
        {
            WishVelocity = rot.Forward;
            Head.LocalRotation = Rotation.FromYaw( 0 );
        }
        else if ( Input.Down( "Backward" ) )
        {
            WishVelocity = rot.Backward;
            Head.LocalRotation = Rotation.FromYaw( 180 );
        }

        // Only move left or right (no simultaneous left and right).
        if ( Input.Down( "Left" ) )
        {
            WishVelocity = rot.Left;
            Head.LocalRotation = Rotation.FromYaw( 90 );
        }
        else if ( Input.Down( "Right" ) )
        {
            WishVelocity = rot.Right;
            Head.LocalRotation = Rotation.FromYaw( 270 );
        }
    }
    else
    {
        var ragdoll = GameObject.GetComponent<ModelPhysics>();
        if ( ragdoll.IsValid() )
        {
            ragdoll.WorldPosition = GameObject.WorldPosition;
            ragdoll.WorldRotation = GameObject.WorldRotation;
        }
    }

    // Zero out the vertical component (keep it on the horizontal plane).
    WishVelocity = WishVelocity.WithZ( 0 );

    // If the velocity is not zero, normalize it.
    if ( !WishVelocity.IsNearZeroLength ) WishVelocity = WishVelocity.Normal;

    // Apply speed.
    WishVelocity *= Speed;
}



    void Move()
    {
        var gravity = Scene.PhysicsWorld.Gravity;

        if ( characterController.IsOnGround )
        {
            // Apply Friction/Acceleration
            characterController.Velocity = characterController.Velocity.WithZ( 0 );
            characterController.Accelerate( WishVelocity );
            characterController.ApplyFriction( GroundControl );
        }
        else
        {
            // Apply Air Control / Gravity
            characterController.Velocity += gravity * Time.Delta * 0.5f;
            characterController.Accelerate( WishVelocity.ClampLength( MaxForce ) );
            characterController.ApplyFriction( AirControl );
        }

        // Move the character controller
        characterController.Move();

        if ( !characterController.IsOnGround )
        {
            characterController.Velocity += gravity * Time.Delta * 0.5f;
        }
        else
        {
            characterController.Velocity = characterController.Velocity.WithZ( 0 );
        }

    }

    void RotateBody()
    {
        if ( Body is null ) return;

        var targetAngle = new Angles( 0, Head.WorldRotation.Yaw(), 0 ).ToRotation();
        Body.WorldRotation = Rotation.FromYaw( targetAngle.Yaw() );
    }

    void UpdateAnimation()
    {
        if ( animationHelper is null ) return;

        animationHelper.WithWishVelocity( WishVelocity );
        animationHelper.WithVelocity( characterController.Velocity );
        animationHelper.AimAngle = Head.WorldRotation;
        animationHelper.IsGrounded = characterController.IsOnGround;
        animationHelper.WithLook( Head.WorldRotation.Forward, 1f, 0.75f, 0.5f );
        animationHelper.MoveStyle = CitizenAnimationHelper.MoveStyles.Run;
    }

    public void Death()
    {
        characterController.Punch( Vector3.Up * 1000 );
        var ragdoll = GameObject.AddComponent<ModelPhysics>();
        var modelRender = GameObject.GetComponentInChildren<SkinnedModelRenderer>();
        if ( modelRender.IsValid() )
        {
            Log.Info( "called" );
            ragdoll.Renderer = modelRender;
            ragdoll.Model = modelRender.Model;
        }
    }

    // Collision Stuff
    public void OnTriggerEnter( Collider other )
    {
        if ( isDead ) return;
        if ( other.GameObject.GetComponent<PowerUp>() != null )
        {
            other.GameObject.GetComponent<PowerUp>().HandlePowerUp();
        }
        if ( other.GameObject.Tags.Has( "Explosion" ) )
        {
            isDead = true;
            characterController.Punch( Vector3.Up * 3000 );
            Body.GetComponent<SkinnedModelRenderer>().PlaybackRate = 0f;
        }
    }

    public void OnTriggerExit( Collider other )
    {
        if ( other.GameObject.Tags.Has( "bomb" ) )
        {
            other.GameObject.GetComponent<BoxCollider>().IsTrigger = false;
        }
    }

    [Button( "Add force" )]
    public void AddForce()
    {
        characterController.Punch( Vector3.Up * 1000 );
    }
}
