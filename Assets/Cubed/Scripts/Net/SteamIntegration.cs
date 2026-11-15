// Optional Steam support. Compiled only when the CUBED_STEAM scripting define is set and the
// Facepunch.Steamworks DLLs are in Assets/Plugins (see docs/STEAM.md). Without it, the stubs at the
// bottom keep the rest of the game compiling and the Steam menu entries hidden.
#if CUBED_STEAM
using System;
using System.Collections.Generic;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace Cubed.Net
{
    /// <summary>Steam lobbies (friend invites), relay networking, achievements and rich presence.</summary>
    public static class SteamIntegration
    {
        /// <summary>Your Steam app id. 480 is Valve's public test app (Spacewar).</summary>
        public static uint AppId = 480;
        public static bool Available => ready;
        public static Lobby? CurrentLobby { get; private set; }
        public static string Status { get; private set; } = "";

        static bool ready;
        static CubedSocketManager socketManager;
        static CubedConnectionManager connectionManager;

        public static void Init()
        {
            if (ready) return;
            try
            {
                SteamClient.Init(AppId, true);
                ready = true;
            }
            catch (Exception e)
            {
                Status = "Steam not available: " + e.Message;
                Debug.LogWarning("[Cubed] " + Status);
                return;
            }
            SteamFriends.OnGameLobbyJoinRequested += (lobby, friend) => JoinLobby(lobby);
            SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
            Profile.AchievementUnlocked += a =>
            {
                try { new Steamworks.Data.Achievement(a.Id).Trigger(true); }
                catch (Exception e) { Debug.LogWarning("[Cubed] Steam achievement " + a.Id + ": " + e.Message); }
            };
            Status = "Signed in as " + SteamClient.Name;
        }

        public static string PlayerName => ready ? SteamClient.Name : null;

        /// <summary>Call every frame: pumps relay sockets.</summary>
        public static void Update()
        {
            if (!ready) return;
            try
            {
                socketManager?.Receive();
                connectionManager?.Receive();
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        public static void Shutdown()
        {
            if (!ready) return;
            try
            {
                CurrentLobby?.Leave();
                socketManager?.Close();
                connectionManager?.Close();
                SteamClient.Shutdown();
            }
            catch (Exception) { }
            ready = false;
        }

        public static void SetRichPresence(string status)
        {
            if (!ready) return;
            SteamFriends.SetRichPresence("status", status);
        }

        /// <summary>Host: open a friends-only lobby and a relay socket other players connect through.</summary>
        public static async void HostLobby()
        {
            if (!ready) return;
            var lobby = await SteamMatchmaking.CreateLobbyAsync(8);
            if (!lobby.HasValue) { Status = "Could not create a Steam lobby"; return; }
            var l = lobby.Value;
            l.SetFriendsOnly();
            l.SetJoinable(true);
            l.SetData("game", "cubed");
            CurrentLobby = l;
            socketManager?.Close();
            socketManager = SteamNetworkingSockets.CreateRelaySocket<CubedSocketManager>(0);
            Status = "Steam lobby open - invite friends";
            SetRichPresence("Hosting a lobby");
        }

        public static void InviteFriends()
        {
            if (ready && CurrentLobby.HasValue) SteamFriends.OpenGameInviteOverlay(CurrentLobby.Value.Id);
        }

        static async void JoinLobby(Lobby lobby)
        {
            var result = await lobby.Join();
            if (result != RoomEnter.Success) Status = "Could not join lobby: " + result;
        }

        static void OnLobbyEntered(Lobby lobby)
        {
            CurrentLobby = lobby;
            if (lobby.Owner.Id == SteamClient.SteamId) return; // we're the host
            connectionManager?.Close();
            connectionManager = SteamNetworkingSockets.ConnectRelay<CubedConnectionManager>(lobby.Owner.Id, 0);
            NetSession.Instance?.JoinLink(connectionManager.Link, "Steam (" + lobby.Owner.Name + ")");
            SetRichPresence("In a match");
        }

        public static void LeaveLobby()
        {
            CurrentLobby?.Leave();
            CurrentLobby = null;
            socketManager?.Close();
            socketManager = null;
            connectionManager?.Close();
            connectionManager = null;
        }
    }

    /// <summary>A Steam relay connection as an INetLink.</summary>
    public class SteamLink : INetLink
    {
        readonly Queue<byte[]> inbox = new Queue<byte[]>();
        public Connection Connection;
        public int Id { get; set; }
        public bool Connected { get; private set; }
        public string CloseReason { get; private set; }
        public string EndPoint { get; set; } = "steam";
        System.DateTime lastReceive = System.DateTime.UtcNow;
        public double IdleSeconds => (System.DateTime.UtcNow - lastReceive).TotalSeconds;

        public void Opened() => Connected = true;

        public void Closed(string reason)
        {
            Connected = false;
            CloseReason = reason;
        }

        public void Enqueue(IntPtr data, int size)
        {
            var bytes = new byte[size];
            System.Runtime.InteropServices.Marshal.Copy(data, bytes, 0, size);
            inbox.Enqueue(bytes);
            lastReceive = System.DateTime.UtcNow;
        }

        public void Send(byte[] message)
        {
            if (Connected) Connection.SendMessage(message, SendType.Reliable);
        }

        public void Poll(List<byte[]> messages)
        {
            while (inbox.Count > 0) messages.Add(inbox.Dequeue());
        }

        public void Close(string reason = "closed")
        {
            if (!Connected) return;
            Closed(reason);
            Connection.Close();
        }
    }

    public class CubedSocketManager : SocketManager
    {
        readonly Dictionary<uint, SteamLink> links = new Dictionary<uint, SteamLink>();

        public override void OnConnected(Connection connection, ConnectionInfo info)
        {
            base.OnConnected(connection, info);
            var link = new SteamLink { Connection = connection, EndPoint = info.Identity.SteamId.ToString() };
            link.Opened();
            links[connection.Id] = link;
            NetSession.Instance?.AcceptLink(link);
        }

        public override void OnDisconnected(Connection connection, ConnectionInfo info)
        {
            base.OnDisconnected(connection, info);
            if (links.TryGetValue(connection.Id, out var link)) link.Closed("left Steam session");
            links.Remove(connection.Id);
        }

        public override void OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            if (links.TryGetValue(connection.Id, out var link)) link.Enqueue(data, size);
        }
    }

    public class CubedConnectionManager : ConnectionManager
    {
        public readonly SteamLink Link = new SteamLink();

        public override void OnConnected(ConnectionInfo info)
        {
            base.OnConnected(info);
            Link.Connection = Connection;
            Link.Opened();
        }

        public override void OnDisconnected(ConnectionInfo info)
        {
            base.OnDisconnected(info);
            Link.Closed("Steam connection closed");
        }

        public override void OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel) => Link.Enqueue(data, size);
    }
}
#else
namespace Cubed.Net
{
    /// <summary>Stub used when the game is built without CUBED_STEAM.</summary>
    public static class SteamIntegration
    {
        public static bool Available => false;
        public static string Status => "Steam support not included in this build";
        public static string PlayerName => null;
        public static void Init() { }
        public static void Update() { }
        public static void Shutdown() { }
        public static void SetRichPresence(string status) { }
        public static void HostLobby() { }
        public static void InviteFriends() { }
        public static void LeaveLobby() { }
    }
}
#endif
