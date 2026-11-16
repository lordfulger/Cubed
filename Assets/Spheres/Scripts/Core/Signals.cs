using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Spheres
{
    public enum SignalKind : byte { Chat = 0, PingSpot = 1, PingEnemy = 2 }

    /// <summary>
    /// Quick chat (a wheel of phrases everyone sees) and pings (markers only teammates see).
    /// Keyboard: hold V for the wheel (pick with WASD / mouse / 1-8), middle mouse or Q to ping.
    /// Gamepad: D-pad sends a phrase, right-stick click pings.
    /// </summary>
    public static class Signals
    {
        /// <summary>Clockwise from up. Mods may replace entries.</summary>
        public static readonly string[] Phrases = { "Nice!", "Behind you!", "Help!", "Push in!", "Thanks!", "Sorry!", "GG!", "Fall back!" };

        public class Signal
        {
            public Player From;
            public SignalKind Kind;
            public Vector3 Position;
            public int Phrase;
            public Player Target;
            public float Time;
        }

        public static readonly List<Signal> Active = new List<Signal>();
        static readonly Dictionary<Player, float> cooldown = new Dictionary<Player, float>();
        static bool hooked;

        public const float PingLife = 5f, EnemyPingLife = 3f, BubbleLife = 2.6f;

        static void Hook()
        {
            if (hooked) return;
            hooked = true;
            GameEvents.PlayerDied += OnDied;
            GameEvents.MatchEnded += OnMatchEnded;
            GameEvents.RoundStarted += _ => Active.Clear();
        }

        // ------------------------------------------------------------------ sending

        /// <summary>Send a signal from a local player (routes through the host when online).</summary>
        public static void Send(Player from, SignalKind kind, Vector3 pos, int phrase, Player target = null)
        {
            Hook();
            var gm = GameManager.Instance;
            if (from == null || gm == null || !gm.InMatch) return;
            if (cooldown.TryGetValue(from, out float until) && Time.unscaledTime < until) return;
            cooldown[from] = Time.unscaledTime + (kind == SignalKind.Chat ? 0.8f : 0.4f);
            if (gm.NetClientMode)
            {
                Net.NetSession.Instance?.SendSignal(from.Index, (int)kind, pos, phrase, target != null ? target.Index : 255);
                return;
            }
            Receive(from, kind, pos, phrase, target);
        }

        /// <summary>Host (or offline): accept a signal, show it, mirror it to clients.</summary>
        public static void Receive(Player from, SignalKind kind, Vector3 pos, int phrase, Player target)
        {
            Hook();
            if (from == null) return;
            phrase = Mathf.Clamp(phrase, 0, Phrases.Length - 1);
            if (Net.NetSession.Mirroring) Net.NetSession.Instance.MirrorSignal(from.Index, (int)kind, pos, phrase, target != null ? target.Index : 255);
            Show(from, kind, pos, phrase, target);
        }

        /// <summary>Display locally (also used by clients for mirrored signals).</summary>
        public static void Show(Player from, SignalKind kind, Vector3 pos, int phrase, Player target)
        {
            Hook();
            // Each player keeps at most 3 pings.
            if (kind != SignalKind.Chat)
            {
                var mine = Active.Where(s => s.From == from && s.Kind != SignalKind.Chat).ToList();
                while (mine.Count >= 3) { Active.Remove(mine[0]); mine.RemoveAt(0); }
            }
            else Active.RemoveAll(s => s.From == from && s.Kind == SignalKind.Chat);
            Active.Add(new Signal { From = from, Kind = kind, Position = pos, Phrase = phrase, Target = target, Time = Time.unscaledTime });
            if (kind == SignalKind.Chat)
            {
                SpheresHud.AddFeedTextLocal("<color=#" + ColorUtility.ToHtmlStringRGB(from.Color) + ">" + from.DisplayName + ":</color> " + Loc.T(Phrases[Mathf.Clamp(phrase, 0, Phrases.Length - 1)]));
                Sfx.Play("deal", 0.35f, 0f);
            }
            else if (VisibleToLocal(from)) Sfx.Play("zap", 0.3f, 0f);
        }

        /// <summary>Pings are shown to the sender's teammates (any local viewer on that team).</summary>
        public static bool VisibleTo(Signal s, Player viewer) =>
            s.Kind == SignalKind.Chat || viewer == null || s.From == viewer || s.From.Team == viewer.Team;

        static bool VisibleToLocal(Player from)
        {
            var gm = GameManager.Instance;
            if (gm == null) return true;
            return gm.Cameras.Any(c => c != null && c.Target != null && (c.Target == from || c.Target.Team == from.Team));
        }

        // ------------------------------------------------------------------ input

        /// <summary>Called by HumanInput each tick with this frame's requests.</summary>
        public static void FromInput(Player p, int phrase, bool ping, Vector3 aimPoint, Vector3 eye)
        {
            var gm = GameManager.Instance;
            if (p == null || gm == null || !gm.InMatch || gm.Paused) return;
            if (phrase >= 0) Send(p, SignalKind.Chat, p.Top, phrase);
            if (!ping) return;
            // Enemy under the crosshair? (closest to the aim ray within 1.6 m)
            Player enemy = null;
            float best = 1.6f;
            Vector3 dir = (aimPoint - eye).normalized;
            float maxDist = (aimPoint - eye).magnitude + 1f;
            foreach (var o in gm.Players)
            {
                if (o == null || !o.IsAlive || o.Team == p.Team) continue;
                Vector3 to = o.Center - eye;
                float along = Vector3.Dot(to, dir);
                if (along < 0f || along > maxDist) continue;
                float off = (to - dir * along).magnitude;
                if (off < best) { best = off; enemy = o; }
            }
            if (enemy != null) Send(p, SignalKind.PingEnemy, enemy.Center, 0, enemy);
            else Send(p, SignalKind.PingSpot, aimPoint, 0);
        }

        // ------------------------------------------------------------------ bots

        static void OnDied(Player victim, DamageInfo info)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.NetClientMode) return;
            var killer = info != null && info.Source != null && info.Source != victim ? info.Source : null;
            if (killer != null && killer.IsBot && Random.value < 0.12f) Receive(killer, SignalKind.Chat, killer.Top, 0, null);
            else if (victim.IsBot && Random.value < 0.06f) Receive(victim, SignalKind.Chat, victim.Top, Random.value < 0.5f ? 5 : 2, null);
        }

        static void OnMatchEnded(int winner)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.NetClientMode) return;
            foreach (var p in gm.Players.Where(p => p.IsBot))
                if (Random.value < 0.5f) Receive(p, SignalKind.Chat, p.Top, 6, null);
        }

        // ------------------------------------------------------------------ drawing

        /// <summary>Pings and speech bubbles for one viewport (called from the HUD's world overlay pass).</summary>
        public static void Draw(Player viewer, System.Func<Vector3, (Vector2 pos, bool visible, bool behind)> project, Rect area)
        {
            float now = Time.unscaledTime;
            Active.RemoveAll(s => s.From == null || now - s.Time > (s.Kind == SignalKind.Chat ? BubbleLife : s.Kind == SignalKind.PingEnemy ? EnemyPingLife : PingLife));
            foreach (var s in Active)
            {
                if (!VisibleTo(s, viewer)) continue;
                float age = now - s.Time;
                if (s.Kind == SignalKind.Chat)
                {
                    if (s.From == viewer || !s.From.IsAlive) continue;
                    var (pos, vis, _) = project(s.From.Top + Vector3.up * 1.2f);
                    if (!vis) continue;
                    string text = Loc.T(Phrases[Mathf.Clamp(s.Phrase, 0, Phrases.Length - 1)]);
                    float w = Mathf.Max(60f, text.Length * 8.5f + 18f);
                    float a = Mathf.Clamp01((BubbleLife - age) * 3f);
                    var r = new Rect(pos.x - w * 0.5f, pos.y - 46f - Mathf.Clamp01(age * 6f) * 6f, w, 24f);
                    UIKit.Fill(r, new Color(1f, 1f, 1f, 0.9f * a));
                    UIKit.Fill(new Rect(pos.x - 4f, r.yMax, 8f, 5f), new Color(1f, 1f, 1f, 0.9f * a));
                    UIKit.Text(r, text, UIKit.SmallCenter, new Color(0.08f, 0.09f, 0.12f, a));
                    continue;
                }
                Vector3 world = s.Kind == SignalKind.PingEnemy && s.Target != null && s.Target.IsAlive ? s.Target.Top + Vector3.up * 0.9f : s.Position;
                var (p, visible, behind) = project(world);
                // Clamp off-screen pings to the viewport edge so they still point the way.
                var clamped = new Vector2(Mathf.Clamp(p.x, area.x + 24f, area.xMax - 24f), Mathf.Clamp(p.y, area.y + 40f, area.yMax - 40f));
                if (behind) clamped = new Vector2(area.xMax - (p.x - area.x), area.yMax - 40f);
                bool edge = !visible || behind;
                var c = s.Kind == SignalKind.PingEnemy ? new Color(1f, 0.3f, 0.25f) : Color.Lerp(s.From.Color, Color.white, 0.25f);
                float life = s.Kind == SignalKind.PingEnemy ? EnemyPingLife : PingLife;
                float alpha = Mathf.Clamp01((life - age) * 2f);
                float pop = 1f + 0.6f * Mathf.Clamp01(1f - age * 4f);
                float size = 16f * pop;
                var center = edge ? clamped : p;
                // Diamond made of two rotated squares' worth of fills: a simple marker that reads at any size.
                var m = GUI.matrix;
                GUIUtility.RotateAroundPivot(45f, center);
                UIKit.Fill(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), new Color(0f, 0f, 0f, 0.55f * alpha));
                UIKit.Fill(new Rect(center.x - size * 0.35f, center.y - size * 0.35f, size * 0.7f, size * 0.7f), new Color(c.r, c.g, c.b, alpha));
                GUI.matrix = m;
                if (!edge && age < 0.6f)
                {
                    float ring = 18f + age * 80f;
                    UIKit.Outline(new Rect(center.x - ring * 0.5f, center.y - ring * 0.5f, ring, ring), new Color(c.r, c.g, c.b, (0.6f - age) * alpha), 2f);
                }
                float dist = viewer != null ? Vector3.Distance(viewer.Center, world) : 0f;
                string label = (s.Kind == SignalKind.PingEnemy ? "Enemy" : s.From.DisplayName) + "  " + Mathf.RoundToInt(dist) + "m";
                UIKit.Shadowed(new Rect(center.x - 70f, center.y + 12f, 140f, 18f), label, UIKit.SmallCenter, new Color(c.r, c.g, c.b, alpha));
            }
        }
    }
}
