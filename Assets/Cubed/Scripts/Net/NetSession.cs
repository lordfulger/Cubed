using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cubed.Net
{
    public enum NetRole { Offline, Host, Client }

    /// <summary>
    /// Online play. The host is authoritative: it runs the whole simulation (including bots and card picks)
    /// and streams snapshots; clients render them and send their inputs and card choices.
    /// Mods only need to be installed on the host for gameplay, but clients need the same cards to show
    /// their names/stats (missing cards appear as placeholders).
    /// </summary>
    public class NetSession : MonoBehaviour
    {
        public static NetSession Instance { get; private set; }

        public NetRole Role { get; private set; }
        public bool IsHosting => Role == NetRole.Host;
        public bool IsClient => Role == NetRole.Client;
        public string Status { get; private set; } = "";
        public string LastError { get; set; } = "";
        public readonly LanDiscovery Lan = new LanDiscovery();

        /// <summary>Host: every Fx/Sfx call during a match is mirrored to clients.</summary>
        public static bool Mirroring =>
            Instance != null && Instance.Role == NetRole.Host && Instance.remotes.Count > 0 &&
            GameManager.Instance != null && GameManager.Instance.InMatch;

        public static readonly HashSet<string> MirroredSounds = new HashSet<string>
            { "shoot", "hit", "block", "jump", "bounce", "explode", "death", "border", "zap", "win" };

        GameManager Gm => GameManager.Instance;

        // ---- host state
        class Remote
        {
            public INetLink Conn;
            public string Name = "";
            public bool Welcomed;
            public int Token;
        }

        /// <summary>Reconnect token → player index for clients that dropped mid-match.</summary>
        readonly Dictionary<int, int> droppedPlayers = new Dictionary<int, int>();
        readonly System.Random tokenRng = new System.Random();

        /// <summary>Stable fingerprint of installed content, to warn about mod mismatches.</summary>
        public static string ContentHash()
        {
            uint h = 2166136261;
            foreach (var id in CardRegistry.All.Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal))
                foreach (char ch in id) { h ^= ch; h *= 16777619; }
            return CardRegistry.All.Count + "-" + h.ToString("x8");
        }

        NetServer server;
        readonly Dictionary<int, Remote> remotes = new Dictionary<int, Remote>();
        float snapshotTimer, lanTimer, lobbyTimer, pickSendTimer;
        string lastLobbyJson = "";
        NetWriter fxBody;
        int fxCount;
        public int Port => server != null ? server.Port : 0;
        public int ClientCount => remotes.Values.Count(r => r.Welcomed && r.Conn.Connected);

        // ---- client state
        NetConnector connector;
        INetLink host;
        public int MyId { get; private set; } = -1;
        public MatchSettings LobbySettings { get; private set; }
        public string HostName { get; private set; } = "";
        readonly Dictionary<int, NetBulletView> bulletViews = new Dictionary<int, NetBulletView>();
        int pendingButtons;
        float inputTimer, pingTimer, reconnectTimer;
        int reconnectToken, reconnectTries;
        string lastAddress;
        int lastPort;
        /// <summary>Smoothed round-trip time to the host in seconds (clients).</summary>
        public float Rtt { get; private set; }
        public bool Reconnecting => reconnectTries > 0 && reconnectTries <= 6;

        readonly List<byte[]> inbox = new List<byte[]>();

        void Awake()
        {
            Instance = this;
            GameEvents.MatchStarted += OnMatchStarted;
            GameEvents.RoundStarted += OnRoundStarted;
            GameEvents.CardPicked += OnCardPicked;
            GameEvents.PickPhaseStarted += OnPickPhaseStarted;
        }

        void OnDestroy()
        {
            GameEvents.MatchStarted -= OnMatchStarted;
            GameEvents.RoundStarted -= OnRoundStarted;
            GameEvents.CardPicked -= OnCardPicked;
            GameEvents.PickPhaseStarted -= OnPickPhaseStarted;
            Shutdown();
            if (Instance == this) Instance = null;
        }

        void OnApplicationQuit() => Shutdown();

        void Shutdown()
        {
            server?.Stop();
            server = null;
            remotes.Clear();
            connector?.Cancel();
            host?.Close("quit");
            host = null;
            Lan.Stop();
            Role = NetRole.Offline;
        }

        // ================================================================== host

        public bool StartHost(int port)
        {
            Leave();
            try
            {
                server = new NetServer();
                server.Start(port);
            }
            catch (Exception e)
            {
                server = null;
                LastError = "Could not host on port " + port + ": " + e.Message;
                return false;
            }
            Role = NetRole.Host;
            LastError = "";
            Status = "Hosting on port " + server.Port;
            lastLobbyJson = "";
            return true;
        }

        public void StopHost()
        {
            if (!IsHosting) return;
            foreach (var r in remotes.Values) { SendReject(r.Conn, "Host closed the lobby"); r.Conn.Close("host closed"); }
            server.Stop();
            server = null;
            remotes.Clear();
            Gm.Settings.slots.RemoveAll(s => s.IsOnline);
            Role = NetRole.Offline;
            Status = "";
        }

        public void Kick(int owner)
        {
            if (!IsHosting || !remotes.TryGetValue(owner, out var r)) return;
            SendReject(r.Conn, "Removed by host");
            r.Conn.Close("kicked");
        }

        static void SendReject(INetLink c, string reason)
        {
            if (c == null || !c.Connected) return;
            c.Send(new NetWriter(Msg.Reject).Str(reason).ToArray());
        }

        void HostUpdate()
        {
            foreach (var c in server.Accept())
                remotes[c.Id] = new Remote { Conn = c };

            foreach (var r in remotes.Values.ToList())
            {
                inbox.Clear();
                r.Conn.Poll(inbox);
                foreach (var m in inbox)
                {
                    try { HostHandle(r, new NetReader(m)); }
                    catch (Exception e) { Debug.LogWarning("[Cubed.Net] Bad message from " + r.Conn.EndPoint + ": " + e.Message); }
                }
                if (!r.Conn.Connected) HostDrop(r);
            }

            lanTimer -= Time.unscaledDeltaTime;
            if (lanTimer <= 0f)
            {
                lanTimer = 1f;
                Lan.Broadcast(Gm.Settings.onlineName + "'s game", server.Port, Gm.Settings.ActiveSlots.Count(), Gm.InMatch);
            }
        }

        void SendWelcome(Remote r, string clientContent)
        {
            string mine = ContentHash();
            r.Conn.Send(new NetWriter(Msg.Welcome).Int(r.Conn.Id).Int(r.Token).Bool(clientContent == mine).Int(CardRegistry.All.Count).ToArray());
        }

        void HostDrop(Remote r)
        {
            remotes.Remove(r.Conn.Id);
            if (!r.Welcomed) return;
            int id = r.Conn.Id;
            if (Gm.InMatch)
                foreach (var p in Gm.Players)
                    if (p.Input is NetworkInput ni && ni.Owner == id) droppedPlayers[r.Token] = p.Index;
            Gm.Settings.slots.RemoveAll(s => s.IsOnline && s.netOwner == id);
            if (Gm.InMatch) Gm.ReplaceRemoteWithBot(id);
            CubedHud.AddFeedText(r.Name + " left (" + r.Conn.CloseReason + ")");
        }

        void HostHandle(Remote r, NetReader m)
        {
            int id = r.Conn.Id;
            switch (m.Type)
            {
                case Msg.Hello:
                {
                    int protocol = m.Int();
                    string version = m.Str();
                    string name = m.Str().Trim();
                    string hat = m.Str();
                    int token = m.Int();
                    string content = m.Str();
                    if (name.Length == 0) name = "Player " + id;
                    if (name.Length > 16) name = name.Substring(0, 16);
                    if (protocol != NetConst.Protocol || version != CubedAPI.GameVersion)
                    {
                        SendReject(r.Conn, "Version mismatch: host runs " + CubedAPI.GameVersion);
                        r.Conn.Close("version mismatch");
                        return;
                    }
                    if (Gm.InMatch && token != 0 && droppedPlayers.TryGetValue(token, out int playerIndex))
                    {
                        // Reconnect: hand the player back from the bot that was covering for them.
                        droppedPlayers.Remove(token);
                        var p = Gm.Players.FirstOrDefault(x => x.Index == playerIndex);
                        r.Name = name;
                        r.Welcomed = true;
                        r.Token = token;
                        if (p != null)
                        {
                            var ni = new NetworkInput(id);
                            ni.OnSpawn(p, p.AimDirection);
                            p.Input = ni;
                            p.Slot.control = ControlType.Online;
                            p.Slot.netOwner = id;
                            p.DisplayName = name;
                            Gm.Settings.slots.Add(new PlayerSlot { name = name, control = ControlType.Online, team = p.Team, netOwner = id, hat = p.Slot.hat });
                        }
                        SendWelcome(r, content);
                        r.Conn.Send(BuildFullSync());
                        CubedHud.AddFeedText(name + " reconnected");
                        return;
                    }
                    if (Gm.Settings.slots.Count >= 8)
                    {
                        SendReject(r.Conn, "Lobby is full");
                        r.Conn.Close("full");
                        return;
                    }
                    r.Name = name;
                    r.Welcomed = true;
                    r.Token = tokenRng.Next(1, int.MaxValue);
                    var slot = new PlayerSlot { name = name, control = ControlType.Online, team = PickTeam(), netOwner = id, hat = hat ?? "" };
                    Gm.Settings.slots.Add(slot);
                    SendWelcome(r, content);
                    lastLobbyJson = "";
                    if (Gm.InMatch)
                    {
                        // Late join: spectate now, play from the next round.
                        Gm.QueueLateJoin(slot);
                        r.Conn.Send(BuildFullSync());
                        CubedHud.AddFeedText(name + " joined (plays next round)");
                    }
                    else CubedHud.AddFeedText(name + " joined");
                    Sfx.Play("pick", 0.5f, 0f);
                    break;
                }
                case Msg.TeamRequest:
                {
                    int team = Mathf.Clamp(m.Byte(), 0, Teams.Max - 1);
                    if (Gm.InMatch) break;
                    foreach (var s in Gm.Settings.slots)
                        if (s.IsOnline && s.netOwner == id) s.team = team;
                    break;
                }
                case Msg.Input:
                {
                    var move = m.Vec3();
                    var aim = m.Vec3();
                    int buttons = m.Byte();
                    float rtt = m.Float();
                    foreach (var p in Gm.Players)
                        if (p.Input is NetworkInput ni && ni.Owner == id)
                        {
                            ni.Receive(move, aim, buttons);
                            ni.Rtt = Mathf.Clamp(rtt, 0f, 1f);
                        }
                    break;
                }
                case Msg.Ping:
                    r.Conn.Send(new NetWriter(Msg.Pong).Float(m.Float()).ToArray());
                    break;
                case Msg.PickChoice:
                case Msg.PickHighlight:
                {
                    int playerIndex = m.Byte();
                    int card = m.Byte();
                    var st = Gm.Picks.FirstOrDefault(x => x.Player.Index == playerIndex);
                    if (st == null || !(st.Player.Input is NetworkInput owner) || owner.Owner != id) break;
                    if (m.Type == Msg.PickHighlight) st.Highlight = Mathf.Clamp(card, 0, Mathf.Max(0, st.Hand.Count - 1));
                    else Gm.Pick(st, card);
                    pickSendTimer = 0f;
                    break;
                }
            }
        }

        int PickTeam()
        {
            var counts = new int[Teams.Max];
            foreach (var s in Gm.Settings.ActiveSlots) counts[s.team]++;
            if (counts.Count(c => c > 0) < 2) return Array.FindIndex(counts, c => c == 0);
            int best = -1;
            for (int t = 0; t < Teams.Max; t++)
                if (counts[t] > 0 && (best < 0 || counts[t] < counts[best])) best = t;
            return Mathf.Max(0, best);
        }

        void HostLateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            FlushFx();
            if (remotes.Count == 0) return;

            if (!Gm.InMatch)
            {
                lobbyTimer -= dt;
                if (lobbyTimer <= 0f)
                {
                    lobbyTimer = 0.25f;
                    string json = JsonUtility.ToJson(Gm.Settings);
                    if (json != lastLobbyJson)
                    {
                        lastLobbyJson = json;
                        Broadcast(new NetWriter(Msg.Lobby).Str(json).Str(Gm.Settings.onlineName).ToArray());
                    }
                }
                return;
            }

            snapshotTimer -= dt;
            if (snapshotTimer <= 0f)
            {
                snapshotTimer = 1f / NetConst.SnapshotRate;
                Broadcast(BuildSnapshot());
            }

            if (Gm.State == GameState.CardPick)
            {
                pickSendTimer -= dt;
                if (pickSendTimer <= 0f)
                {
                    pickSendTimer = 0.2f;
                    Broadcast(BuildPickState());
                }
            }
        }

        void OnMatchStarted(MatchSettings s)
        {
            if (!IsHosting || server == null) return;
            droppedPlayers.Clear();
            Broadcast(BuildFullSync());
            snapshotTimer = 0f;
        }

        /// <summary>Host: resend the whole match (roster changed after late joins).</summary>
        public void BroadcastFullSync()
        {
            if (!IsHosting || server == null) return;
            Broadcast(BuildFullSync());
        }

        /// <summary>Everything a client needs to (re)build the match: rules, map, roster with cards, scores.</summary>
        byte[] BuildFullSync()
        {
            var w = new NetWriter(Msg.MatchStart)
                .Str(JsonUtility.ToJson(Gm.Settings))
                .Str(Gm.Map != null ? Gm.Map.Data.ToJson(false) : "");
            w.Byte(Gm.Players.Count);
            foreach (var p in Gm.Players)
            {
                int owner = p.Input is NetworkInput ni ? ni.Owner : -1;
                w.Byte(p.Index).Str(p.DisplayName).Byte(p.Team).Int(owner).Bool(p.IsBot).Str(p.Slot != null ? p.Slot.hat : "");
                w.Byte(p.Cards.Count);
                foreach (var c in p.Cards) w.Str(c.Id);
            }
            w.UShort(Gm.RoundNumber);
            for (int i = 0; i < Teams.Max; i++) w.Byte(Gm.TeamScores[i]);
            return w.ToArray();
        }

        void OnRoundStarted(int round)
        {
            if (!IsHosting || server == null || Gm.Map == null) return;
            Broadcast(new NetWriter(Msg.RoundStart).UShort(round).Str(Gm.Map.Data.ToJson(false)).ToArray());
            snapshotTimer = 0f;
        }

        void OnCardPicked(Player p, CardDefinition card)
        {
            if (!IsHosting || server == null || !Gm.InMatch) return;
            Broadcast(new NetWriter(Msg.CardApplied).Byte(p.Index).Str(card.Id).ToArray());
            pickSendTimer = 0f;
        }

        void OnPickPhaseStarted() => pickSendTimer = 0f;

        void Broadcast(byte[] message)
        {
            foreach (var r in remotes.Values)
                if (r.Conn.Connected) r.Conn.Send(message);
        }

        int nextLinkId = 100000;

        /// <summary>Host: accept a connection from another transport (Steam relay).</summary>
        public void AcceptLink(INetLink link)
        {
            if (!IsHosting) { link.Close("not hosting"); return; }
            link.Id = nextLinkId++;
            remotes[link.Id] = new Remote { Conn = link };
        }

        /// <summary>Client: use an already-connecting link (Steam relay) as the host connection.</summary>
        public void JoinLink(INetLink link, string description)
        {
            Leave();
            host = link;
            Role = NetRole.Client;
            LastError = "";
            Status = "Connecting via " + description + "...";
            MyId = -1;
            helloSent = false;
        }

        bool helloSent;

        public void BroadcastMatchAbort()
        {
            if (!IsHosting || server == null) return;
            Broadcast(new NetWriter(Msg.MatchAbort).ToArray());
            lastLobbyJson = "";
        }

        byte[] BuildSnapshot()
        {
            var w = new NetWriter(Msg.Snapshot);
            w.Byte((int)Gm.State).Float(Gm.StateTime).UShort(Gm.RoundNumber).Float(Gm.RoundTime)
             .SByte(Gm.LastRoundWinner).SByte(Gm.MatchWinner);
            for (int i = 0; i < Teams.Max; i++) w.Byte(Gm.TeamScores[i]);
            w.Bool(Gm.Paused).Float(Gm.Map != null ? Gm.Map.Shrink : 0f);
            w.SByte(Gm.HillHolder);
            for (int i = 0; i < Teams.Max; i++) w.Float(Gm.HillProgress[i]).Byte(Gm.RoundKills[i]);

            w.Byte(Gm.Players.Count);
            foreach (var p in Gm.Players)
            {
                var aim = p.AimDirection;
                float yaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
                float pitch = -Mathf.Asin(Mathf.Clamp(aim.y, -1f, 1f)) * Mathf.Rad2Deg;
                int flags = (p.IsAlive ? 1 : 0) | (p.IsBlocking ? 2 : 0) | (p.Grounded ? 4 : 0) | (p.IsSlowed ? 8 : 0)
                            | (p.IsPoisoned ? 16 : 0) | (p.InvulnerableTimer > 0f ? 32 : 0) | (p.Weapon.Reloading ? 64 : 0)
                            | (p.IsStunned ? 128 : 0);
                float cd = p.BlockCooldownTimer / Mathf.Max(0.01f, p.GetStat(Stat.BlockCooldown));
                w.Byte(p.Index).Vec3(p.transform.position).Vec3(p.Velocity).Float(yaw).Float(pitch).Float(p.Health)
                 .Byte(flags).Byte(p.Weapon.Ammo).Unit(p.Weapon.ReloadProgress).Unit(cd).Byte(p.LivesRemaining)
                 .UShort(p.Kills).UShort(p.Deaths).Float(p.DamageDealt).Float(p.Barrier);
            }

            int count = Mathf.Min(Bullet.Active.Count, 400);
            w.UShort(count);
            for (int i = 0; i < count; i++)
            {
                var b = Bullet.Active[i];
                w.Int(b.NetId).Vec3(b.Position).Vec3(b.Velocity).Float(b.Radius).Color(b.Color);
            }
            return w.ToArray();
        }

        byte[] BuildPickState()
        {
            var w = new NetWriter(Msg.PickState).Bool(Gm.StartingPickPhase).Byte(Gm.Picks.Count);
            foreach (var st in Gm.Picks)
            {
                w.Byte(st.Player.Index).Byte(st.PicksRemaining).Byte(st.PicksTotal).Byte(st.Highlight).Float(st.TimeLeft);
                w.Byte(st.Hand.Count);
                foreach (var c in st.Hand) w.Str(c.Id);
                w.Byte(st.PickedThisPhase.Count);
                foreach (var c in st.PickedThisPhase) w.Str(c.Id);
            }
            return w.ToArray();
        }

        // ---- effect mirroring (called from Fx / Sfx / CubedHud on the host)

        NetWriter FxWriter
        {
            get
            {
                if (fxBody == null) fxBody = new NetWriter(Msg.Fx);
                fxCount++;
                return fxBody;
            }
        }

        public void MirrorBurst(Vector3 pos, Color c, int count, float speed, float size, float life, float gravity) =>
            FxWriter.Byte((int)FxKind.Burst).Vec3(pos).Color(c).Byte(count).Float(speed).Float(size).Float(life).Float(gravity);

        public void MirrorPulse(Vector3 pos, Color c, float from, float to, float life, bool glow, float intensity) =>
            FxWriter.Byte((int)FxKind.Pulse).Vec3(pos).Color(c).Float(from).Float(to).Float(life).Bool(glow).Float(intensity);

        public void MirrorDecal(Vector3 point, Vector3 normal, float size, Color c) =>
            FxWriter.Byte((int)FxKind.Decal).Vec3(point).Vec3(normal).Float(size).Color(c);

        public void MirrorSmoke(Vector3 pos, int count, float size, Color c) =>
            FxWriter.Byte((int)FxKind.Smoke).Vec3(pos).Byte(count).Float(size).Color(c);

        public void MirrorLine(Vector3 a, Vector3 b, Color c, float width, float life) =>
            FxWriter.Byte((int)FxKind.Line).Vec3(a).Vec3(b).Color(c).Float(width).Float(life);

        public void MirrorSound(string id, float volume, Vector3? position)
        {
            var w = FxWriter.Byte((int)FxKind.Sound).Str(id).Float(volume).Bool(position.HasValue);
            if (position.HasValue) w.Vec3(position.Value);
        }

        public void MirrorLight(Vector3 pos, Color c, float range, float intensity, float life) =>
            FxWriter.Byte((int)FxKind.Light).Vec3(pos).Color(c).Float(range).Float(intensity).Float(life);

        public void MirrorShake(Vector3 pos, float strength) =>
            FxWriter.Byte((int)FxKind.Shake).Vec3(pos).Float(strength);

        public void MirrorDamageNumber(Vector3 pos, float amount, Color c) =>
            FxWriter.Byte((int)FxKind.DamageNumber).Vec3(pos).Float(amount).Color(c);

        public void MirrorBox(Vector3 pos, Quaternion rot, Vector3 size, Color c, float life) =>
            FxWriter.Byte((int)FxKind.Box).Vec3(pos).Vec3(rot.eulerAngles).Vec3(size).Color(c).Float(life);

        public void MirrorFeed(string text) =>
            FxWriter.Byte((int)FxKind.Feed).Str(text);

        void FlushFx()
        {
            if (fxBody == null) return;
            var body = fxBody.ToArray();
            int count = fxCount;
            fxBody = null;
            fxCount = 0;
            if (remotes.Count == 0 || count == 0) return;
            var w = new NetWriter(Msg.Fx).UShort(count).Bytes(body, 1, body.Length - 1);
            Broadcast(w.ToArray());
        }

        // ================================================================== client

        public void Join(string address, int port) => Join(address, port, false);

        void Join(string address, int port, bool reconnect)
        {
            if (!reconnect) { Leave(); reconnectToken = 0; reconnectTries = 0; }
            lastAddress = address;
            lastPort = port;
            connector = new NetConnector();
            connector.Begin(address, port);
            if (connector.Error != null)
            {
                LastError = connector.Error;
                connector = null;
                return;
            }
            Role = NetRole.Client;
            LastError = "";
            Status = "Connecting to " + address + ":" + port + "...";
            MyId = -1;
            LobbySettings = null;
        }

        /// <summary>Leave whatever session we're in (client) or close the lobby (host).</summary>
        public void Leave()
        {
            if (IsHosting) StopHost();
            reconnectTries = 0;
            reconnectToken = 0;
            if (!IsClient) return;
            connector?.Cancel();
            connector = null;
            host?.Close("left");
            host = null;
            Role = NetRole.Offline;
            Status = "";
            MyId = -1;
            LobbySettings = null;
            if (Gm != null && Gm.NetClientMode) Gm.ClientExitMatch(GameState.Online);
            ClearBulletViews();
        }

        public void RequestTeam(int team)
        {
            host?.Send(new NetWriter(Msg.TeamRequest).Byte(team).ToArray());
        }

        public void SendPickChoice(int playerIndex, int cardIndex) =>
            host?.Send(new NetWriter(Msg.PickChoice).Byte(playerIndex).Byte(cardIndex).ToArray());

        public void SendPickHighlight(int playerIndex, int cardIndex) =>
            host?.Send(new NetWriter(Msg.PickHighlight).Byte(playerIndex).Byte(cardIndex).ToArray());

        void UpdateReconnect()
        {
            if (!Reconnecting || IsClient) return;
            reconnectTimer -= Time.unscaledDeltaTime;
            if (reconnectTimer > 0f) return;
            reconnectTimer = 3f;
            reconnectTries++;
            if (reconnectTries > 6) { LastError = "Could not reconnect to the host."; return; }
            Status = "Reconnecting (attempt " + (reconnectTries - 1) + ")...";
            Join(lastAddress, lastPort, true);
        }

        void ClientUpdate()
        {
            if (connector != null)
            {
                if (!connector.Poll()) return;
                if (connector.Connection == null)
                {
                    LastError = "Could not connect: " + connector.Error;
                    connector = null;
                    Role = NetRole.Offline;
                    Status = "";
                    return;
                }
                host = connector.Connection;
                connector = null;
                helloSent = false;
            }
            if (host == null) return;
            if (!helloSent && host.Connected)
            {
                helloSent = true;
                Status = "Connected, waiting for the host...";
                host.Send(new NetWriter(Msg.Hello).Int(NetConst.Protocol).Str(CubedAPI.GameVersion).Str(Gm.Settings.onlineName)
                    .Str(Profile.Current.hat).Int(reconnectToken).Str(ContentHash()).ToArray());
            }
            if (host == null) return;

            inbox.Clear();
            host.Poll(inbox);
            foreach (var m in inbox)
            {
                try { ClientHandle(new NetReader(m)); }
                catch (Exception e) { Debug.LogWarning("[Cubed.Net] Bad message from host: " + e); }
                if (host == null) return;
            }
            if (!host.Connected)
            {
                if (string.IsNullOrEmpty(LastError)) LastError = "Disconnected: " + host.CloseReason;
                // Dropped mid-match (not rejected/kicked): try to get back in with our token.
                if (Gm.NetClientMode && reconnectToken != 0 && host.CloseReason != "rejected")
                {
                    reconnectTries = 1;
                    reconnectTimer = 2f;
                    Toasts.Show("Connection lost", "Reconnecting...", UIKit.Bad);
                }
                var wasInGame = Gm.NetClientMode || Gm.State == GameState.Lobby;
                host = null;
                Role = NetRole.Offline;
                Status = "";
                if (Gm.NetClientMode) Gm.ClientExitMatch(GameState.Online);
                else if (wasInGame) Gm.SetState(GameState.Online);
                ClearBulletViews();
            }
        }

        void ClientHandle(NetReader m)
        {
            switch (m.Type)
            {
                case Msg.Welcome:
                {
                    MyId = m.Int();
                    reconnectToken = m.Int();
                    bool same = m.Bool();
                    int hostCards = m.Int();
                    Status = "In lobby";
                    if (!same)
                        Toasts.Show("Mods differ from the host", "Host has " + hostCards + " cards, you have " + CardRegistry.All.Count + ". Missing cards show as placeholders.", UIKit.Bad);
                    break;
                }
                case Msg.Pong:
                {
                    float sample = Time.unscaledTime - m.Float();
                    Rtt = Rtt <= 0f ? sample : Mathf.Lerp(Rtt, sample, 0.25f);
                    break;
                }

                case Msg.Reject:
                    LastError = m.Str();
                    host.Close("rejected");
                    break;

                case Msg.Lobby:
                {
                    var s = JsonUtility.FromJson<MatchSettings>(m.Str());
                    s?.Validate();
                    LobbySettings = s;
                    HostName = m.Str();
                    if (!Gm.InMatch && Gm.State != GameState.Lobby) Gm.SetState(GameState.Lobby);
                    break;
                }

                case Msg.MatchStart:
                {
                    var rules = JsonUtility.FromJson<MatchSettings>(m.Str());
                    string mapJson = m.Str();
                    var map = string.IsNullOrEmpty(mapJson) ? null : MapData.FromJson(mapJson);
                    int n = m.Byte();
                    var infos = new List<GameManager.NetPlayerInfo>();
                    var cards = new List<List<string>>();
                    for (int i = 0; i < n; i++)
                    {
                        var info = new GameManager.NetPlayerInfo { Index = m.Byte(), Name = m.Str(), Team = m.Byte() };
                        int owner = m.Int();
                        info.Bot = m.Bool();
                        info.Hat = m.Str();
                        info.Local = owner >= 0 && owner == MyId;
                        infos.Add(info);
                        var list = new List<string>();
                        int cc = m.Byte();
                        for (int k = 0; k < cc; k++) list.Add(m.Str());
                        cards.Add(list);
                    }
                    int round = m.UShort();
                    var scores = new int[Teams.Max];
                    for (int i = 0; i < Teams.Max; i++) scores[i] = m.Byte();
                    ClearBulletViews();
                    Gm.ClientStartMatch(rules, infos, map);
                    for (int i = 0; i < infos.Count; i++)
                    {
                        var p = FindPlayer(infos[i].Index);
                        if (p != null) foreach (var id in cards[i]) p.ApplyCard(ResolveCard(id), false);
                    }
                    Gm.ClientSetState(GameState.RoundIntro, 0f, round, 0f, -1, -1, scores, false);
                    reconnectTries = 0;
                    if (!infos.Any(x => x.Local)) Toasts.Show("Spectating", "You'll join the fight next round.");
                    break;
                }

                case Msg.RoundStart:
                {
                    m.UShort();
                    var map = MapData.FromJson(m.Str());
                    ClearBulletViews();
                    if (map != null) Gm.ClientLoadMap(map);
                    break;
                }

                case Msg.Snapshot:
                    ApplySnapshot(m);
                    break;

                case Msg.CardApplied:
                {
                    var p = FindPlayer(m.Byte());
                    var card = ResolveCard(m.Str());
                    if (p != null) p.ApplyCard(card, false);
                    break;
                }

                case Msg.PickState:
                {
                    bool starting = m.Bool();
                    int n = m.Byte();
                    var list = new List<PickState>();
                    for (int i = 0; i < n; i++)
                    {
                        var p = FindPlayer(m.Byte());
                        var st = new PickState { Player = p, PicksRemaining = m.Byte(), PicksTotal = m.Byte(), Highlight = m.Byte(), TimeLeft = m.Float() };
                        int hand = m.Byte();
                        for (int k = 0; k < hand; k++) st.Hand.Add(ResolveCard(m.Str()));
                        int picked = m.Byte();
                        for (int k = 0; k < picked; k++) st.PickedThisPhase.Add(ResolveCard(m.Str()));
                        st.LastPicked = st.PickedThisPhase.LastOrDefault();
                        if (p != null) list.Add(st);
                    }
                    if (Gm.NetClientMode) Gm.ClientSetPicks(list, starting);
                    break;
                }

                case Msg.Fx:
                    ApplyFx(m);
                    break;

                case Msg.MatchAbort:
                    if (Gm.NetClientMode) Gm.ClientExitMatch(GameState.Lobby);
                    ClearBulletViews();
                    break;
            }
        }

        Player FindPlayer(int index) => Gm.Players.FirstOrDefault(p => p.Index == index);

        static CardDefinition ResolveCard(string id)
        {
            var c = CardRegistry.Get(id);
            if (c != null) return c;
            return new CardDefinition(id, id, "This card comes from a mod you don't have installed.", Rarity.Common, Color.gray) { Source = "Missing mod" };
        }

        void ApplySnapshot(NetReader m)
        {
            if (!Gm.NetClientMode) return;
            var state = (GameState)m.Byte();
            float stateTime = m.Float();
            int round = m.UShort();
            float roundTime = m.Float();
            int lastWinner = m.SByte();
            int matchWinner = m.SByte();
            var scores = new int[Teams.Max];
            for (int i = 0; i < Teams.Max; i++) scores[i] = m.Byte();
            bool paused = m.Bool();
            float shrink = m.Float();
            int holder = m.SByte();
            var hill = new float[Teams.Max];
            var teamKills = new int[Teams.Max];
            for (int i = 0; i < Teams.Max; i++) { hill[i] = m.Float(); teamKills[i] = m.Byte(); }
            Gm.ClientSetModeState(holder, hill, teamKills);
            Gm.ClientSetState(state, stateTime, round, roundTime, lastWinner, matchWinner, scores, paused);
            if (Gm.Map != null) Gm.Map.SetShrink(shrink);

            int n = m.Byte();
            for (int i = 0; i < n; i++)
            {
                int index = m.Byte();
                var pos = m.Vec3();
                var vel = m.Vec3();
                float yaw = m.Float(), pitch = m.Float(), health = m.Float();
                int flags = m.Byte();
                int ammo = m.Byte();
                float reload = m.Unit(), cd = m.Unit();
                int lives = m.Byte(), kills = m.UShort(), deaths = m.UShort();
                float dmg = m.Float();
                float barrier = m.Float();
                var p = FindPlayer(index);
                if (p == null) continue;
                p.Frozen = state != GameState.Fighting;
                p.NetApply(pos, vel, yaw, pitch, health, (flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0, (flags & 8) != 0,
                    (flags & 16) != 0, (flags & 32) != 0, ammo, (flags & 64) != 0, reload, cd, lives, kills, deaths, dmg,
                    (flags & 128) != 0, barrier);
            }

            int bullets = m.UShort();
            var seen = new HashSet<int>();
            for (int i = 0; i < bullets; i++)
            {
                int id = m.Int();
                var pos = m.Vec3();
                var vel = m.Vec3();
                float radius = m.Float();
                var color = m.Color();
                seen.Add(id);
                if (!bulletViews.TryGetValue(id, out var view) || view == null)
                {
                    var go = new GameObject("NetBullet");
                    go.transform.position = pos;
                    Bullet.CreateVisual(go, radius, color);
                    view = go.AddComponent<NetBulletView>();
                    bulletViews[id] = view;
                }
                view.Target = pos;
                view.Velocity = vel;
            }
            foreach (var id in bulletViews.Keys.Where(k => !seen.Contains(k)).ToList())
            {
                if (bulletViews[id] != null) Destroy(bulletViews[id].gameObject);
                bulletViews.Remove(id);
            }
        }

        void ApplyFx(NetReader m)
        {
            int n = m.UShort();
            for (int i = 0; i < n; i++)
            {
                switch ((FxKind)m.Byte())
                {
                    case FxKind.Burst:
                    {
                        var pos = m.Vec3(); var c = m.Color(); int count = m.Byte();
                        float speed = m.Float(), size = m.Float(), life = m.Float(), gravity = m.Float();
                        Fx.Burst(pos, c, count, speed, size, life, gravity);
                        break;
                    }
                    case FxKind.Pulse:
                    {
                        var pos = m.Vec3(); var c = m.Color();
                        float from = m.Float(), to = m.Float(), life = m.Float();
                        bool glow = m.Bool();
                        float intensity = m.Float();
                        Fx.Pulse(pos, c, from, to, life, glow, intensity);
                        break;
                    }
                    case FxKind.Line:
                    {
                        var a = m.Vec3(); var b = m.Vec3(); var c = m.Color();
                        float width = m.Float(), life = m.Float();
                        Fx.Line(a, b, c, width, life);
                        break;
                    }
                    case FxKind.Sound:
                    {
                        string id = m.Str();
                        float vol = m.Float();
                        if (m.Bool()) Sfx.PlayAt(id, m.Vec3(), vol);
                        else Sfx.Play(id, vol);
                        break;
                    }
                    case FxKind.Light:
                    {
                        var pos = m.Vec3(); var c = m.Color(); float range = m.Float(), intensity = m.Float(), life = m.Float();
                        Fx.Light(pos, c, range, intensity, life);
                        break;
                    }
                    case FxKind.Shake:
                    {
                        var pos = m.Vec3();
                        CameraShake.Impulse(pos, m.Float());
                        break;
                    }
                    case FxKind.DamageNumber:
                    {
                        var pos = m.Vec3(); float amount = m.Float(); var c = m.Color();
                        CubedHud.AddDamageNumber(pos, amount, c);
                        break;
                    }
                    case FxKind.Box:
                    {
                        var pos = m.Vec3(); var euler = m.Vec3(); var size = m.Vec3(); var c = m.Color(); float life = m.Float();
                        Fx.Box(pos, Quaternion.Euler(euler), size, c, life, false);
                        break;
                    }
                    case FxKind.Decal:
                    {
                        var p = m.Vec3(); var nrm = m.Vec3(); float size = m.Float(); var c = m.Color();
                        Fx.Decal(p, nrm, size, c);
                        break;
                    }
                    case FxKind.Smoke:
                    {
                        var p = m.Vec3(); int count = m.Byte(); float size = m.Float(); var c = m.Color();
                        Fx.Smoke(p, count, size, c);
                        break;
                    }
                    case FxKind.Feed:
                        CubedHud.AddFeedText(m.Str());
                        break;
                    default:
                        return; // unknown kind: can't continue parsing safely
                }
            }
        }

        public void ClearBulletViews()
        {
            foreach (var v in bulletViews.Values) if (v != null) Destroy(v.gameObject);
            bulletViews.Clear();
        }

        void ClientLateUpdate()
        {
            if (host == null) return;
            pingTimer -= Time.unscaledDeltaTime;
            if (pingTimer <= 0f)
            {
                pingTimer = 1f;
                host.Send(new NetWriter(Msg.Ping).Float(Time.unscaledTime).ToArray());
            }
            if (!Gm.NetClientMode) return;
            var local = Gm.Players.FirstOrDefault(p => p.Input is HumanInput);
            if (local == null) return;
            var hi = (HumanInput)local.Input;
            bool blocked = Gm.Paused;
            if (!blocked)
            {
                if (hi.JumpPressed) pendingButtons |= NetworkInput.JumpBit;
                if (hi.BlockPressed) pendingButtons |= NetworkInput.BlockBit;
                if (hi.ReloadPressed) pendingButtons |= NetworkInput.ReloadBit;
            }
            inputTimer -= Time.unscaledDeltaTime;
            if (inputTimer > 0f) return;
            inputTimer = 1f / 60f;
            int buttons = pendingButtons | (!blocked && hi.FireHeld ? NetworkInput.FireBit : 0);
            pendingButtons = 0;
            host.Send(new NetWriter(Msg.Input).Vec3(blocked ? Vector3.zero : hi.Move).Vec3(hi.AimPoint).Byte(buttons).Float(Rtt).ToArray());
        }

        // ================================================================== loop

        void Update()
        {
            if (Gm == null) return;
            if (IsHosting && server != null) HostUpdate();
            else if (IsClient) ClientUpdate();
            else UpdateReconnect();
            if (Gm.State == GameState.Online) Lan.Listen();
        }

        void LateUpdate()
        {
            if (Gm == null) return;
            if (IsHosting && server != null) HostLateUpdate();
            else if (IsClient) ClientLateUpdate();
        }

        /// <summary>This machine's IPv4 addresses, for telling friends where to connect.</summary>
        public static string LocalAddresses()
        {
            try
            {
                var list = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList
                    .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(a))
                    .Select(a => a.ToString()).ToArray();
                return list.Length > 0 ? string.Join(", ", list) : "127.0.0.1";
            }
            catch (Exception) { return "unknown"; }
        }
    }
}
