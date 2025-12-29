using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Sandbox;
using Sandbox.Services.Players;

public sealed class MapLoader : Component, Component.INetworkListener
{
	#region Variables
	#region Editor Variables and References
	[Property, Title("Power Up Prefabs"), Category("Prefab References")] private List<PrefabScene> _powerUpPrefabs = new List<PrefabScene>();
    [Property, Title("Wall Prefabs"), Category("Prefab References")] private List<PrefabScene> _wallPrefabs = new List<PrefabScene>();
    [Property, Title("Player Spawn Prefabs"), Category("Prefab References")] private PrefabScene _playerSpawnPrefab { get; set; }

    [Property, Title("Map Entry Sound"), Category("Sound References")] private SoundEvent _swoshSound { get; set; }
    [Property, Title("Map Landed Sound"), Category("Sound References")] private SoundEvent _explosionSound { get; set; }

	#endregion

	#region Animation Editor Variables
	[Property, Title( "Intro Animation Selection" ), Category( "Animation Settings" )] private GridIntroAnimation _introAnimation = GridIntroAnimation.Circle;
	[Property, Title( "Intro Animation Speed" ), Category( "Animation Settings" )] private float _animationSpeed = 1f;

	[Property, Title( "Game Start Smoke" ), Category( "Particle References" )] private GameObject _SmokePlayer { get; set; }
	[Property, Title( "Single Tile Smoke" ), Category( "Particle References" )] private GameObject _singleSmoke { get; set; }

	#endregion

	#region Private References
	/// <summary>
	/// A key value reference of grid positions to gameobjects on that grid position.
	/// </summary>
	private NetDictionary<Vector2, GameObject> _tileObjects = new();


	public GameObject tile;

	private GameObject _camera;
	private CameraShake _cameraShake;
	#endregion



	#region Constants
	// Properties
	private const float scaleFactor = 0.1825f;
    public const int GridSize = 13;

    public const int targetZPosition = 25;
    private const int startingZPosition = 1000;

	private const int PlayerCount = 4;
	[Property, Title("Maximum Powerups"), Category("Gameplay Settings")]private int MaxPowerUpCount = 2;

	private static readonly HashSet<(int, int)> lampPositions = new()
	{
		(3,1), (1,3), (3,11), (1,9),
		(11,3), (9,1), (11,9), (9,11),
		(5,5), (5,7), (7,5), (7,7)
	};

	private static readonly HashSet<(int, int)> EmptyPositions = new()
	{
		(1, 0),
		(0, 1),
		(GridSize - 2, 0),
		(GridSize - 1, 1),
		(0, GridSize - 2),
		(1, GridSize - 1),
		(GridSize - 2, GridSize - 1),
		(GridSize - 1, GridSize - 2)
	};

	private static readonly HashSet<(int, int)> PlayerSpawnPositions = new()
	{
		(0, 0),
		(0, GridSize - 1),
		(GridSize - 1, 0),
		(GridSize - 1, GridSize - 1)
	};



	#endregion

	#region Gameplay Variables
	private List<Vector2> _emptySpaces = new List<Vector2>();
    [Sync] private NetDictionary<Vector2, Vector3> gridWorldPositions { get; set; } = [];
	[Sync] private NetList<GridCellType> grid { get; set; } = new();

	private Vector3 StartingGridCell = new Vector3(-352, 352, startingZPosition);
	#endregion


	#region Map Start Variables
	private int _powerUpIndex = 0;
    public int _powerUpCount = 0;
    private int _playerIndex = 0;

    private bool _hasGeneratedGridPositions = false;
    private bool _startGeneratingGridPositions = false;
	[Sync] public bool IsMapReady { get; private set; } = false;

	private bool animationPlayed = false;
	private bool _soundOnePlayed = false;

	private bool tileAnimationPlayed => tile == null;

	#endregion


	#region Power Up Spawn Variables
	private float _spawnPowerUpTimer = 0f;
    private int _spawnInterval;

	#endregion


	#region Tile References

	public enum GridCellType
	{
		Empty,
		Wall,
		IndestructibleWall,
		PlayerSpawn,
		Bomb,
		PowerUp
	}

	private enum GridWallType
	{
		Wall,
		IndestructibleWall,
		Lamp
	};

	private Dictionary<GridWallType, PrefabScene> _gridReference;

	#endregion

	#region Animation Control Variables

	private enum GridIntroAnimation
	{
		Drop,   // Current default animation
		Wave,   // Column by column
		Circle  // Circular pattern
	}


	// Time Tracking for animations
	private float _waveTime = 0f;
	private float _circleTime = 0f;

	#endregion
	#endregion
	private static int GridIndex( int x, int y ) => x + y * GridSize;


	#region Engine Methods
	protected override void OnStart()
    {
		_gridReference = new Dictionary<GridWallType, PrefabScene>
		{
			{
				GridWallType.Wall, _wallPrefabs[0]
			},
			{
				GridWallType.IndestructibleWall, _wallPrefabs[1]
			},
			{
				GridWallType.Lamp, _wallPrefabs[2]
			}
		};

        if (!Networking.IsHost) return;

		LocalPosition = new Vector3( GameObject.LocalPosition.x, GameObject.LocalPosition.y, 0 );

		if ( Networking.IsHost )
		{
			grid.Clear();
			for ( int i = 0; i < GridSize * GridSize; i++ )
				grid.Add( GridCellType.Empty );
		}


		// Assignments
		_camera = Scene.Camera.GameObject;
		_cameraShake = _camera.GetComponent<CameraShake>();

		if ( _cameraShake == null )
		{
			Log.Error( "Camera Shake component not found." );
		}

		_SmokePlayer.Enabled = false;
		_singleSmoke.Enabled = false;
	}

    protected override void OnUpdate()
    {
        if ( !GameManager.StartGame || !Networking.IsHost ) return;

        if ( !_startGeneratingGridPositions && !_hasGeneratedGridPositions )
        {
            GenerateMap();
            _startGeneratingGridPositions = true;
        }


		HandlePowerUpSpawning();
	}

	protected override void OnFixedUpdate()
	{
		if ( !GameManager.StartGame || !Networking.IsHost ) return;

		if ( _hasGeneratedGridPositions && !animationPlayed )
		{
			if ( !_soundOnePlayed )
			{
				var sound = GameObject.AddComponent<SoundPointComponent>();
				if ( sound != null )
				{
					sound.SoundEvent = _swoshSound;
					sound.StartSound();
				}
				_soundOnePlayed = true;
				RpcSendGridPositions( gridWorldPositions.Keys.ToArray(), gridWorldPositions.Values.ToArray(), grid.ToArray());

			}
			StartingAnimation();
		}

		if ( !tileAnimationPlayed )
			SingleTileAnimation();
	}

	#endregion


	#region Map Generation Methods
	private void GenerateMap()
    {
        Random rnd = new Random();

        // Loop through the grid positions for generation
        for ( int x = 0; x < GridSize; x++ )
        {
            for ( int y = 0; y < GridSize; y++ )
            {
                // Handle player spawns in predefined corners
                if ( IsPlayerSpawn( x, y ) )
                {
                    grid[GridIndex(x, y)] = GridCellType.PlayerSpawn;
                    PlacePrefabAt( x, y, _playerSpawnPrefab );
                    _playerIndex++;
                    continue;
                }

                // Handle empty spaces that are not walls or power-ups
                if ( IsEmptySpace( x, y ) )
                {
                    grid[GridIndex(x, y)] = GridCellType.Empty;
                    _emptySpaces.Add( new Vector2( x, y ) );
                    gridWorldPositions[new Vector2( x, y )] = CalculateGridPlacement( x, y );
                    continue;
                }

                // Handle wall and power-up generation
                HandleWallAndPowerUpGeneration( rnd, x, y );
            }
        }

        // Place power-ups in random empty spaces
        PlacePowerUps( rnd );

        _hasGeneratedGridPositions = true;
    }

    

    private void HandleWallAndPowerUpGeneration(Random rnd, int x, int y)
    {

        if ( x % 2 == 1 && y % 2 == 1 )
        {
            if (lampPositions.Contains((x, y)))
                PlacePrefabAt( x, y, _gridReference[GridWallType.Lamp] );
            else
            {
                PlacePrefabAt(x, y, _gridReference[GridWallType.IndestructibleWall]);
            }
            grid[GridIndex(x, y)] = GridCellType.IndestructibleWall;
            return;
        }
        {
            float randomValue = rnd.Float(0f, 1f);

			// 40% chance for empty space, 60% chance for wall
			if ( randomValue < 0.4f)
            {
                grid[GridIndex(x, y)] = GridCellType.Empty;
                _emptySpaces.Add(new Vector2(x, y));
                gridWorldPositions[new Vector2(x, y)] = CalculateGridPlacement(x, y);
            }
            else if (randomValue < 1f ) // 60% chance for wall
			{
                grid[GridIndex(x, y)] = GridCellType.Wall;
                PlacePrefabAt(x, y, _wallPrefabs[0]);
            }
        }
    }

	#endregion


	#region Gameloop Methods

	private void PlacePowerUps(Random rnd)
    {
        for (; _powerUpCount < MaxPowerUpCount; IncreasePowerUpCount())
        {
            Random randomPowerUp = new Random();
            _powerUpIndex = randomPowerUp.Next(0, _powerUpPrefabs.Count);
            int randomIndex = rnd.Next(0, _emptySpaces.Count);
            Vector2 randomEmptySpace = _emptySpaces[randomIndex];
            PlacePrefabAt((int)randomEmptySpace.x, (int)randomEmptySpace.y, _powerUpPrefabs[_powerUpIndex]);
            grid[GridIndex((int)randomEmptySpace.x, (int)randomEmptySpace.y)] = GridCellType.PowerUp;
            _emptySpaces.RemoveAt(randomIndex);
        }
    }

	private GameObject PlacePrefabAt( int x, int y, PrefabScene prefab )
	{
		Vector3 worldPosition = CalculateGridPlacement( x, y );
		gridWorldPositions[new Vector2( x, y )] = worldPosition;

		prefab.LocalScale = new Vector3(1, 1, 1);
		var clonedPrefab = prefab.Clone( position: worldPosition );
		clonedPrefab.NetworkSpawn();
		clonedPrefab.SetParent( GameObject );

		// Store the tile reference
		_tileObjects[new Vector2( x, y )] = clonedPrefab;

		return clonedPrefab;
	}

	private void HandlePowerUpSpawning()
	{
		if ( _powerUpCount < MaxPowerUpCount )
		{
			if ( _spawnInterval == 0f )
			{
				_spawnInterval = new Random().Next( 5, 10 );
				return;
			}
			else if (
				_spawnPowerUpTimer >= _spawnInterval )
			{
				SpawnPowerUp();
				_spawnPowerUpTimer = 0f;
				_spawnInterval = 0;
			}
			else
			{
				_spawnPowerUpTimer += Time.Delta;
			}
		}
	}

	[Rpc.Host]
	public void DestroyGameObjectAt( Vector2 gridPosition )
    {
        int x = (int)gridPosition.x;
        int y = (int)gridPosition.y;

        Vector3 worldPosition = gridWorldPositions[new Vector2( x, y )];

        var gameObject = GameObject.GetAllObjects( true ).FirstOrDefault( go =>
            go.Tags.Contains( "breakable" ) && new Vector2( (int)Math.Floor( go.WorldPosition.x ), (int)Math.Floor( go.WorldPosition.y ) ) == new Vector2( (int)Math.Floor( worldPosition.x ), (int)Math.Floor( worldPosition.y ) ) );
        if ( gameObject != null )
        {
            if ( grid[GridIndex((int)gridPosition.x, (int)gridPosition.y)] == GridCellType.PowerUp)
            {
                DecreasePowerUpCount();
			}
            gameObject.Destroy();
            grid[GridIndex((int)gridPosition.x, (int)gridPosition.y)] = GridCellType.Empty;
			//RpcSendGridPositions( gridWorldPositions.Keys.ToArray(), gridWorldPositions.Values.ToArray(), grid.ToArray());
		}
    }

	private void SpawnPowerUp()
	{
		IncreasePowerUpCount();
		int randomIndex = new Random().Next( 0, _emptySpaces.Count );
		Vector2 randomEmptySpace = _emptySpaces[randomIndex];
		int randomPowerUp = new Random().Next( 0, _powerUpPrefabs.Count );
		var newPowerUp = PlacePrefabAt( (int)randomEmptySpace.x, (int)randomEmptySpace.y, _powerUpPrefabs[randomPowerUp] );
		tile = newPowerUp;
		grid[GridIndex((int)randomEmptySpace.x, (int)randomEmptySpace.y)] = GridCellType.PowerUp;
		_emptySpaces.RemoveAt( randomIndex );
	}

	#endregion


	#region Grid Positions References

	public Vector2 GetGridPosition( Vector3 worldPosition ) =>
	new Vector2(
		-(int)Math.Round( (worldPosition.x - StartingGridCell.x) / (StartingGridCell.x * scaleFactor) ),
		-(int)Math.Round( (worldPosition.y - StartingGridCell.y) / (StartingGridCell.y * scaleFactor) )
	);

	public GridCellType GetGridValue( Vector2 gridPosition )
	{
		int x = (int)gridPosition.x;
		int y = (int)gridPosition.y;
		return grid[GridIndex( x, y )];
	}


	public Vector3 GetWorldPosition( Vector2 gridPosition ) => gridWorldPositions[new Vector2( gridPosition.x, gridPosition.y )];

	public void SetGridValue( Vector2 gridPosition, GridCellType value ) => grid[GridIndex((int)gridPosition.x, (int)gridPosition.y)] = value;

	private Vector3 CalculateGridPlacement( int x, int y ) => StartingGridCell + new Vector3( x * -StartingGridCell.x * scaleFactor, y * -StartingGridCell.y * scaleFactor, 0 );
	#endregion


	#region Animation Methods
	/// <summary>
	/// Single Tile Animation
	/// </summary>
	/// <remarks>Intended for single tiles such as <see cref="PowerUp"/> spawns.</remarks>
	public void SingleTileAnimation()
	{
		if ( tile == null ) return;
		Vector3 velocity = Vector3.Zero;
		tile.LocalPosition = Vector3.SpringDamp(
			current: tile.LocalPosition,
			target: new Vector3( tile.LocalPosition.x, tile.LocalPosition.y, targetZPosition ),
			velocity: ref velocity,
			deltaTime: Time.Delta,
			frequency: 7f,
			damping: 0.04f
		);


		if ( tile.LocalPosition.z <= targetZPosition + 10f )
		{
			tile.WorldPosition = new Vector3( tile.WorldPosition.x, tile.WorldPosition.y, targetZPosition );
			var tileSmoke = _singleSmoke.Clone();
			tileSmoke.NetworkSpawn();
			tileSmoke.WorldPosition = tile.WorldPosition + Vector3.Up * 10;
			tileSmoke.Enabled = true;
			_cameraShake.Shake( magnitude: 4 );
			var sound = GameObject.AddComponent<SoundPointComponent>();
			if ( sound != null )
			{
				sound.SoundEvent = _explosionSound;
				sound.Volume = 0.5f;
				sound.Pitch = 1.55f;
				sound.StartSound();
			}
			tile = null;
		}
	}

	/// <summary>
	/// Plays the configured introductory animation for the grid based on the selected animation type.
	/// </summary>
	/// <remarks>The animation performed depends on the value of the internal grid intro animation setting. This
	/// method should be called to visually introduce the grid at the start of an interaction or game. It has no effect if
	/// called multiple times in succession without resetting the grid state.</remarks>
	public void StartingAnimation()
	{
		switch ( _introAnimation )
		{
			case GridIntroAnimation.Drop:
				AnimateDropGrid();
				break;
			case GridIntroAnimation.Wave:
				AnimateWaveGrid();
				break;
			case GridIntroAnimation.Circle:
				AnimateCircularGrid();
				break;
		}
	}

	/// <summary>
	/// Animates the drop of the grid container to its target position and triggers related visual and audio effects when
	/// the animation completes.
	/// </summary>
	/// <remarks>This method should be called to initiate the drop animation sequence. It enables smoke effects,
	/// marks the map as ready, and plays a camera shake and explosion sound when the grid reaches its target position.
	/// Subsequent calls have no effect once the animation has played.</remarks>
	private void AnimateDropGrid()
	{
		if ( animationPlayed ) return;

		Vector3 velocity = Vector3.Zero;
		GameObject.LocalPosition = Vector3.SpringDamp(
			current: GameObject.LocalPosition,
			target: new Vector3( GameObject.LocalPosition.x, GameObject.LocalPosition.y, targetZPosition ),
			velocity: ref velocity,
			deltaTime: Time.Delta,
			damping: 0.04f
		);

		if ( GameObject.LocalPosition.z <= targetZPosition + 20f )
		{
			GameObject.WorldPosition = GameObject.WorldPosition.WithZ( targetZPosition );
			HandleMapReady();
		}
	}

	/// <summary>
	/// Animates the wave effect across the grid by smoothly raising each tile in sequence. When the animation completes,
	/// marks the map as ready and triggers associated effects such as sound and camera shake.
	/// </summary>
	/// <remarks>This method should be called repeatedly, typically once per frame, until the animation is complete.
	/// Once all tiles have reached their target positions, the method enables additional effects and sets the map state to
	/// ready. Subsequent calls after completion have no effect.</remarks>
	private void AnimateWaveGrid()
	{
		if ( animationPlayed ) return;
		// How fast the tiles rise
		float columnDelay = 0.15f; // Delay per column

		bool allColumnsDone = true;

		for ( int x = 0; x < GridSize; x++ )
		{
			float columnProgress = (_waveTime - x * columnDelay) * _animationSpeed;
			columnProgress = Math.Clamp( columnProgress, 0f, 1f );

			for ( int y = 0; y < GridSize; y++ )
			{
				Vector2 pos = new Vector2( x, y );
				if ( !_tileObjects.TryGetValue( pos, out var tile ) ) continue;

				// Get the correct world position for this tile
				Vector3 basePos = CalculateGridPlacement( x, y );

				// Start at the startingZPosition
				Vector3 startPos = basePos.WithZ( startingZPosition );

				// Target is always 25
				Vector3 targetPos = basePos.WithZ( targetZPosition );

				// Smooth interpolation
				tile.LocalPosition = Vector3.Lerp( startPos, targetPos, columnProgress );

				// If any tile hasn't reached target yet, keep animating
				if ( columnProgress < 1f )
					allColumnsDone = false;
			}
		}

		_waveTime += Time.Delta;

		if ( allColumnsDone )
		{
			HandleMapReady();
		}
	}



	/// <summary>
	/// Animates the grid tiles in a circular pattern, transitioning each tile from its starting position to its target
	/// position. Marks the map as ready and triggers associated effects when the animation completes.
	/// </summary>
	/// <remarks>This method should be called once to initiate the circular grid animation. When all tiles have
	/// finished animating, the map is marked as ready, visual and audio effects are triggered, and any dependent
	/// components are enabled. Subsequent calls have no effect after the animation has played.</remarks>
	private void AnimateCircularGrid()
	{
		if ( animationPlayed ) return;

		bool allTilesDone = true;
		Vector2 center = new Vector2( GridSize / 2, GridSize / 2 );

		for ( int x = 0; x < GridSize; x++ )
		{
			for ( int y = 0; y < GridSize; y++ )
			{
				Vector2 pos = new Vector2( x, y );
				if ( !_tileObjects.TryGetValue( pos, out var tile ) ) continue;

				float distance = Vector2.Distance( pos, center );
				float progress = (_circleTime - distance * 0.1f) * _animationSpeed;
				if ( progress < 1f )
				{
					allTilesDone = false;
					progress = Math.Clamp( progress, 0f, 1f );
				}
				else progress = 1f;

				Vector3 startPos = CalculateGridPlacement( x, y ).WithZ( startingZPosition );
				Vector3 targetPos = CalculateGridPlacement( x, y ).WithZ( targetZPosition );

				tile.LocalPosition = Vector3.Lerp( startPos, targetPos, progress );
			}
		}

		_circleTime += Time.Delta;

		if ( allTilesDone )
		{
			HandleMapReady();
		}
	}

	#endregion


	#region Boolean Helper Methods
	public static bool IsValidGridPosition( Vector2 pos ) => pos.x is >= 0 and < GridSize && pos.y is >= 0 and < GridSize;

	private static bool IsPlayerSpawn( int x, int y ) => PlayerSpawnPositions.Contains( (x, y) );

	private static bool IsEmptySpace( int x, int y ) => EmptyPositions.Contains( (x, y) );


	#endregion


	#region Helpers
	public void IncreasePowerUpCount()
	{
		if ( !Networking.IsHost )
			return;
		_powerUpCount = Math.Min( MaxPowerUpCount, _powerUpCount + 1 );
	}

	public void DecreasePowerUpCount()
	{
		if ( !Networking.IsHost )
			return;

		_powerUpCount = Math.Max( 0, _powerUpCount - 1 );
	}

	[Rpc.Broadcast]
	private void HandleMapReady()
	{
		animationPlayed = true;
		IsMapReady = true;
		_SmokePlayer.Enabled = true;
		_cameraShake?.Shake( 30f, 2f, true );

		var sound = GameObject.AddComponent<SoundPointComponent>();
		if ( sound != null )
		{
			sound.SoundEvent = _explosionSound;
			sound.StartSound();
		}
	}

	[Rpc.Broadcast]
	private void RpcSendGridPositions( Vector2[] keys, Vector3[] values, GridCellType[] gridValues )
	{
		// Sync grid
		grid.Clear();
		for ( int i = 0; i < gridValues.Length; i++ )
			grid.Add( gridValues[i] );

		// Sync world positions
		gridWorldPositions.Clear();
		for ( int i = 0; i < keys.Length; i++ )
			gridWorldPositions[keys[i]] = values[i];
	}

	[Rpc.Broadcast]
	public void RpcSetGridValuesToEmpty( Vector2[] positions)
	{
		foreach (var pos in positions)
		{
			if ( MapLoader.IsValidGridPosition( pos ) && (GetGridValue(pos) )!= GridCellType.IndestructibleWall && GetGridValue(pos) != GridCellType.Empty)
				SetGridValue( pos, GridCellType.Empty );
		}
	}

	//[Rpc.Broadcast]
	//private void RpcLateJoinMapReady()
	//{
	//	// Set map ready state locally
	//	animationPlayed = true;
	//	IsMapReady = true;

	//}


	//void INetworkListener.OnConnected( Connection channel )
	//{
	//	// Send the grid and world positions
	//	RpcSendGridPositions(
	//		gridWorldPositions.Keys.ToArray(),
	//		gridWorldPositions.Values.ToArray(),
	//		grid.ToArray()
	//	);

	//	// If map is ready, trigger the ready logic for just this client
	//	if ( IsMapReady )
	//		RpcLateJoinMapReady();
	//}


	#endregion


	#region Debugging Methods
	[Button("Print Grid"), Category("Debugging"), Order(9)]
    private void PrintGrid()
    {
        foreach (var row in grid.Cast<GridCellType>().Select((value, idx) => new { value, idx })
            .GroupBy(v => v.idx / GridSize)
            .Select(g => g.Select(v => v.value).ToArray()))
        {
            Log.Info(string.Join(", ", row));
        }
    }

    [Button("Print Empty Spaces"), Category( "Debugging" )]
    private void PrintEmptySpaces()
    {
        foreach (var emptySpace in _emptySpaces)
        {
            Log.Info($"Empty Space: {emptySpace}");
        }
    }

    [Button("Print Grid World Positions"), Category( "Debugging" )]
    private void PrintGridWorldPositions()
    {
        foreach (var gridWorldPosition in gridWorldPositions)
        {
            Log.Info($"Grid Position: {gridWorldPosition.Key} World Position: {gridWorldPosition.Value}");
        }
    }

	[Button("Spawn Powerup"), Category("Debugging")]
	private void SpawnPowerUpDebug()
	{
		SpawnPowerUp();
	}


	#endregion
}
