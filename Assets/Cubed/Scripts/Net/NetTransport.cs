using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Cubed.Net
{
    /// <summary>A message connection to one peer (TCP here; Steam relay in the optional Steam module).</summary>
    public interface INetLink
    {
        int Id { get; set; }
        bool Connected { get; }
        string CloseReason { get; }
        string EndPoint { get; }
        /// <summary>Seconds since anything was received (used for silence timeouts).</summary>
        double IdleSeconds { get; }
        void Send(byte[] message);
        void Poll(List<byte[]> messages);
        void Close(string reason = "closed");
    }

    /// <summary>
    /// One TCP connection with length-prefixed framing. Non-blocking: call <see cref="Poll"/> every frame
    /// from the main thread; no background threads are used. (No UnityEngine dependency.)
    /// </summary>
    public class NetConnection : INetLink
    {
        public const int MaxMessage = 4 * 1024 * 1024;

        public int Id { get; set; }
        public string Name = "";
        public object Tag;
        public Socket Socket { get; private set; }
        public bool Connected { get; private set; }
        public string EndPoint { get; private set; } = "";
        public string CloseReason { get; private set; }

        readonly byte[] readBuffer = new byte[64 * 1024];
        byte[] incoming = new byte[64 * 1024];
        int incomingCount;
        byte[] outgoing = new byte[64 * 1024];
        int outgoingCount;

        public const int MaxOutgoing = 8 * 1024 * 1024;
        DateTime lastReceive = DateTime.UtcNow;
        public double IdleSeconds => (DateTime.UtcNow - lastReceive).TotalSeconds;

        public NetConnection(Socket socket)
        {
            Socket = socket;
            Socket.Blocking = false;
            Socket.NoDelay = true;
            Connected = true;
            try { EndPoint = socket.RemoteEndPoint?.ToString() ?? ""; } catch { }
        }

        public void Send(byte[] message) => Send(message, 0, message.Length);

        public void Send(byte[] data, int offset, int count)
        {
            if (!Connected) return;
            if (outgoingCount + count + 4 > MaxOutgoing) { Close("send buffer overflow (peer not reading)"); return; }
            EnsureCapacity(ref outgoing, outgoingCount + count + 4);
            outgoing[outgoingCount++] = (byte)count;
            outgoing[outgoingCount++] = (byte)(count >> 8);
            outgoing[outgoingCount++] = (byte)(count >> 16);
            outgoing[outgoingCount++] = (byte)(count >> 24);
            Buffer.BlockCopy(data, offset, outgoing, outgoingCount, count);
            outgoingCount += count;
            Flush();
        }

        static void EnsureCapacity(ref byte[] buffer, int needed)
        {
            if (buffer.Length >= needed) return;
            int size = buffer.Length;
            while (size < needed) size *= 2;
            Array.Resize(ref buffer, size);
        }

        void Flush()
        {
            while (Connected && outgoingCount > 0)
            {
                int sent = Socket.Send(outgoing, 0, outgoingCount, SocketFlags.None, out SocketError err);
                if (err == SocketError.WouldBlock || err == SocketError.NoBufferSpaceAvailable) return;
                if (err != SocketError.Success || sent <= 0) { Close("send failed: " + err); return; }
                Buffer.BlockCopy(outgoing, sent, outgoing, 0, outgoingCount - sent);
                outgoingCount -= sent;
            }
        }

        /// <summary>Reads everything available and returns complete messages.</summary>
        public void Poll(List<byte[]> messages)
        {
            if (!Connected) return;
            Flush();
            try
            {
                while (Connected)
                {
                    bool readable = Socket.Poll(0, SelectMode.SelectRead);
                    if (!readable) break;
                    int n = Socket.Receive(readBuffer, 0, readBuffer.Length, SocketFlags.None, out SocketError err);
                    if (err == SocketError.WouldBlock) break;
                    if (err != SocketError.Success) { Close("receive failed: " + err); break; }
                    if (n == 0) { Close("remote closed the connection"); break; }
                    lastReceive = DateTime.UtcNow;
                    EnsureCapacity(ref incoming, incomingCount + n);
                    Buffer.BlockCopy(readBuffer, 0, incoming, incomingCount, n);
                    incomingCount += n;
                }
            }
            catch (ObjectDisposedException) { Close("socket disposed"); }
            catch (SocketException e) { Close(e.Message); }

            int pos = 0;
            while (incomingCount - pos >= 4)
            {
                int len = incoming[pos] | (incoming[pos + 1] << 8) | (incoming[pos + 2] << 16) | (incoming[pos + 3] << 24);
                if (len < 0 || len > MaxMessage) { Close("bad message length"); return; }
                if (incomingCount - pos - 4 < len) break;
                var msg = new byte[len];
                Buffer.BlockCopy(incoming, pos + 4, msg, 0, len);
                messages.Add(msg);
                pos += 4 + len;
            }
            if (pos > 0)
            {
                Buffer.BlockCopy(incoming, pos, incoming, 0, incomingCount - pos);
                incomingCount -= pos;
            }
        }

        public void Close(string reason = "closed")
        {
            if (!Connected) return;
            Connected = false;
            CloseReason = reason;
            try { Socket.Shutdown(SocketShutdown.Both); } catch { }
            try { Socket.Close(); } catch { }
        }
    }

    /// <summary>Accepts connections on a TCP port.</summary>
    public class NetServer
    {
        Socket listener;
        int nextId = 1;
        public readonly List<NetConnection> Connections = new List<NetConnection>();
        public int Port { get; private set; }
        public bool Running => listener != null;

        public void Start(int port)
        {
            Stop();
            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            listener.Bind(new IPEndPoint(IPAddress.Any, port));
            listener.Listen(16);
            listener.Blocking = false;
            Port = ((IPEndPoint)listener.LocalEndPoint).Port;
        }

        /// <summary>Accept new connections. Returns the newly accepted ones.</summary>
        public List<NetConnection> Accept()
        {
            var accepted = new List<NetConnection>();
            if (listener == null) return accepted;
            while (true)
            {
                Socket s;
                try
                {
                    if (!listener.Poll(0, SelectMode.SelectRead)) break;
                    s = listener.Accept();
                }
                catch (SocketException) { break; }
                var c = new NetConnection(s) { Id = nextId++ };
                Connections.Add(c);
                accepted.Add(c);
            }
            return accepted;
        }

        public void Broadcast(byte[] message, NetConnection except = null)
        {
            foreach (var c in Connections)
                if (c != except && c.Connected) c.Send(message);
        }

        public void Stop()
        {
            foreach (var c in Connections) c.Close("server stopped");
            Connections.Clear();
            if (listener != null)
            {
                try { listener.Close(); } catch { }
                listener = null;
            }
        }
    }

    /// <summary>Non-blocking client connect with timeout.</summary>
    public class NetConnector
    {
        Socket socket;
        IAsyncResult pending;
        DateTime deadline;
        public NetConnection Connection { get; private set; }
        public string Error { get; private set; }
        public bool Busy => pending != null;

        public void Begin(string host, int port, double timeoutSeconds = 6)
        {
            Cancel();
            Error = null;
            Connection = null;
            IPAddress address;
            try
            {
                if (!IPAddress.TryParse(host.Trim(), out address))
                {
                    address = null;
                    foreach (var a in Dns.GetHostAddresses(host.Trim()))
                        if (a.AddressFamily == AddressFamily.InterNetwork) { address = a; break; }
                    if (address == null) { Error = "Could not resolve " + host; return; }
                }
            }
            catch (Exception e) { Error = "Could not resolve " + host + ": " + e.Message; return; }

            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try { pending = socket.BeginConnect(new IPEndPoint(address, port), null, null); }
            catch (Exception e) { Error = e.Message; socket.Close(); socket = null; return; }
            deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        }

        /// <summary>Returns true once finished (check Connection / Error).</summary>
        public bool Poll()
        {
            if (pending == null) return true;
            if (pending.IsCompleted)
            {
                try
                {
                    socket.EndConnect(pending);
                    Connection = new NetConnection(socket);
                }
                catch (Exception e)
                {
                    Error = e.Message;
                    try { socket.Close(); } catch { }
                }
                pending = null;
                socket = null;
                return true;
            }
            if (DateTime.UtcNow > deadline)
            {
                Error = "Connection timed out";
                Cancel();
                return true;
            }
            return false;
        }

        public void Cancel()
        {
            if (socket != null) { try { socket.Close(); } catch { } }
            socket = null;
            pending = null;
        }
    }

    /// <summary>LAN discovery: hosts broadcast a small UDP beacon, joiners listen for it.</summary>
    public class LanDiscovery
    {
        public const int Port = 27016;
        public const string Magic = "CUBED1";

        public class Found
        {
            public string Address;
            public int Port;
            public string Name;
            public int Players;
            public bool InMatch;
            public DateTime LastSeen;
        }

        UdpClient sender, listener;
        public readonly List<Found> Hosts = new List<Found>();

        public void Broadcast(string name, int gamePort, int players, bool inMatch)
        {
            try
            {
                if (sender == null) sender = new UdpClient { EnableBroadcast = true };
                var text = Magic + "|" + gamePort + "|" + players + "|" + (inMatch ? 1 : 0) + "|" + name.Replace("|", "/");
                var bytes = System.Text.Encoding.UTF8.GetBytes(text);
                sender.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, Port));
            }
            catch (Exception) { /* no network / broadcast not permitted */ }
        }

        public void Listen()
        {
            try
            {
                if (listener == null)
                {
                    listener = new UdpClient();
                    listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    listener.Client.Bind(new IPEndPoint(IPAddress.Any, Port));
                }
                while (listener.Available > 0)
                {
                    var from = new IPEndPoint(IPAddress.Any, 0);
                    var data = listener.Receive(ref from);
                    var parts = System.Text.Encoding.UTF8.GetString(data).Split(new[] { '|' }, 5);
                    if (parts.Length < 5 || parts[0] != Magic) continue;
                    if (!int.TryParse(parts[1], out int port)) continue;
                    int.TryParse(parts[2], out int players);
                    string addr = from.Address.ToString();
                    var f = Hosts.Find(h => h.Address == addr && h.Port == port);
                    if (f == null) { f = new Found { Address = addr, Port = port }; Hosts.Add(f); }
                    f.Players = players;
                    f.InMatch = parts[3] == "1";
                    f.Name = parts[4];
                    f.LastSeen = DateTime.UtcNow;
                }
                Hosts.RemoveAll(h => (DateTime.UtcNow - h.LastSeen).TotalSeconds > 4);
            }
            catch (Exception) { }
        }

        public void Stop()
        {
            try { sender?.Close(); } catch { }
            try { listener?.Close(); } catch { }
            sender = null;
            listener = null;
            Hosts.Clear();
        }
    }
}
