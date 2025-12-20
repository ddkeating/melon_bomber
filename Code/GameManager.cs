using Sandbox;

public sealed class BomberManager : Component
{
    public static bool StartGame = false;
    protected override void OnStart()
    {
        if ( !Networking.IsHost ) return;
		StartGame = false;
    }

    protected override void OnFixedUpdate()
    {
        if ( !Networking.IsHost ) return;
        if ( !StartGame ) return;
        // Check if all players are loaded from a lobby system or something like that.
        

    }
    
    public static void StartGameButton()
    {
        StartGame = true;
    }
}
