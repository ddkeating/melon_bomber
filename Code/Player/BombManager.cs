using Sandbox;
using System;

public sealed class BombManager : Component
{
    // We can add power-up stats here
    public MapLoader _mapLoader;
    [Property] public PrefabScene bombPrefab;
    [Property] public PrefabScene bombExplosion;
    [Property] private PrefabScene textParticle { get; set; }
	private PlayerMovement _playerMovement;
    [Sync] private List<GameObject> bombs { get; set; }

    private const int bombFromGroundOffset = 0;
	public enum BombType
	{
		Standard,
		Remote,
		Atomic
	}
	[Property] public BombType CurrentBombType { get; set; } = BombType.Standard;

	// Gameplay variables
	public int MaxBombCount { get; set; } = 1;
    private int BombCount { get; set; } = 0;
    public int Radius { get; set; } = 1;
	public int MoveSpeedUpgrades { get; set; } = 1;
	private float BombDetonationTime { get; set; } = 4.0f;

    protected override void OnStart()
    {
		_playerMovement = GameObject.GetComponent<PlayerMovement>();
		if (Network.IsProxy) return;
        bombs = new List<GameObject>();

    }

    protected override void OnUpdate()
    {
		if (_playerMovement == null )
		{
			Log.Info( "PlayerMovement component not found." );
		}
		
		// Check if the player presses the jump key and if they can place a bomb
		if ( Input.Pressed( "jump" ) && !_playerMovement.IsDead && !Network.IsProxy)
        {
			if ( BombCount < MaxBombCount )
            {

				SpawnBomb();
            }
        }
		
    }

    private void SpawnBomb()
    {
		if (_mapLoader == null)
		{
			_mapLoader = Scene.GetAllComponents<MapLoader>().FirstOrDefault();
		}
		if (_mapLoader == null || !_mapLoader.IsMapReady )
		{
			Log.Error("MapLoader is not ready.");
			return;
		}
		if ( bombPrefab == null)
        {
            Log.Error( "Bomb prefab is not assigned." );
            return;
        }

        // Get grid position and ensure the space is empty
        var bombOnGrid = _mapLoader.GetGridPosition( GameObject.WorldPosition );
        if ( _mapLoader.GetGridValue( bombOnGrid ) != MapLoader.GridCellType.Empty && _mapLoader.GetGridValue( bombOnGrid ) != MapLoader.GridCellType.PlayerSpawn )
            return;

        // Calculate the bomb position based on the grid
        Vector3 bombPosition = new Vector3( _mapLoader.GetWorldPosition( bombOnGrid ).x, _mapLoader.GetWorldPosition( bombOnGrid ).y, bombFromGroundOffset );

        // Clone the bomb prefab
        var bomb = bombPrefab.Clone( position: bombPosition );
        bomb.NetworkSpawn();
        if ( bomb == null )
        {
            Log.Error( "Failed to clone bomb prefab." );
            return;
        }

        // Add the bomb to the bombs list and ensure it's not null
        bombs.Add( bomb );
        var bombComponent = bomb.GetComponent<Bomb>();

        if ( bombComponent == null )
        {
            Log.Error( "Bomb component not found on the cloned bomb." );
            return;
        }

        // Set the detonation time and increase bomb count
        bombComponent.SetDetonationTime( BombDetonationTime );
        bombComponent.SetRadius( Radius );
        BombCount++;
    }

    // Method to handle bomb detonation (or removal of bombs from the list)

    public void OnBombDetonated( GameObject bomb )
    {
        if ( bomb != null && bombs.Contains( bomb ) )
        {
            bombs.Remove( bomb );
            BombCount--;
        }
    }



	// Power-up methods
	public void IncreaseBombRadius()
    {
		if ( Radius >= MapLoader.GridSize)
		{
			var clone = textParticle.Clone( GameObject.WorldPosition + Vector3.Up * 100, GameObject.WorldRotation );
			clone.GetComponent<ParticleTextRenderer>().Text = new Sandbox.TextRendering.Scope("Max Radius Reached", color: Color.Red, size: 128);
		}
		else
		{
			Radius++;
			var clone = textParticle.Clone( GameObject.WorldPosition + Vector3.Up * 100, GameObject.WorldRotation );
			clone.GetComponent<ParticleTextRenderer>().Text = new Sandbox.TextRendering.Scope("+1 Radius", color: Color.White, size: 128);
		}
    }

    public void IncreaseBombCount()
    {
		if ( MaxBombCount >= 10 )
		{
			var clone = textParticle.Clone( GameObject.WorldPosition + Vector3.Up * 100, GameObject.WorldRotation );
			clone.GetComponent<ParticleTextRenderer>().Text = new Sandbox.TextRendering.Scope("Max Bombs Reached", color: Color.Red, size: 128);
			return;
		}
		else
		{
			// Increase max bomb count
			MaxBombCount++;
			var clone = textParticle.Clone( GameObject.WorldPosition + Vector3.Up * 100, GameObject.WorldRotation );
			clone.GetComponent<ParticleTextRenderer>().Text = new Sandbox.TextRendering.Scope( "+1 Bomb", color: Color.White, size: 128 );
		}

    }

    public void IncreasePlayerSpeed()
    {
		if ( MoveSpeedUpgrades >= 7 )
		{
			var clone = textParticle.Clone( GameObject.WorldPosition + Vector3.Up * 100, GameObject.WorldRotation );
			clone.GetComponent<ParticleTextRenderer>().Text = new Sandbox.TextRendering.Scope("Max Speed Reached", color: Color.Red, size: 128);
			return;
		}
		else
		{
			_playerMovement.Speed *= 1.15f;
			MoveSpeedUpgrades++;
			var clone = textParticle.Clone( GameObject.WorldPosition + Vector3.Up * 100, GameObject.WorldRotation );
			clone.GetComponent<ParticleTextRenderer>().Text = new Sandbox.TextRendering.Scope( "+0.2 Speed", color: Color.White, size: 128 );
		}

    }

    [Button( "Increase Radius" )]
    private void IncreaseRadius()
    {
        IncreaseBombRadius();
    }

    [Button( "Increase Bomb Count" )]
    private void IncreaseBomb()
    {
        IncreaseBombCount();
    }
}
