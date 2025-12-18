using Sandbox;

public sealed class BombManager : Component
{
    // We can add power-up stats here
    public MapLoader _mapLoader;
    [Property] public PrefabScene bombPrefab;
    [Property] public PrefabScene bombExplosion;
    [Property] private PrefabScene textParticle;
    [Property] private PlayerMovement _playerMovement;
    [Sync] private List<GameObject> bombs { get; set; }

    private const int bombFromGroundOffset = 0;

    // Gameplay variables
    [Sync] private int maxBombCount { get; set; } = 50;
    [Sync] private int bombCount { get; set; } = 0;
    private int radius = 1;
    private float bombDetonationTime = 4.0f;

    protected override void OnStart()
    {
        if (IsProxy) return;
        if (Networking.IsClient)
        {
            Log.Info("BombManager is a client.");
        }
        bombs = new List<GameObject>();
        _playerMovement = GameObject.GetComponent<PlayerMovement>();
        var _mapLoaders = Scene.GetAllComponents<MapLoader>();
        foreach ( var mapLoader in _mapLoaders )
        {
            _mapLoader = mapLoader;
            if ( Networking.IsClient )
                Log.Info( mapLoader );
        }
        if (_mapLoader == null )
        {
            Log.Error( "MapLoader component not found." );
        }
    }

    protected override void OnUpdate()
    {
        // Check if the player presses the jump key and if they can place a bomb
        if ( Input.Pressed( "jump" ) && !_playerMovement.isDead && !IsProxy)
        {
            if ( bombCount < maxBombCount )
            {
                SpawnBomb();
            }
            else
            {
                Log.Info( "Max bomb count reached." );
            }
        }
    }

    private void SpawnBomb()
    {
        if ( bombPrefab == null || IsProxy )
        {
            Log.Error( "Bomb prefab is not assigned." );
            return;
        }

        // Get grid position and ensure the space is empty
        var bombOnGrid = _mapLoader.GetGridPosition( GameObject.WorldPosition );
        if ( _mapLoader.GetGridValue( bombOnGrid ) != "Empty Space" && _mapLoader.GetGridValue( bombOnGrid ) != "Player Spawn" )
        {
            Log.Info( _mapLoader.GetGridValue( new Vector2( 0, 0 ) ) );
            Log.Info( "Cannot place `bomb on non-empty space" );
            return;
        }

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
        bombComponent.SetDetonationTime( bombDetonationTime );
        bombComponent.SetRadius( radius );
        bombCount++;
    }

    // Method to handle bomb detonation (or removal of bombs from the list)
    public void OnBombDetonated( GameObject bomb )
    {
        if ( bomb != null && bombs.Contains( bomb ) )
        {
            bombs.Remove( bomb );
            bombCount--;
        }
    }

    // Power-up methods
    public void IncreaseBombRadius()
    {
        radius++;
        var clone = textParticle.Clone( GameObject.WorldPosition + Vector3.Up * 100, GameObject.WorldRotation );
        clone.GetComponent<ParticleTextRenderer>().Text = new Sandbox.TextRendering.Scope("+1 Radius", color: Color.White, size: 48);
    }

    public void IncreaseBombCount()
    {
        // Increase max bomb count
        maxBombCount++;
        var clone = textParticle.Clone( GameObject.WorldPosition + Vector3.Up * 100, GameObject.WorldRotation );
        clone.GetComponent<ParticleTextRenderer>().Text = new Sandbox.TextRendering.Scope("+1 Bomb", color: Color.White, size: 48);
    }

    public void IncreasePlayerSpeed()
    {
        _playerMovement.Speed *= 1.2f;
        var clone = textParticle.Clone( GameObject.WorldPosition + Vector3.Up * 100, GameObject.WorldRotation );
        clone.GetComponent<ParticleTextRenderer>().Text = new Sandbox.TextRendering.Scope("+0.2 Speed", color: Color.White, size: 48);
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
