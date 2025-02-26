using Sandbox;

public sealed class GameNetworkManager : Component, Component.INetworkListener

{
    public void OnActive( Connection connection )
    {
        Log.Info( "Client connected" );
    }
}
