using System.Diagnostics;
using System.Threading;
using Sandbox;

public sealed class Bomb : Component
{
    private GameObject _explosionParticle;
    private GameObject _shockwaveParticle;
    private GameObject _fuseFlareParticle;
    private GameObject _fuseParticle;
    private GameObject _explosion;
    private GameObject _boomSound;
    private MapLoader _mapLoader;
    private BombManager _bombManager;
    private CameraShake cameraShake;
    private BoxCollider _boxCollider;
    float _bombDetonationTime = 5f;
    int radius = 1;

    protected override void OnStart()
    {
        _explosionParticle = GameObject.Children[0];
        _explosionParticle.Enabled = false;
        _explosionParticle.WorldPosition = GameObject.WorldPosition + Vector3.Up * 100;
        _shockwaveParticle = GameObject.Children[1];
        _shockwaveParticle.Enabled = false;
        _shockwaveParticle.WorldPosition = GameObject.WorldPosition + Vector3.Up * 20;
        _explosion = GameObject.Children[2];
        _explosion.Enabled = false;
        _explosion.WorldPosition = GameObject.WorldPosition + Vector3.Up * 20;
        _boxCollider = GameObject.GetComponent<BoxCollider>();
        _boxCollider.Enabled = true;
        _boxCollider.IsTrigger = true;
        
        var mapLoaderContainer = Scene.GetAllComponents<MapLoader>();
        foreach ( var mapLoader in mapLoaderContainer )
        {
            _mapLoader = mapLoader;
        }
        var bombManagerContainer = Scene.GetAllComponents<BombManager>();
        foreach ( var bombManager in bombManagerContainer )
        {
            _bombManager = bombManager;
        }
        cameraShake = Scene.Camera.GetComponent<CameraShake>();

    }
    protected override void OnUpdate()
    {
        HandleTint();
        _bombDetonationTime -= Time.Delta;
        if ( _bombDetonationTime <= 0 )
        {
            Explode();
        }
    }

    private void Explode()
    {
        var smokeCloud = _explosionParticle.Clone( GameObject.WorldPosition + Vector3.Up * 50, GameObject.WorldRotation );
        smokeCloud.NetworkSpawn();
        smokeCloud.Enabled = true;
        var shockwave = _shockwaveParticle.Clone( GameObject.WorldPosition + Vector3.Up * 20, GameObject.WorldRotation );
        shockwave.NetworkSpawn();
        shockwave.Enabled = true;
        cameraShake.Shake( magnitude: 4 );
        CloneExplosion( GameObject.WorldPosition );
        ExplodeOnOtherTiles();

    
        GameObject.Destroy();
        _bombManager.OnBombDetonated( GameObject );
    }

    private void ExplodeOnOtherTiles()
    {
        var bombOnGrid = _mapLoader.GetGridPosition( GameObject.WorldPosition );
        var gridSize = MapLoader.GridSize;

        void TryCloneExplosion( Vector2 gridPosition )
        {
            if ( IsValidGridPosition( gridPosition, gridSize ) && !IsIndestructibleWall( gridPosition ) )
            {
                CloneExplosion( _mapLoader.GetWorldPosition( gridPosition ) );
                _mapLoader.DestroyGameObjectAt( gridPosition );
            }
        }

        void ProcessDirection( Vector2 direction )
        {
            var wallList = new List<Vector2>();
            for ( int i = 1; i <= radius; i++ )
            {
                var position = bombOnGrid + direction * i;
                if ( IsIndestructibleWall( position ) || IsWall( position ) )
                {
                    wallList.Add( position );
                }
                if ( !wallList.Any( wall => direction.x != 0 ? position.x > wall.x && position.y == wall.y : position.y > wall.y && position.x == wall.x ) )
                {
                    TryCloneExplosion( position );
                    if ( IsIndestructibleWall( position ) || IsWall( position ) )
                    {
                        break;
                    }
                }
            }
        }

        ProcessDirection( new Vector2( 0, 1 ) ); // Right
        ProcessDirection( new Vector2( 0, -1 ) ); // Left
        ProcessDirection( new Vector2( 1, 0 ) ); // Up
        ProcessDirection( new Vector2( -1, 0 ) ); // Down
    }

    private bool IsValidGridPosition( Vector2 gridPosition, int gridSize )
    {
        return gridPosition.x >= 0 && gridPosition.x < gridSize && gridPosition.y >= 0 && gridPosition.y < gridSize;
    }

    private bool IsIndestructibleWall( Vector2 gridPosition )
    {
        if ( !IsValidGridPosition( gridPosition, MapLoader.GridSize ) )
        {
            return false;
        }

        return _mapLoader.GetGridValue( gridPosition ) == "Indestructable Wall";
    }

    private bool IsWall( Vector2 gridPosition )
    {
        if ( !IsValidGridPosition( gridPosition, MapLoader.GridSize ) )
        {
            return false;
        }
        return _mapLoader.GetGridValue( gridPosition ) == "Wall";
    }

    private void CloneExplosion( Vector2 position )
    {
        var explosion = _explosion.Clone( position: new Vector3( position.x, position.y, 25 ), GameObject.WorldRotation );
        explosion.Enabled = true;
    }



    public void SetDetonationTime( float detonationTime )
    {
        _bombDetonationTime = detonationTime;
    }
    public void SetRadius( int radius )
    {
        this.radius = radius;
    }

    private void SetRedTint()
    {
        var modelRenderer = GameObject.GetComponent<ModelRenderer>();
        modelRenderer.Tint = Color.Red;
    }
    private void SetWhiteTint()
    {
        var modelRenderer = GameObject.GetComponent<ModelRenderer>();
        modelRenderer.Tint = Color.White;
    }

    private void HandleTint()
    {
        float[] beatIntervals = { 0.125f, 0.25f, 0.375f, 0.5f, 0.75f };
        float beatInterval = beatIntervals[0];

        if (_bombDetonationTime <= 1f)
        {
            beatInterval = beatIntervals[0];
        }
        else if (_bombDetonationTime <= 2f)
        {
            beatInterval = beatIntervals[1];
        }
        else if (_bombDetonationTime <= 3f)
        {
            beatInterval = beatIntervals[2];
        }
        else if (_bombDetonationTime <= 4f)
        {
            beatInterval = beatIntervals[3];
        }
        else if (_bombDetonationTime <= 5f)
        {
            beatInterval = beatIntervals[4];
        }

        float timeRemaining = _bombDetonationTime % (beatInterval * 2);

        if (timeRemaining <= beatInterval)
        {
            SetWhiteTint();
        }
        else
        {
            SetRedTint();
        }
    }
}
