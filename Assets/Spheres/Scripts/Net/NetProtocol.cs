using System.IO;
using System.Text;
using UnityEngine;

namespace Spheres.Net
{
    public enum Msg : byte
    {
        // client -> host
        Hello = 1,
        TeamRequest,
        Input,
        PickChoice,
        PickHighlight,
        Ping,
        // host -> client
        Welcome = 32,
        Reject,
        Lobby,
        MatchStart,
        RoundStart,
        Snapshot,
        CardApplied,
        PickState,
        Fx,
        MatchAbort,
        Pong,
    }

    public enum FxKind : byte { Burst, Pulse, Line, Sound, DamageNumber, Feed, Box, Light, Shake, Decal, Smoke }

    public static class NetConst
    {
        /// <summary>Bump when the wire format changes.</summary>
        public const int Protocol = 8;
        public const int DefaultPort = 27015;
        public const float SnapshotRate = 30f;
    }

    public class NetWriter
    {
        readonly MemoryStream ms = new MemoryStream(256);
        readonly BinaryWriter w;

        public NetWriter(Msg type)
        {
            w = new BinaryWriter(ms, Encoding.UTF8);
            w.Write((byte)type);
        }

        public NetWriter Byte(int v) { w.Write((byte)Mathf.Clamp(v, 0, 255)); return this; }
        public NetWriter SByte(int v) { w.Write((sbyte)Mathf.Clamp(v, -128, 127)); return this; }
        public NetWriter Bool(bool v) { w.Write(v); return this; }
        public NetWriter UShort(int v) { w.Write((ushort)Mathf.Clamp(v, 0, 65535)); return this; }
        public NetWriter Int(int v) { w.Write(v); return this; }
        public NetWriter Float(float v) { w.Write(v); return this; }
        public NetWriter Str(string v) { w.Write(v ?? ""); return this; }
        public NetWriter Vec3(Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); return this; }
        public NetWriter Unit(float v) { w.Write((byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f)); return this; }
        public NetWriter Color(Color c)
        {
            Color32 k = c;
            w.Write(k.r); w.Write(k.g); w.Write(k.b); w.Write(k.a);
            return this;
        }
        public NetWriter Bytes(byte[] data, int offset, int count) { w.Write(data, offset, count); return this; }

        public long Length => ms.Length;
        public byte[] ToArray() { w.Flush(); return ms.ToArray(); }
    }

    public class NetReader
    {
        readonly BinaryReader r;
        public readonly Msg Type;

        public NetReader(byte[] data)
        {
            r = new BinaryReader(new MemoryStream(data), Encoding.UTF8);
            Type = (Msg)r.ReadByte();
        }

        public int Byte() => r.ReadByte();
        public int SByte() => r.ReadSByte();
        public bool Bool() => r.ReadBoolean();
        public int UShort() => r.ReadUInt16();
        public int Int() => r.ReadInt32();
        public float Float() => r.ReadSingle();
        public string Str() => r.ReadString();
        public Vector3 Vec3() => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        public float Unit() => r.ReadByte() / 255f;
        public Color Color() => new Color32(r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte());
        public bool AtEnd => r.BaseStream.Position >= r.BaseStream.Length;
    }

    /// <summary>Host-side input for a player controlled by a remote client.</summary>
    public class NetworkInput : IPlayerInput
    {
        public const byte FireBit = 1, JumpBit = 2, BlockBit = 4, ReloadBit = 8;

        public int Owner;
        /// <summary>Round-trip time reported by the client (seconds).</summary>
        public float Rtt;
        public Vector3 Move { get; private set; }
        public Vector3 AimPoint { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool FireHeld { get; private set; }
        public bool BlockPressed { get; private set; }
        public bool ReloadPressed { get; private set; }

        bool pendingJump, pendingBlock, pendingReload;

        public NetworkInput(int owner) { Owner = owner; }

        public void Receive(Vector3 move, Vector3 aim, int buttons)
        {
            move.y = 0f;
            Move = Vector3.ClampMagnitude(move, 1f);
            AimPoint = aim;
            FireHeld = (buttons & FireBit) != 0;
            if ((buttons & JumpBit) != 0) pendingJump = true;
            if ((buttons & BlockBit) != 0) pendingBlock = true;
            if ((buttons & ReloadBit) != 0) pendingReload = true;
        }

        public void Tick(Player player, float dt)
        {
            JumpPressed = pendingJump;
            BlockPressed = pendingBlock;
            ReloadPressed = pendingReload;
            pendingJump = pendingBlock = pendingReload = false;
        }

        public void OnSpawn(Player player, Vector3 lookDirection)
        {
            Move = Vector3.zero;
            FireHeld = false;
            AimPoint = player.Head.position + lookDirection * 20f;
        }
    }

    /// <summary>Client-side visual for a bullet simulated on the host.</summary>
    public class NetBulletView : MonoBehaviour
    {
        public Vector3 Velocity;
        public Vector3 Target;

        void Update()
        {
            float dt = Time.deltaTime;
            Target += Velocity * dt;
            transform.position = Vector3.Lerp(transform.position + Velocity * dt, Target, 1f - Mathf.Exp(-dt * 25f));
        }
    }
}
