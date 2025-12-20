using Sandbox;
using Sandbox.Citizen;
using System;
using static Sandbox.ModelPhysics;

public sealed class PlayerMovement : Component, Component.ITriggerListener
{
	// === CONFIG ===
	[Property] public float Speed { get; set; } = 200f;
	[Property] public GameObject Body { get; set; }

	// === STATE ===
	[Sync] public bool IsDead { get; set; }

	private Vector2 currentGridPos;
	private Vector2 targetGridPos;
	private bool isMoving;
	private bool canMove = false;

	private float ragdollTimer;
	private Vector2 deathGridPos;
	private const float RagdollDuration = 2.5f;
	private const float PlayerZ = 0f;

	private ModelPhysics ragdoll;


	private MapLoader mapLoader;
	private CitizenAnimationHelper anim;
	private CharacterController characterController;

	// === SETUP ===
	protected override void OnStart()
	{
		mapLoader = Scene.GetAllComponents<MapLoader>().FirstOrDefault();
		anim = Components.Get<CitizenAnimationHelper>();
		characterController = Components.Get<CharacterController>();
	}

	// === INPUT ===
	protected override void OnUpdate()
	{
		if ( mapLoader == null || !mapLoader.IsMapReady || IsDead ) return;

		if ( !canMove )
		{
			InitializeGridPosition();
			canMove = true;
		}

		HandleInput();
		UpdateAnimation();
		RotateBody();
	}

	// === MOVEMENT ===
	protected override void OnFixedUpdate()
	{
		if ( IsDead )
		{
			UpdateRagdoll();
			return;
		}

		if ( !isMoving ) return;
		MoveTowardsTarget();
	}


	private void HandleInput()
	{
		if ( isMoving ) return;

		Vector2 dir = Vector2.Zero;

		if ( Input.Down( "Forward" ) ) dir = new Vector2( 1, 0 ); // W -> up
		if ( Input.Down( "Backward" ) ) dir = new Vector2( -1, 0 ); // S -> down
		if ( Input.Down( "Left" ) ) dir = new Vector2( 0, -1 );    // A -> left
		if ( Input.Down( "Right" ) ) dir = new Vector2( 0, 1 );    // D -> right

		if ( dir == Vector2.Zero ) return;

		Vector2 nextCell = currentGridPos + dir;

		if ( !CanMoveTo( nextCell ) ) return;

		targetGridPos = nextCell;
		Log.Info( $"Target Grid Pos: {mapLoader.GetWorldPosition(targetGridPos)}" );
		isMoving = true;
	}


	private bool CanMoveTo( Vector2 gridPos )
	{
		if ( !mapLoader.IsValidGridPosition(gridPos)) return false;
		var value = mapLoader.GetGridValue( gridPos );
		return value == MapLoader.GridCellType.Empty || value == MapLoader.GridCellType.PowerUp || value == MapLoader.GridCellType.PlayerSpawn;
	}

	private void MoveTowardsTarget()
	{
		if ( IsDead ) return;
		Vector3 targetWorldPos = mapLoader.GetWorldPosition( targetGridPos ).WithZ(0);

		Vector3 delta = targetWorldPos - GameObject.WorldPosition;
		float step = Speed * Time.Delta;

		if ( delta.Length <= step )
		{
			GameObject.WorldPosition = targetWorldPos;
			currentGridPos = targetGridPos;
			isMoving = false;
			return;
		}

		GameObject.WorldPosition += delta.Normal * step;
	}


	// === HELPERS ===
	private void SnapToGrid()
	{
		if ( IsDead ) return;
		Vector3 gridPos = mapLoader.GetWorldPosition( currentGridPos ).WithZ(0);


		GameObject.WorldPosition = gridPos;
	}


	private void InitializeGridPosition()
	{
		currentGridPos = mapLoader.GetGridPosition( GameObject.WorldPosition );
		targetGridPos = currentGridPos;
		SnapToGrid();
	}


	private void RotateBody()
	{
		if ( Body == null ) return;

		Vector2 dir = targetGridPos - currentGridPos;
		if ( dir == Vector2.Zero ) return;

		float yaw = dir switch
		{
			{ x: 1, y: 0 } => 0,    // up
			{ x: -1, y: 0 } => 180,   // down
			{ x: 0, y: 1 } => 270,  // right
			{ x: 0, y: -1 } => 90,    // left
			_ => 0
		};


		Body.WorldRotation = Rotation.FromYaw( yaw );
	}

	private void UpdateAnimation()
	{
		if ( anim == null ) return;

		anim.WithVelocity( isMoving ? Body.WorldRotation.Forward * Speed : Vector3.Zero );
		anim.WithWishVelocity( isMoving ? Vector3.Forward * Speed : Vector3.Zero );
		anim.IsGrounded = true;
		anim.MoveStyle = CitizenAnimationHelper.MoveStyles.Run;
	}

	public void OnTriggerEnter( Collider other ) 
	{ 
		if ( IsDead ) return; 

		HandlePowerUp( other ); 
		HandleExplosion( other ); 
	}

	private void HandlePowerUp( Collider other ) 
	{ 
		var powerUp = other.GameObject.GetComponent<PowerUp>(); 

		if ( powerUp != null ) 
		{ 
			powerUp.HandlePowerUp(); 
		} 
	}
	private void HandleExplosion( Collider other ) 
	{ 
		if ( other.GameObject.Tags.Has( "Explosion" ) ) 
		{ 
			Kill();
		} 
	}


	// === DEATH ===
	public void Kill()
	{
		IsDead = true;

		deathGridPos = currentGridPos;

		anim?.Enabled = false;

		// Spawn ragdoll
		ragdoll = GameObject.AddComponent<ModelPhysics>();

		var renderer = GameObject.GetComponentInChildren<SkinnedModelRenderer>();
		if ( renderer != null )
		{
			ragdoll.Model = renderer.Model;
			ragdoll.Renderer = renderer;
		}
		Log.Info( $"Ragdoll model: {ragdoll.Bodies}" );

		// Launch upward: apply velocity to all ragdoll bodies
		foreach ( var body in ragdoll.Bodies )
		{
			body.Component.Velocity += Vector3.Up * 700f;
		}
	}

	private void UpdateRagdoll()
	{
		ragdollTimer += Time.Delta;

		if ( ragdoll != null )
		{
			// Keep ragdoll roughly above death spot (optional polish)
			ragdoll.WorldPosition =
				ragdoll.WorldPosition.WithX(
					mapLoader.GetWorldPosition( deathGridPos ).x
				).WithY(
					mapLoader.GetWorldPosition( deathGridPos ).y
				);
		}
	}

	[Button("Revive")]
	private void Revive()
	{
		if ( !IsDead ) return;
		IsDead = false;
		ragdollTimer = 0f;
		// Remove ragdoll
		if ( ragdoll != null )
		{
			GameObject.GetComponent<ModelPhysics>().Destroy();
			ragdoll = null;
		}
		// Reset position
		InitializeGridPosition();
		// Re-enable animation
		anim?.Enabled = true;
	}






}
