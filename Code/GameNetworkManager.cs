using Sandbox;
using Sandbox.Services.Players;
using System.Threading.Tasks;

public sealed class GameNetworkManager : Component, Component.INetworkListener
{
	[Sync] public static NetList<PlayerSlot> PlayerSlots { get; set; } = new();

	public struct PlayerSlot
	{
		public long SteamId;
		public string DisplayName;
		public string AvatarUrl;
		public bool IsHost;
	}

	protected override void OnStart()
	{
		PlayerSlots.Clear();
	}

	protected override void OnUpdate()
	{
		if ( Network.IsProxy ) return;

		if ( PlayerSlots.Count == Connection.All.Count ) return;
		PlayerSlots.Clear();
		foreach ( var conn in Connection.All )
		{
			var slot = new PlayerSlot
			{
				SteamId = conn.SteamId,
				DisplayName = conn.DisplayName,
				IsHost = conn.SteamId == Connection.Host.SteamId,
			};
			PlayerSlots.Add( slot );
			_ = FetchPlayerProfileAsync( PlayerSlots.Count - 1, conn.SteamId );
		}
	}


	public static void KickPlayer( long steamId )
	{
		if ( !Networking.IsHost )
			return;

		var connection = Connection.All.FirstOrDefault( c => c.SteamId == steamId );

		if ( connection == null )
		{
			Log.Warning( $"Kick failed: no connection for {steamId}" );
			return;
		}
		Log.Info( connection );
		connection.Kick( "You have been kicked from the game." );
	}

	#region Helper Methods
	private async Task FetchPlayerProfileAsync( int slotIndex, long steamId )
	{
		var profile = await Profile.Get( steamId );
		if ( profile == null ) return;

		var slot = PlayerSlots[slotIndex];
		slot.AvatarUrl = profile.Avatar;

		// Called for updating the UI.
		PlayerSlots[slotIndex] = slot;
	}

	#endregion
}
