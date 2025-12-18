using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Sandbox;
using Sandbox.Services.Players;

public sealed class MapLoader : Component
{
    // Reference Variables
    [Property] private List<PrefabScene> _powerUpPrefabs = new List<PrefabScene>();
    [Property] private List<PrefabScene> _wallPrefabs = new List<PrefabScene>();
    [Property] private List<PrefabScene> _playerPrefabs = new List<PrefabScene>();
    [Property] private SoundEvent _swoshSound;
    [Property] private SoundEvent _explosionSound;
    [Property] private GameObject _SmokePlayer;
    [Property] private GameObject _singleSmoke;
    private GameObject _camera;
    private GameObject gridContainer;
    private GameObject tile;
    private CameraShake _cameraShake;

    // Properties
    private const float scaleFactor = 0.1825f;
    public const int GridSize = 13;
    public const int targetZPosition = 25;
    private const int startingZPosition = 1000;
    private List<Vector2> _emptySpaces = new List<Vector2>();
    private Dictionary<Vector2, Vector3> gridWorldPositions = new Dictionary<Vector2, Vector3>();

    private Vector3 StartingGridCell = new Vector3(-352, 352, startingZPosition);
    private Vector3 GridCellSize = new Vector3(1, 1, 1f);
    private const int PlayerCount = 4;
    private const int MaxPowerUpCount = 2;
    private int _powerUpIndex = 0;
    public int _powerUpCount = 0;
    private int _playerIndex = 0;
    private bool MapGenerate = false;
    private bool StartGeneratingMap = false;
    private bool animationPlayed = false;
    private bool tileAnimationPlayed => tile == null;
    private float _spawnPowerUpTimer = 0f;
    private int _spawnInterval;
    private bool _soundOnePlayed = false;
    private bool _soundTwoPlayed = false;

    private string[,] grid = new string[GridSize, GridSize];

    /* 
    Random Values:
    0 = Empty Space
    1 = Wall
    2 = Player Spawn
    3 = PowerUp
    */

    // SANDBOX METHODS
    protected override void OnStart()
    {
        if (!Networking.IsHost) return;
        gridContainer = GameObject;
        gridContainer.LocalPosition = new Vector3( gridContainer.LocalPosition.x, gridContainer.LocalPosition.y, startingZPosition );
        _camera = Scene.Camera.GameObject;
        _SmokePlayer.Enabled = false;
        _singleSmoke.Enabled = false;
        _cameraShake = _camera.GetComponent<CameraShake>();
        Log.Info( "called" );
        if ( _cameraShake == null )
        {
            Log.Error( "Camera Shake component not found." );
        }
    }

    protected override void OnUpdate()
    {
        if ( !BomberManager.StartGame ) return;
        if ( !StartGeneratingMap && !MapGenerate )
        {
            GenerateMap();
            StartGeneratingMap = true;
        }
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

    protected override void OnFixedUpdate()
    {
        if ( MapGenerate && !animationPlayed )
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
            }
            StartingAnimation();
        }

        if ( !tileAnimationPlayed )
        {
            SingleTileAnimation();
        }
    }
    

    // MAP GENERATION METHODS
    private void GenerateMap()
    {
        // Creating Random instance for randomly generating values for the grid.
        Random rnd = new Random();

        // Loop through the grid positions for generation
        for ( int x = 0; x < GridSize; x++ )
        {
            for ( int y = 0; y < GridSize; y++ )
            {
                // Handle player spawns in predefined corners
                if ( IsPlayerSpawn( x, y ) )
                {
                    grid[x, y] = "Player Spawn";
                    PlacePrefabAt( x, y, _playerPrefabs[0] );
                    _playerIndex++;
                    continue;
                }

                // Handle empty spaces that are not walls or power-ups
                if ( IsEmptySpace( x, y ) )
                {
                    grid[x, y] = "Empty Space";
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

        Log.Info( "Done Generating Map" );
        MapGenerate = true;
    }

    private bool IsPlayerSpawn(int x, int y)
    {
        return (x == 0 && y == 0) || (x == 0 && y == GridSize - 1) || (x == GridSize - 1 && y == 0) || (x == GridSize - 1 && y == GridSize - 1);
    }

    private bool IsEmptySpace(int x, int y)
    {
        return new[] { (x == 1 && y == 0), (x == 0 && y == 1), (x == GridSize - 2 && y == 0), (x == GridSize - 1 && y == 1), 
                       (x == 0 && y == GridSize - 2), (x == 1 && y == GridSize - 1), (x == GridSize - 2 && y == GridSize - 1), (x == GridSize - 1 && y == GridSize - 2) }
                .Any(condition => condition);
    }

    private void HandleWallAndPowerUpGeneration(Random rnd, int x, int y)
    {
        if ( x % 2 == 1 && y % 2 == 1 )
        {
            if ( x == 3 && y == 1 || x == 1 && y == 3 || x == 3 && y == 11 || x == 1 && y == 9 || x == 11 && y == 3 || x == 9 && y == 1 || x == 11 && y == 9 || x == 9 && y == 11 || x == 5 && y == 5 || x == 5 && y == 7 || x == 7 && y == 5 || x == 7 && y == 7 )
                PlacePrefabAt( x, y, _wallPrefabs[1] );
            else
            {
                PlacePrefabAt(x, y, _wallPrefabs[2]);
            }
            grid[x, y] = "Indestructable Wall";
            return;
        }
        {
            float randomValue = rnd.Float(0f, 1f);

            if (randomValue < 0.4f)
            {
                grid[x, y] = "Empty Space";
                _emptySpaces.Add(new Vector2(x, y));
                gridWorldPositions[new Vector2(x, y)] = CalculateGridPlacement(x, y);
            }
            else if (randomValue < 1f)
            {
                grid[x, y] = "Wall";
                PlacePrefabAt(x, y, _wallPrefabs[0]);
            }
        }
    }


    private void PlacePowerUps(Random rnd)
    {
        for (; _powerUpCount < MaxPowerUpCount; _powerUpCount++)
        {
            Random randomPowerUp = new Random();
            _powerUpIndex = randomPowerUp.Next(0, _powerUpPrefabs.Count);
            int randomIndex = rnd.Next(0, _emptySpaces.Count);
            Vector2 randomEmptySpace = _emptySpaces[randomIndex];
            PlacePrefabAt((int)randomEmptySpace.x, (int)randomEmptySpace.y, _powerUpPrefabs[_powerUpIndex]);
            grid[(int)randomEmptySpace.x, (int)randomEmptySpace.y] = "PowerUp";
            _emptySpaces.RemoveAt(randomIndex);
        }
    }

    private GameObject PlacePrefabAt( int x, int y, PrefabScene prefab )
    {
        Vector3 worldPosition = CalculateGridPlacement( x, y );
        gridWorldPositions[new Vector2( x, y )] = worldPosition;

        prefab.LocalScale = GridCellSize;
        var clonedPrefab = prefab.Clone( position: worldPosition );
        clonedPrefab.NetworkSpawn();
        clonedPrefab.SetParent( gridContainer );
        return clonedPrefab;
    }

    private Vector3 CalculateGridPlacement(int x, int y)
    {
        float xOffset = x * -StartingGridCell.x * 0.1825f;
        float yOffset = y * -StartingGridCell.y * 0.1825f;

        return StartingGridCell + new Vector3(xOffset, yOffset, 0);
    }

    // GAME APPEARANCE METHODS
    private void SingleTileAnimation( )
    {
        if (tile == null) return;
        Log.Info( "called" );
        Vector3 velocity = Vector3.Zero;
        tile.LocalPosition = Vector3.SpringDamp( tile.LocalPosition,
            new Vector3( tile.LocalPosition.x, tile.LocalPosition.y, targetZPosition ), ref velocity, 2f, 0.08f );

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
    private void StartingAnimation()
    {
        if ( animationPlayed ) return;

        // BE CAREFUL ADJUSTING THESE VALUES AS THEY ARE TIED WITH THE SOUND EFFECTS.
        Vector3 velocity = Vector3.Zero;
        gridContainer.LocalPosition = Vector3.SpringDamp( gridContainer.LocalPosition,
            new Vector3( gridContainer.LocalPosition.x, gridContainer.LocalPosition.y, targetZPosition ), ref velocity, 1f, 0.04f);

        if ( gridContainer.LocalPosition.z <= targetZPosition + 20f )
        {
            gridContainer.WorldPosition = new Vector3( gridContainer.WorldPosition.x, gridContainer.WorldPosition.y, targetZPosition );
            _SmokePlayer.Enabled = true;
            animationPlayed = true;
            _cameraShake.Shake( magnitude: 30, duration: 2f, decay: true );
            var sound = GameObject.AddComponent<SoundPointComponent>();
            if ( sound != null )
            {
                sound.SoundEvent = _explosionSound;
                sound.StartSound();
            }
        }
    }

    
    

    // GAMEPLAY METHODS
    public void DestroyGameObjectAt( Vector2 gridPosition )
    {
        int x = (int)gridPosition.x;
        int y = (int)gridPosition.y;

        Vector3 worldPosition = gridWorldPositions[new Vector2( x, y )];

        var gameObject = GameObject.GetAllObjects( true ).FirstOrDefault( go =>
            go.Tags.Contains( "breakable" ) && new Vector2( (int)Math.Floor( go.WorldPosition.x ), (int)Math.Floor( go.WorldPosition.y ) ) == new Vector2( (int)Math.Floor( worldPosition.x ), (int)Math.Floor( worldPosition.y ) ) );
        if ( gameObject != null )
        {
            if ( grid[(int)gridPosition.x, (int)gridPosition.y] == "PowerUp" )
            {
                _powerUpCount--;
            }
            gameObject.Destroy();
            grid[(int)gridPosition.x, (int)gridPosition.y] = "Empty Space";
        }
    }

    public string GetGridValue( Vector2 gridPosition )
    {
        return grid[(int)gridPosition.x, (int)gridPosition.y];
    }
    
    public void SetGridValue( Vector2 gridPosition, string value )
    {
        grid[(int)gridPosition.x, (int)gridPosition.y] = value;
    }
    
    public Vector2 GetGridPosition( Vector3 worldPosition )
    {
        // Get the grid position of the bomb
        int x = (int)Math.Round( (worldPosition.x - StartingGridCell.x) / (StartingGridCell.x * scaleFactor) );
        int y = (int)Math.Round( (worldPosition.y - StartingGridCell.y) / (StartingGridCell.y * scaleFactor) );

        // Multiplied by -1 to invert the values to positive.
        return new Vector2( x * -1, y * -1 );
    }

    public Vector3 GetWorldPosition( Vector2 gridPosition )
    {
        return gridWorldPositions[new Vector2( gridPosition.x, gridPosition.y )];
    }

    // Spawn Powerups
    private void SpawnPowerUp()
    {
        if (_powerUpCount >= MaxPowerUpCount)
        {
            return;
        }
        _powerUpCount++;
        int randomIndex = new Random().Next(0, _emptySpaces.Count);
        Vector2 randomEmptySpace = _emptySpaces[randomIndex];
        int randomPowerUp = new Random().Next(0, _powerUpPrefabs.Count);
        var newPowerUp = PlacePrefabAt((int)randomEmptySpace.x, (int)randomEmptySpace.y, _powerUpPrefabs[randomPowerUp]);
        tile = newPowerUp;
        grid[(int)randomEmptySpace.x, (int)randomEmptySpace.y] = "PowerUp";
        _emptySpaces.RemoveAt(randomIndex);
    }

    // DEBUG
        [Button( "Print Grid" )]
    private void PrintGrid()
    {
        foreach (var row in grid.Cast<string>().Select((value, idx) => new { value, idx })
            .GroupBy(v => v.idx / GridSize)
            .Select(g => g.Select(v => v.value).ToArray()))
        {
            Log.Info(string.Join(", ", row));
        }
    }

    [Button("Print Empty Spaces")]
    private void PrintEmptySpaces()
    {
        foreach (var emptySpace in _emptySpaces)
        {
            Log.Info($"Empty Space: {emptySpace}");
        }
    }

    [Button("Print Grid World Positions")]
    private void PrintGridWorldPositions()
    {
        foreach (var gridWorldPosition in gridWorldPositions)
        {
            Log.Info($"Grid Position: {gridWorldPosition.Key} World Position: {gridWorldPosition.Value}");
        }
    }
}
