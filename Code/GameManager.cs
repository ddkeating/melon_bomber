using Sandbox;
using Sandbox.Services.Players;

public sealed class GameManager : Component
{
    public static bool StartGame { get; set; } = false;
	public static GameMode SelectedGameMode { get; set; } = GameMode.Classic;
	public enum GameMode
	{
		Classic,
		FallingBlocks,
		ChainReaction
	}
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

	[Rpc.Broadcast]
    public static void StartGameButton()
    {
		StartGame = true;
    }





}
