using Sandbox;

public sealed class PowerUp : Component
{
    private SpriteRenderer _spriteRenderer;
    private BombManager _bombManager;
    private MapLoader _mapLoader;
    private Vector2 pivot = new Vector2( 0.5f, 0.5f );
    private float pivotInterval = 0.5f;
    private float pivotTimer = 0f;
    private bool _pivotDown = false;
    private Vector2 targetPivot = new Vector2( 0.5f, 0.6f ); // Starting target pivot
    private float smoothSpeed = 5f; // Adjust this for smoother or quicker transition

    protected override void OnStart()
    {
        _spriteRenderer = GameObject.GetComponent<SpriteRenderer>();
        var bombManagerContainer = Scene.GetAllComponents<BombManager>();
        foreach ( var bombManager in bombManagerContainer )
        {
            _bombManager = bombManager;
        }
        var mapLoaderContainer = Scene.GetAllComponents<MapLoader>();
        foreach ( var mapLoader in mapLoaderContainer )
        {
            _mapLoader = mapLoader;
        }
    }

    protected override void OnUpdate()
    {

    }

    protected override void OnFixedUpdate()
    {
        pivotTimer += Time.Delta;
        if ( pivotTimer >= pivotInterval )
        {
            pivotTimer = 0f;
            SpriteAnimation();
        }

        // Smoothly transition to the target pivot value
        //_spriteRenderer.Pivot = Vector2.Lerp( _spriteRenderer.Pivot, targetPivot, smoothSpeed * Time.Delta );
    }

    private void SpriteAnimation()
    {
        // Toggle target pivot between 0.4f and 0.6f
        targetPivot = new Vector2( 0.5f, _pivotDown ? .4f : .8f );
        _pivotDown = !_pivotDown;
    }

    public void HandlePowerUp()
    {
        if ( GameObject.Name == "radius-powerup" )
        {
            IncreaseRadius();
        }
        else if ( GameObject.Name == "bomb-powerup" )
        {
            IncreaseMaxBombs();
        }
        else if ( GameObject.Name == "speed-powerup" )
        {
            IncreaseSpeed();
        }
        var powerUpOnGrid = _mapLoader.GetGridPosition( GameObject.WorldPosition );
        _mapLoader.SetGridValue( powerUpOnGrid, MapLoader.GridCellType.Empty );
        _mapLoader._powerUpCount--;
    }

    public void IncreaseRadius()
    {
        if ( _bombManager == null )
        {
            Log.Error( "BombManager is not assigned." );
            return;
        }
        _bombManager.IncreaseBombRadius();
        GameObject.Destroy();
    }

    public void IncreaseMaxBombs()
    {
        if ( _bombManager == null )
        {
            Log.Error( "BombManager is not assigned." );
            return;
        }
        _bombManager.IncreaseBombCount();
        GameObject.Destroy();
    }
    public void IncreaseSpeed()
    {
        if ( _bombManager == null )
        {
            Log.Error( "BombManager is not assigned." );
            return;
        }
        _bombManager.IncreasePlayerSpeed();
        GameObject.Destroy();
    }
}
