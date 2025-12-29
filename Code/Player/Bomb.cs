using System.Diagnostics;
using System.Threading;
using Sandbox;

public sealed class Bomb : Component
{
    private GameObject _explosionParticle;
    private GameObject _shockwaveParticle;
    private GameObject _explosion;
    private MapLoader _mapLoader;
    private BombManager _bombManager;
    private CameraShake cameraShake;
    private BoxCollider _boxCollider;
    float _bombDetonationTime = 5f;
    int radius = 1;

	private Vector2 bombGridPos;

	protected override void OnStart()
    {
        _explosionParticle = GameObject.Children[0];
        _explosionParticle.Enabled = false;
        _explosionParticle.WorldPosition = GameObject.WorldPosition + Vector3.Up * 100;
        _shockwaveParticle = GameObject.Children[1];
        _shockwaveParticle.Enabled = false;
        _shockwaveParticle.WorldPosition = GameObject.WorldPosition + Vector3.Up * 20;
        _explosion = GameObject.Children.Find(c => c.Name == "Explosion");
        _explosion.Enabled = false;
        _explosion.WorldPosition = GameObject.WorldPosition + Vector3.Up * 20;
        _boxCollider = GameObject.GetComponent<BoxCollider>();
        _boxCollider.Enabled = true;
        _boxCollider.IsTrigger = true;
        
        _mapLoader = Scene.GetAllComponents<MapLoader>().FirstOrDefault();
		_bombManager = Scene.GetAllComponents<BombManager>().Where( x => x.Network.Owner == Network.Owner ).FirstOrDefault();
		cameraShake = Scene.Camera.GetComponent<CameraShake>();

		bombGridPos = _mapLoader.GetGridPosition( GameObject.WorldPosition );
		_mapLoader.SetGridValue( bombGridPos, MapLoader.GridCellType.Bomb );



		if ( _mapLoader == null || !_mapLoader.IsMapReady )
		{
			GameObject.Destroy();
			return;
		}

		HandleBombType();
	}
	protected override void OnUpdate()
    {
		if ( _bombManager.CurrentBombType == BombManager.BombType.Remote )
			return;
        HandleTint();
        _bombDetonationTime -= Time.Delta;
        if ( _bombDetonationTime <= 0 )
        {
            Explode();
        }
    }

    private void Explode()
    {
		_mapLoader.SetGridValue( bombGridPos, MapLoader.GridCellType.Empty );

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

	private void HandleBombType()
	{
		switch ( _bombManager.CurrentBombType )
		{
			case BombManager.BombType.Remote:
				RemoteBombHandler();
				break;
			case BombManager.BombType.Atomic:
				AtomicBombHandler();
				break;
			default:
				StandardBombHandler();
				break;
		}
	}

	private void RemoteBombHandler()
	{
		// Implement remote bomb detonation logic here
		// Change model to bomb with remote detonator

	}

	private void AtomicBombHandler()
	{
		// Implement atomic bomb explosion logic here
		GameObject.GetComponentInChildren<Decal>(includeDisabled: true).Enabled = true;

	}

	private void StandardBombHandler()
	{
		// Implement standard bomb explosion logic here
	}

	private void ExplodeOnOtherTiles()
    {
        var bombOnGrid = _mapLoader.GetGridPosition( GameObject.WorldPosition );
        var gridSize = MapLoader.GridSize;
		List<Vector2> affectedPositions = new List<Vector2> { bombOnGrid};

		void ProcessDirection( Vector2 direction )
		{
			for ( int i = 1; i <= radius; i++ )
			{
				var position = bombOnGrid + direction * i;

				if ( !IsValidGridPosition( position, gridSize ) )
					break;

				// ❌ Stop immediately on indestructible walls (no explosion)
				if ( IsIndestructibleWall( position ) )
					break;

				// 💥 Always explode on valid non-indestructible tiles
				CloneExplosion( _mapLoader.GetWorldPosition( position ) );

				affectedPositions.Add( position );

				// 🧱 Destroy breakable walls and stop
				if ( IsWall( position ) )
				{
					_mapLoader.DestroyGameObjectAt( position );
					break;
				}
			}
		}



		ProcessDirection( new Vector2( 0, 1 ) ); // Right
        ProcessDirection( new Vector2( 0, -1 ) ); // Left
        ProcessDirection( new Vector2( 1, 0 ) ); // Up
        ProcessDirection( new Vector2( -1, 0 ) ); // Down

		_mapLoader.RpcSetGridValuesToEmpty( affectedPositions.ToArray() );
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

        return _mapLoader.GetGridValue( gridPosition ) == MapLoader.GridCellType.IndestructibleWall;
    }

    private bool IsWall( Vector2 gridPosition )
    {
        if ( !IsValidGridPosition( gridPosition, MapLoader.GridSize ) )
        {
            return false;
        }
        return _mapLoader.GetGridValue( gridPosition ) == MapLoader.GridCellType.Wall;
    }

    private void CloneExplosion( Vector2 position )
    {
        var explosion = _explosion.Clone( position: new Vector3( position.x, position.y, 25 ), GameObject.WorldRotation );
        explosion.NetworkSpawn();
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
