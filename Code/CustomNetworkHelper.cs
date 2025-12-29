using System;
using System.Threading.Tasks;
using Sandbox;

/// <summary>
/// Creates a networked game lobby and assigns player prefabs to connected clients.
/// </summary>
[Title( "Custom Network Helper" )]
[Category( "Networking" )]
public sealed class CustomNetworkHelper : Component, Component.INetworkListener
{

	[Property] public bool StartServer { get; set; } = true;


	[Property] public GameObject PlayerPrefab { get; set; }

	[Property] public List<GameObject> SpawnPoints { get; set; }

	private Transform[] _usedSpawnPoints = Array.Empty<Transform>();

	protected override async Task OnLoad()
	{
		if ( Scene.IsEditor )
			return;

		if ( StartServer && !Networking.IsActive )
		{
			LoadingScreen.Title = "Creating Lobby";
			await Task.DelayRealtimeSeconds( 0.1f );
			Networking.CreateLobby( new() );
		}
	}

	public void OnActive( Connection channel )
	{
		Log.Info( $"Player '{channel.DisplayName}' has joined the game" );

		if ( !PlayerPrefab.IsValid() )
			return;

		//
		// Find a spawn location for this player
		//
		var startLocation = FindSpawnLocation().WithScale( 1 );

		// Spawn this object and make the client the owner
		var player = PlayerPrefab.Clone( startLocation, name: $"Player - {channel.DisplayName}" );
		player.NetworkSpawn( channel );
	}

	Transform FindSpawnLocation()
	{
		// If they have spawn point set then use those
		if ( SpawnPoints is not null && SpawnPoints.Count > 0 )
		{
			foreach( var sp in SpawnPoints )
			{
				if ( sp.IsValid() && !_usedSpawnPoints.Contains( sp.WorldTransform ) )
				{
					_usedSpawnPoints = _usedSpawnPoints.Append( sp.WorldTransform ).ToArray();
					return sp.WorldTransform;
				}
			}
		}

		//
		// If we have any SpawnPoint components in the scene, then use those
		//
		var spawnPoints = Scene.GetAllComponents<SpawnPoint>().ToArray();
		if ( spawnPoints.Length > 0 )
		{
			return Random.Shared.FromArray( spawnPoints ).WorldTransform;
		}

		//
		// Failing that, spawn where we are
		//
		return WorldTransform;
	}
}
