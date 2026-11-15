using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cubed
{
    /// <summary>In-match UI: per-viewport HUDs, card picking, scoreboard, banners, kill feed.</summary>
    public static class CubedHud
    {
        struct DamageNumber { public Vector3 Pos; public float Amount; public Color Color; public float Time; }
        struct FeedEntry { public string Text; public float Time; }

        static readonly List<DamageNumber> damageNumbers = new List<DamageNumber>();
        static readonly List<FeedEntry> feed = new List<FeedEntry>();
        static Vector2 lastMouse;

        public static void AddDamageNumber(Vector3 pos, float amount, Color color)
        {
            if (amount < 0.5f) return;
            if (!Options.Current.damageNumbers) { if (Net.NetSession.Mirroring) Net.NetSession.Instance.MirrorDamageNumber(pos, amount, color); return; }
            if (Net.NetSession.Mirroring) Net.NetSession.Instance.MirrorDamageNumber(pos, amount, color);
            damageNumbers.Add(new DamageNumber { Pos = pos + Random.insideUnitSphere * 0.3f, Amount = amount, Color = color, Time = Time.unscaledTime });
            if (damageNumbers.Count > 60) damageNumbers.RemoveAt(0);
        }

        public static void AddKill(Player killer, Player victim, DamageInfo info)
        {
            string v = Colored(victim.DisplayName, victim.Color);
            string text;
            if (killer != null && killer != victim) text = Colored(killer.DisplayName, killer.Color) + "  >  " + v;
            else
            {
                string how = info == null ? "died" :
                    info.Type == DamageType.Fall ? "fell" :
                    info.Type == DamageType.Border ? "hit the border" :
                    info.Type == DamageType.Hazard ? "touched lava" : "died";
                text = v + " " + how;
            }
            AddFeedText(text);
        }

        /// <summary>Add a line to the kill feed (also used for join/leave messages).</summary>
        public static void AddFeedText(string text)
        {
            if (Net.NetSession.Mirroring) Net.NetSession.Instance.MirrorFeed(text);
            feed.Add(new FeedEntry { Text = text, Time = Time.unscaledTime });
            if (feed.Count > 6) feed.RemoveAt(0);
        }

        public static void Clear()
        {
            damageNumbers.Clear();
            feed.Clear();
        }

        static string Colored(string s, Color c) => "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + s + "</color>";

        // ------------------------------------------------------------------ draw

        public static void Draw(GameManager gm)
        {
            if (gm.Cameras.Count == 0)
            {
                var full = new Rect(0f, 0f, UIKit.Size.x, UIKit.Size.y);
                DrawWorldOverlays(gm, gm.OverviewCamera, full, null);
                if (gm.State == GameState.CardPick) DrawSpectatorPicks(gm, full);
            }
            foreach (var cam in gm.Cameras)
            {
                if (cam == null) continue;
                var v = UIKit.FromPixels(cam.GuiRect);
                GUI.BeginGroup(v);
                var local = new Rect(0f, 0f, v.width, v.height);
                DrawWorldOverlays(gm, cam.Cam, local, cam.Target, v.position);
                if (gm.State == GameState.CardPick) DrawPickView(gm, cam.Target, local);
                else if (gm.State != GameState.MatchEnd) DrawPlayerHud(gm, cam, local);
                GUI.EndGroup();
            }
            if (gm.Cameras.Count > 1) DrawSplitLines(gm);

            DrawScoreboard(gm);
            DrawModeInfo(gm);
            DrawFeed();
            DrawBanners(gm);
            if (gm.State == GameState.MatchEnd) DrawMatchEnd(gm);
        }

        static void DrawSplitLines(GameManager gm)
        {
            var c = new Color(0f, 0f, 0f, 0.9f);
            int n = gm.Cameras.Count;
            int cols = n == 2 ? 2 : n <= 4 ? 2 : n <= 6 ? 3 : 4;
            for (int i = 1; i < cols; i++) UIKit.Fill(new Rect(UIKit.Size.x * i / cols - 1.5f, 0f, 3f, UIKit.Size.y), c);
            if (n > 2) UIKit.Fill(new Rect(0f, UIKit.Size.y * 0.5f - 1.5f, UIKit.Size.x, 3f), c);
        }

        static void DrawWorldOverlays(GameManager gm, Camera cam, Rect area, Player self, Vector2 groupOffset = default)
        {
            if (cam == null || !cam.enabled) return;
            float scale = UIKit.Scale;
            Vector2 ToLocal(Vector3 world, out bool visible)
            {
                var sp = cam.WorldToScreenPoint(world);
                visible = sp.z > 0.1f;
                var gui = new Vector2(sp.x / scale, (Screen.height - sp.y) / scale) - groupOffset;
                if (!area.Contains(gui)) visible = false;
                return gui;
            }

            foreach (var p in gm.Players)
            {
                if (p == null || !p.IsAlive) continue;
                if (p == self) continue;
                var pos = ToLocal(p.Top + Vector3.up * 0.45f, out bool vis);
                if (!vis) continue;
                float w = 70f;
                var bar = new Rect(pos.x - w * 0.5f, pos.y, w, 8f);
                UIKit.Bar(bar, p.Health / Mathf.Max(1f, p.MaxHealth), p.Color, new Color(0f, 0f, 0f, 0.6f));
                if (p.LivesRemaining > 0)
                    for (int i = 0; i < p.LivesRemaining; i++) UIKit.Fill(new Rect(bar.x + i * 9f, bar.yMax + 2f, 7f, 4f), new Color(1f, 0.9f, 0.5f));
                UIKit.Shadowed(new Rect(pos.x - 80f, pos.y - 20f, 160f, 18f), p.DisplayName, UIKit.SmallCenter, Color.Lerp(p.Color, Color.white, 0.4f));
                if (p.IsBlocking) UIKit.Outline(new Rect(bar.x - 2f, bar.y - 2f, bar.width + 4f, bar.height + 4f), Color.white, 1f);
            }

            float now = Time.unscaledTime;
            var style = UIKit.HeaderCenter;
            for (int i = damageNumbers.Count - 1; i >= 0; i--)
            {
                var d = damageNumbers[i];
                float age = now - d.Time;
                if (age > 0.9f)
                {
                    damageNumbers.RemoveAt(i);
                    continue;
                }
                var pos = ToLocal(d.Pos + Vector3.up * age * 1.2f, out bool vis);
                if (!vis) continue;
                var c = Color.Lerp(d.Color, Color.white, 0.5f);
                c.a = 1f - age / 0.9f;
                UIKit.Shadowed(new Rect(pos.x - 50f, pos.y - 14f, 100f, 28f), Mathf.RoundToInt(d.Amount).ToString(), style, c);
            }
        }

        static void DrawPlayerHud(GameManager gm, PlayerCamera cam, Rect v)
        {
            var p = cam.Target;
            if (p == null) return;

            // Damage / low-health vignette.
            float vig = cam.DamageFlash * 0.75f;
            if (cam.LowHealth) vig = Mathf.Max(vig, 0.35f + 0.2f * Mathf.Sin(Time.unscaledTime * 7f));
            if (vig > 0.01f)
            {
                var old = GUI.color;
                GUI.color = new Color(0.9f, 0.05f, 0.05f, Mathf.Clamp01(vig));
                GUI.DrawTexture(v, Looks.Vignette, ScaleMode.StretchToFill);
                GUI.color = old;
            }

            // Crosshair.
            if (p.IsAlive)
            {
                Vector2 c = v.center;
                var col = new Color(1f, 1f, 1f, 0.85f);
                UIKit.Fill(new Rect(c.x - 1f, c.y - 1f, 2f, 2f), col);
                UIKit.Fill(new Rect(c.x - 11f, c.y - 1f, 6f, 2f), col);
                UIKit.Fill(new Rect(c.x + 5f, c.y - 1f, 6f, 2f), col);
                UIKit.Fill(new Rect(c.x - 1f, c.y - 11f, 2f, 6f), col);
                UIKit.Fill(new Rect(c.x - 1f, c.y + 5f, 2f, 6f), col);
                if (cam.HitMarker > 0f)
                {
                    // Diagonal hit marker built from small squares.
                    var hm = new Color(1f, 0.95f, 0.85f, Mathf.Clamp01(cam.HitMarker / 0.18f));
                    for (int k = 4; k <= 10; k += 2)
                    {
                        UIKit.Fill(new Rect(c.x + k - 1f, c.y + k - 1f, 2.5f, 2.5f), hm);
                        UIKit.Fill(new Rect(c.x - k - 1f, c.y + k - 1f, 2.5f, 2.5f), hm);
                        UIKit.Fill(new Rect(c.x + k - 1f, c.y - k - 1f, 2.5f, 2.5f), hm);
                        UIKit.Fill(new Rect(c.x - k - 1f, c.y - k - 1f, 2.5f, 2.5f), hm);
                    }
                }
                if (p.Weapon.Reloading)
                    UIKit.Bar(new Rect(c.x - 20f, c.y + 16f, 40f, 4f), p.Weapon.ReloadProgress, Color.white, new Color(0f, 0f, 0f, 0.5f));
            }
            else
            {
                string who = cam.Following != null && cam.Following != p ? "  -  spectating " + cam.Following.DisplayName : "";
                UIKit.Shadowed(new Rect(v.x, v.yMax - 120f, v.width, 30f), "ELIMINATED" + who, UIKit.HeaderCenter, UIKit.Bad);
            }

            float x = v.x + 20f, y = v.yMax - 58f;
            // Health.
            var hpRect = new Rect(x, y, Mathf.Min(300f, v.width * 0.4f), 24f);
            UIKit.Fill(new Rect(hpRect.x - 4f, hpRect.y - 26f, hpRect.width + 8f, 58f), new Color(0f, 0f, 0f, 0.45f));
            UIKit.Bar(hpRect, p.Health / Mathf.Max(1f, p.MaxHealth), p.Color, new Color(0.15f, 0.15f, 0.2f, 0.9f));
            if (p.Barrier > 0f)
                UIKit.Fill(new Rect(hpRect.x, hpRect.y, hpRect.width * Mathf.Clamp01(p.Barrier / Mathf.Max(1f, p.MaxHealth)), 6f), new Color(0.85f, 0.95f, 1f, 0.95f));
            UIKit.Shadowed(hpRect, Mathf.CeilToInt(Mathf.Max(0f, p.Health)) + " / " + Mathf.RoundToInt(p.MaxHealth) + (p.Barrier > 0f ? "  +" + Mathf.CeilToInt(p.Barrier) : ""), UIKit.LabelCenter, Color.white);
            UIKit.Shadowed(new Rect(hpRect.x, hpRect.y - 24f, 200f, 22f), p.DisplayName, UIKit.Label, Color.Lerp(p.Color, Color.white, 0.5f));
            for (int i = 0; i < p.LivesRemaining; i++) UIKit.Fill(new Rect(hpRect.xMax - 14f - i * 16f, hpRect.y - 18f, 12f, 12f), new Color(1f, 0.9f, 0.5f));

            // Ammo.
            float ax = hpRect.xMax + 18f;
            int max = p.Weapon.MaxAmmo;
            if (max <= 16)
            {
                for (int i = 0; i < max; i++)
                {
                    var r = new Rect(ax + i * 13f, y + 4f, 10f, 16f);
                    UIKit.Fill(r, i < p.Weapon.Ammo ? new Color(1f, 0.85f, 0.4f) : new Color(1f, 1f, 1f, 0.15f));
                }
            }
            else UIKit.Shadowed(new Rect(ax, y, 120f, 24f), p.Weapon.Ammo + " / " + max, UIKit.Label, new Color(1f, 0.85f, 0.4f));
            if (p.Weapon.Reloading)
                UIKit.Bar(new Rect(ax, y - 10f, Mathf.Min(max, 16) * 13f, 5f), p.Weapon.ReloadProgress, Color.white, new Color(1f, 1f, 1f, 0.15f));

            // Block.
            float total = Mathf.Max(0.01f, p.GetStat(Stat.BlockCooldown));
            float ready = 1f - p.BlockCooldownTimer / total;
            var br = new Rect(v.xMax - 190f, y, 170f, 24f);
            UIKit.Fill(new Rect(br.x - 4f, br.y - 4f, br.width + 8f, br.height + 8f), new Color(0f, 0f, 0f, 0.45f));
            UIKit.Bar(br, ready, p.IsBlocking ? Color.white : ready >= 1f ? UIKit.Accent : new Color(0.35f, 0.4f, 0.5f), new Color(0.15f, 0.15f, 0.2f, 0.9f));
            UIKit.Shadowed(br, p.IsBlocking ? "BLOCKING" : ready >= 1f ? "BLOCK READY" : "BLOCK", UIKit.LabelCenter, Color.white);

            // Statuses.
            string status = "";
            if (p.IsStunned) status += "<color=#ffe36a>STUNNED</color>  ";
            if (p.IsSlowed) status += "<color=#9fe3ff>SLOWED</color>  ";
            if (p.IsPoisoned) status += "<color=#8cff6a>POISONED</color>  ";
            if (p.InvulnerableTimer > 0f) status += "<color=#ffe680>REVIVED</color>  ";
            if (status.Length > 0) UIKit.Shadowed(new Rect(br.x - 40f, br.y - 30f, 210f, 22f), status, UIKit.Small, Color.white);

            // Card list.
            if (p.Cards.Count > 0)
            {
                var groups = p.Cards.GroupBy(c => c).ToList();
                float cy = v.y + 90f;
                int shown = 0;
                foreach (var g in groups)
                {
                    if (cy > v.yMax - 110f) break;
                    var c = g.Key;
                    UIKit.Fill(new Rect(v.x + 12f, cy + 3f, 10f, 10f), c.Color);
                    string label = c.Title + (g.Count() > 1 ? " x" + g.Count() : "");
                    UIKit.Shadowed(new Rect(v.x + 28f, cy, 220f, 16f), label, UIKit.Small, new Color(1f, 1f, 1f, 0.75f));
                    cy += 17f;
                    shown++;
                }
            }
        }

        // ------------------------------------------------------------------ picking

        static void DrawPickView(GameManager gm, Player p, Rect v)
        {
            UIKit.Fill(v, new Color(0.02f, 0.03f, 0.06f, 0.6f));
            var st = gm.Picks.FirstOrDefault(s => s.Player == p);
            if (st == null || st.Done)
            {
                string msg = st != null && st.LastPicked != null ? "You picked " + string.Join(", ", st.PickedThisPhase.Select(c => c.Title)) : "Other players are choosing cards";
                UIKit.Shadowed(new Rect(v.x, v.center.y - 80f, v.width, 34f), msg, UIKit.HeaderCenter, Color.white);
                DrawPickerList(gm, new Rect(v.center.x - 200f, v.center.y - 30f, 400f, 200f));
                return;
            }

            string head = (gm.StartingPickPhase ? "Starting pick" : "Choose a card") + "   " + st.PickNumber + " / " + st.PicksTotal;
            UIKit.Shadowed(new Rect(v.x, v.y + 50f, v.width, 34f), head, UIKit.HeaderCenter, Color.Lerp(p.Color, Color.white, 0.4f));
            if (gm.Settings.pickTimer > 0f)
            {
                float t = st.TimeLeft / gm.Settings.pickTimer;
                var tr = new Rect(v.center.x - 160f, v.y + 90f, 320f, 8f);
                UIKit.Bar(tr, t, st.TimeLeft < 4f ? UIKit.Bad : UIKit.Accent, new Color(0f, 0f, 0f, 0.5f));
                UIKit.Shadowed(new Rect(tr.xMax + 8f, tr.y - 8f, 60f, 24f), Mathf.CeilToInt(st.TimeLeft) + "s", UIKit.Small, Color.white);
            }

            int n = st.Hand.Count;
            if (n == 0) return;
            // One row for up to 6 cards, two rows beyond that.
            int rows = n > 6 ? 2 : 1;
            int perRow = Mathf.CeilToInt(n / (float)rows);
            float gap = 14f;
            float avail = v.width - 40f;
            float areaH = v.height - 200f;
            float cardW = Mathf.Min(220f, (avail - gap * (perRow - 1)) / perRow);
            float cardH = Mathf.Min(cardW * 1.5f, (areaH - gap * (rows - 1)) / rows);
            cardW = Mathf.Min(cardW, cardH / 1.3f);
            float totalH = cardH * rows + gap * (rows - 1);
            float y0 = v.y + 120f + Mathf.Max(0f, (areaH - totalH) * 0.4f);

            bool mouseUser = p.Input is HumanInput hi && hi.IsKeyboard;
            var e = Event.current;
            float now = Time.unscaledTime;
            for (int i = 0; i < n; i++)
            {
                int row = i / perRow, col = i % perRow;
                int inRow = row == rows - 1 ? n - perRow * (rows - 1) : perRow;
                float rowW = cardW * inRow + gap * (inRow - 1);
                var r = new Rect(v.center.x - rowW * 0.5f + col * (cardW + gap), y0 + row * (cardH + gap), cardW, cardH);
                bool hl = i == st.Highlight;

                // Deal animation: cards flip in one after another.
                float t = Mathf.Clamp01((now - st.DealtTime - i * 0.05f) / 0.3f);
                float ease = 1f - (1f - t) * (1f - t) * (1f - t);
                r.y += (1f - ease) * 50f;
                if (hl) r.y -= 14f + Mathf.Sin(now * 5f) * 2f;

                if (mouseUser && t >= 1f)
                {
                    if (e.type == EventType.Repaint && r.Contains(e.mousePosition) && st.Highlight != i && (e.mousePosition - lastMouse).sqrMagnitude > 1f)
                    {
                        st.Highlight = i;
                        Sfx.Play("hover", 0.5f, 0.05f);
                    }
                    if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                    {
                        gm.Pick(st, i);
                        return;
                    }
                }
                if (ease < 1f)
                {
                    // "Flip" by narrowing the card around its center while it slides in.
                    float w = r.width * Mathf.Max(0.05f, ease);
                    r = new Rect(r.center.x - w * 0.5f, r.y, w, r.height);
                    UIKit.Fill(r, new Color(0.1f, 0.11f, 0.16f, t));
                    UIKit.Outline(r, CardDefinition.RarityColor(st.Hand[i].Rarity), 2f);
                    continue;
                }
                UIKit.DrawCard(r, st.Hand[i], hl);
            }
            if (mouseUser && e.type == EventType.Repaint) lastMouse = e.mousePosition;

            string hint = mouseUser ? "Click a card  -  or A/D + Space" : "Stick / LB RB to choose  -  A to pick";
            UIKit.Shadowed(new Rect(v.x, Mathf.Min(v.yMax - 40f, y0 + totalH + 20f), v.width, 24f), hint, UIKit.SmallCenter, UIKit.Muted);
        }

        static void DrawPickerList(GameManager gm, Rect r)
        {
            float y = r.y;
            foreach (var st in gm.Picks)
            {
                string state = st.Done ? "picked " + string.Join(", ", st.PickedThisPhase.Select(c => c.Title)) : "choosing... (" + st.PickNumber + "/" + st.PicksTotal + ")";
                UIKit.Shadowed(new Rect(r.x, y, r.width, 22f), Colored(st.Player.DisplayName, st.Player.Color) + "  " + state, UIKit.LabelCenter, Color.white);
                y += 24f;
            }
        }

        static void DrawSpectatorPicks(GameManager gm, Rect v)
        {
            UIKit.Fill(v, new Color(0.02f, 0.03f, 0.06f, 0.55f));
            UIKit.Shadowed(new Rect(v.x, v.y + 60f, v.width, 40f), "Card pick", UIKit.Title, Color.white);
            var current = gm.Picks.FirstOrDefault(s => !s.Done);
            if (current != null)
            {
                int n = current.Hand.Count;
                float cardW = Mathf.Min(190f, (v.width - 80f) / Mathf.Max(1, n) - 12f);
                float cardH = cardW * 1.45f;
                float x0 = v.center.x - (cardW * n + 12f * (n - 1)) * 0.5f;
                UIKit.Shadowed(new Rect(v.x, v.y + 110f, v.width, 30f), Colored(current.Player.DisplayName, current.Player.Color) + " is choosing", UIKit.HeaderCenter, Color.white);
                for (int i = 0; i < n; i++)
                {
                    var r = new Rect(x0 + i * (cardW + 12f), v.y + 160f - (i == current.Highlight ? 12f : 0f), cardW, cardH);
                    UIKit.DrawCard(r, current.Hand[i], i == current.Highlight);
                }
            }
            DrawPickerList(gm, new Rect(v.center.x - 300f, v.yMax - 40f - gm.Picks.Count * 24f, 600f, 300f));
        }

        // ------------------------------------------------------------------ global

        static void DrawScoreboard(GameManager gm)
        {
            var teams = gm.ActiveTeams;
            if (teams.Count == 0) return;
            float gap = 8f;
            float w = Mathf.Min(150f, (UIKit.Size.x - 360f) / teams.Count - gap);
            float total = teams.Count * w + (teams.Count - 1) * gap;
            float x = UIKit.Size.x * 0.5f - total * 0.5f;
            const float y = 10f;
            int target = gm.Settings.roundsToWin;
            foreach (int t in teams)
            {
                var r = new Rect(x, y, w, 44f);
                UIKit.Fill(r, new Color(0f, 0f, 0f, 0.55f));
                var col = Teams.GetColor(t);
                UIKit.Fill(new Rect(r.x, r.y, 5f, r.height), col);
                bool anyAlive = gm.Players.Any(p => p.Team == t && p.IsAlive);
                UIKit.Text(new Rect(r.x + 10f, r.y + 2f, w - 14f, 20f), Teams.GetName(t) + (anyAlive || !gm.InMatch ? "" : "  (out)"), UIKit.Small, col);
                int score = gm.TeamScores[t];
                if (target <= 10)
                {
                    float pw = Mathf.Min(14f, (w - 20f) / target - 3f);
                    for (int i = 0; i < target; i++)
                        UIKit.Fill(new Rect(r.x + 10f + i * (pw + 3f), r.y + 26f, pw, 10f), i < score ? col : new Color(1f, 1f, 1f, 0.15f));
                }
                else UIKit.Text(new Rect(r.x + 10f, r.y + 22f, w - 14f, 20f), score + " / " + target, UIKit.Small, Color.white);
                x += w + gap;
            }
            UIKit.Shadowed(new Rect(0f, 58f, UIKit.Size.x, 18f), "Round " + Mathf.Max(1, gm.RoundNumber) + "  -  first to " + target, UIKit.SmallCenter, UIKit.Muted);

            var map = gm.Map;
            if (gm.State == GameState.Fighting && map != null && map.Shrink > 0f && map.Shrink < 1f && Mathf.Repeat(Time.unscaledTime, 1f) < 0.65f)
                UIKit.Shadowed(new Rect(0f, 78f, UIKit.Size.x, 22f), "BORDERS CLOSING IN", UIKit.LabelCenter, UIKit.Bad);
        }

        static void DrawModeInfo(GameManager gm)
        {
            float y = 100f;
            var mode = gm.Settings.mode;
            if (mode == GameMode.Deathmatch && gm.InMatch)
            {
                string text = "First to " + gm.Settings.killsToWinRound + " kills:  " + string.Join("   ", gm.ActiveTeams.Select(t =>
                    "<color=#" + ColorUtility.ToHtmlStringRGB(Teams.GetColor(t)) + ">" + gm.RoundKills[t] + "</color>"));
                UIKit.Shadowed(new Rect(0f, y, UIKit.Size.x, 24f), text, UIKit.LabelCenter, Color.white);
            }
            else if (mode == GameMode.KingOfTheHill && gm.InMatch)
            {
                float w = 300f, x = UIKit.Size.x * 0.5f - w * 0.5f;
                foreach (int t in gm.ActiveTeams)
                {
                    UIKit.Bar(new Rect(x, y, w, 7f), gm.HillProgress[t] / Mathf.Max(1f, gm.Settings.hillSeconds), Teams.GetColor(t), new Color(0f, 0f, 0f, 0.5f));
                    y += 10f;
                }
                string holder = gm.HillHolder >= 0 ? Teams.GetName(gm.HillHolder) + " holds the hill" : gm.HillHolder == -2 ? "Hill contested!" : "Hill is open";
                UIKit.Shadowed(new Rect(0f, y + 2f, UIKit.Size.x, 22f), holder, UIKit.SmallCenter, gm.HillHolder >= 0 ? Teams.GetColor(gm.HillHolder) : Color.white);
            }
            else if (mode == GameMode.CardRush && gm.InMatch)
                UIKit.Shadowed(new Rect(0f, y, UIKit.Size.x, 22f), "Card Rush: every kill draws a card", UIKit.SmallCenter, UIKit.Muted);

            // Respawn countdown for local players.
            foreach (var cam in gm.Cameras)
            {
                if (cam == null || cam.Target == null || cam.Target.IsAlive) continue;
                float t = gm.RespawnIn(cam.Target);
                if (t < 0f) continue;
                var v = UIKit.FromPixels(cam.GuiRect);
                UIKit.Shadowed(new Rect(v.x, v.yMax - 90f, v.width, 30f), "Respawning in " + Mathf.CeilToInt(t), UIKit.HeaderCenter, Color.white);
            }

            var tut = gm.Tutorial;
            if (gm.IsTraining && tut != null)
            {
                var r = new Rect(16f, 300f, 380f, 60f + tut.Steps.Count * 26f);
                UIKit.Fill(r, new Color(0.05f, 0.06f, 0.1f, 0.85f));
                UIKit.Text(new Rect(r.x + 12f, r.y + 8f, r.width, 28f), tut.Complete ? "Training complete!" : "Training", UIKit.Header, UIKit.Accent);
                float ty = r.y + 42f;
                for (int i = 0; i < tut.Steps.Count; i++)
                {
                    var st = tut.Steps[i];
                    string mark = i < tut.Index ? "<color=#7dff8a>[x]</color> " : i == tut.Index ? "<color=#ffd75e>[>]</color> " : "[ ] ";
                    UIKit.Text(new Rect(r.x + 12f, ty, r.width - 20f, 24f), mark + st.Title, UIKit.Label, i == tut.Index ? Color.white : UIKit.Muted);
                    ty += 26f;
                }
                if (!tut.Complete)
                {
                    var step = tut.Steps[tut.Index];
                    float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
                    UIKit.Shadowed(new Rect(0f, UIKit.Size.y * 0.72f, UIKit.Size.x, 34f), step.Title + ":  " + step.Hint, UIKit.HeaderCenter, new Color(1f, 1f, 1f, pulse));
                }
                else UIKit.Shadowed(new Rect(0f, UIKit.Size.y * 0.72f, UIKit.Size.x, 30f), "Press C (or Select) for more cards. Esc to leave training.", UIKit.LabelCenter, UIKit.Muted);
            }
        }

        static void DrawFeed()
        {
            float now = Time.unscaledTime;
            float y = 12f;
            for (int i = feed.Count - 1; i >= 0; i--)
            {
                float age = now - feed[i].Time;
                if (age > 6f) continue;
                float a = Mathf.Clamp01(6f - age);
                float slide = Mathf.Pow(1f - Mathf.Clamp01(age * 5f), 3f) * 160f;
                var r = new Rect(UIKit.Size.x - 330f + slide, y, 318f, 24f);
                UIKit.Fill(r, new Color(0f, 0f, 0f, 0.45f * a));
                UIKit.Text(new Rect(r.x + 8f, r.y, r.width - 16f, r.height), feed[i].Text, UIKit.Small, new Color(1f, 1f, 1f, a));
                y += 28f;
            }
        }

        static void DrawBanners(GameManager gm)
        {
            var full = new Rect(0f, UIKit.Size.y * 0.32f, UIKit.Size.x, 110f);
            if (gm.State == GameState.RoundIntro)
            {
                int n = Mathf.CeilToInt(GameManager.IntroDuration - gm.StateTime);
                float frac = Mathf.Repeat(GameManager.IntroDuration - gm.StateTime, 1f);
                var style = new GUIStyle(UIKit.Huge) { fontSize = Mathf.RoundToInt(84 + frac * 30f) };
                UIKit.Shadowed(full, n.ToString(), style, Color.white);
                if (gm.RoundNumber > 0)
                    UIKit.Shadowed(new Rect(0f, full.yMax, UIKit.Size.x, 30f), "Round " + gm.RoundNumber, UIKit.HeaderCenter, UIKit.Muted);
            }
            else if (gm.State == GameState.Fighting && gm.StateTime < 0.7f)
            {
                var c = Color.white;
                c.a = 1f - gm.StateTime / 0.7f;
                UIKit.Shadowed(full, "FIGHT!", UIKit.Huge, c);
            }
            else if (gm.State == GameState.RoundEnd)
            {
                int w = gm.LastRoundWinner;
                string text = w >= 0 ? Teams.GetName(w).ToUpperInvariant() + " WINS THE ROUND" : "DRAW";
                UIKit.Fill(new Rect(0f, full.y + 10f, UIKit.Size.x, 90f), new Color(0f, 0f, 0f, 0.5f));
                var style = new GUIStyle(UIKit.Title) { fontSize = 54 };
                UIKit.Shadowed(full, text, style, w >= 0 ? Teams.GetColor(w) : Color.white);
            }
        }

        static void DrawMatchEnd(GameManager gm)
        {
            var size = UIKit.Size;
            UIKit.Fill(new Rect(0f, 0f, size.x, size.y), new Color(0f, 0f, 0f, 0.5f));
            float w = 760f, h = 160f + gm.Players.Count * 30f + 90f;
            var r = new Rect(size.x * 0.5f - w * 0.5f, size.y * 0.5f - h * 0.5f, w, h);
            UIKit.Fill(r, UIKit.Panel);
            int win = gm.MatchWinner;
            UIKit.Outline(r, Teams.GetColor(win), 3f);
            UIKit.Shadowed(new Rect(r.x, r.y + 20f, w, 50f), Teams.GetName(win).ToUpperInvariant() + " TEAM WINS!", UIKit.Title, Teams.GetColor(win));

            float y = r.y + 90f;
            float[] cols = { 20f, 300f, 390f, 480f, 600f };
            string[] heads = { "Player", "Kills", "Deaths", "Damage", "Cards" };
            for (int i = 0; i < cols.Length; i++) UIKit.Text(new Rect(r.x + cols[i], y, 150f, 24f), heads[i], UIKit.Small, UIKit.Muted);
            y += 28f;
            foreach (var p in gm.Players.OrderByDescending(p => p.Team == win).ThenByDescending(p => p.Kills))
            {
                UIKit.Text(new Rect(r.x + cols[0], y, 270f, 26f), p.DisplayName + (p.IsBot ? " (bot)" : ""), UIKit.Label, p.Color);
                UIKit.Text(new Rect(r.x + cols[1], y, 80f, 26f), p.Kills.ToString(), UIKit.Label, Color.white);
                UIKit.Text(new Rect(r.x + cols[2], y, 80f, 26f), p.Deaths.ToString(), UIKit.Label, Color.white);
                UIKit.Text(new Rect(r.x + cols[3], y, 110f, 26f), Mathf.RoundToInt(p.DamageDealt).ToString(), UIKit.Label, Color.white);
                UIKit.Text(new Rect(r.x + cols[4], y, 140f, 26f), p.Cards.Count.ToString(), UIKit.Label, Color.white);
                y += 30f;
            }

            float by = r.yMax - 60f;
            if (gm.NetClientMode)
            {
                UIKit.Text(new Rect(r.x + 20f, by, w - 280f, 42f), "Waiting for the host...", UIKit.Label, UIKit.Muted);
                if (UIKit.Btn(new Rect(r.xMax - 240f, by, 220f, 42f), "Leave")) { gm.Net.Leave(); gm.ReturnToMenu(); }
                return;
            }
            float bw = gm.TestingFromEditor ? 220f : 300f;
            float bx = r.x + 20f;
            if (UIKit.Btn(new Rect(bx, by, bw, 42f), "Rematch")) gm.Rematch();
            bx += bw + 20f;
            if (gm.TestingFromEditor)
            {
                if (UIKit.Btn(new Rect(bx, by, bw, 42f), "Back to Editor")) gm.BackToEditor();
                bx += bw + 20f;
            }
            if (UIKit.Btn(new Rect(r.xMax - 20f - bw, by, bw, 42f), gm.Net.IsHosting ? "Back to Lobby" : "Main Menu")) gm.ReturnToMenu();
        }
    }
}
