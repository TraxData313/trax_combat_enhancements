using System;
using System.Collections.Generic;
using System.Diagnostics;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 74 - THE GREEN BLOCK STAYS A BLOCK INTO CONTACT (Toni's battle 2026-10-20 20:30; AI_NOTES "Step 74", DESIGN decision 68). It rides
    /// courage's looks (every AI man on foot in a formation, about every 0.5 s, ON and OFF - the OFF looks only measure): the unit table
    /// (<see cref="BlockUnits"/>) learns each formation's block frame - its centre line (the median of its men across its front), its depth, its
    /// speed - and step 73's cap; a man of a held block (D above 1) standing beyond its half-width + the slack, ahead of its front or behind its rear
    /// is walked back by a sixth input wish (<see cref="AiInputState.Block"/> - the LAST: a step back, the wall's pull, courage's walk back and the
    /// fear flock go first), facing the enemy, his place leading the advancing block; a man with an enemy in reach fights. THE DIAGNOSTIC
    /// (<see cref="BlockShapeStats"/>) samples every counted formation's order / behaviour / arrangement / spread / men out of their slots each frame
    /// and logs the changes as they happen. The master switch FIRST (then step 73's, then the hold's own) - off = every pull let go at once.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        /// <summary>The order / behaviour change lines logged per formation and per battle (the rest counted).</summary>
        private const int BlockChangeLinesPerUnit = 16, BlockChangeLinesPerBattle = 120;

        private readonly BlockUnits _blockUnits = new BlockUnits();
        private readonly BlockHoldStats _blockStats = new BlockHoldStats();
        private readonly BlockShapeStats _blockShape = new BlockShapeStats();
        private readonly List<TrackedAgent> _blocking = new List<TrackedAgent>(128);
        private readonly int[] _blockPublished = new int[FearMath.UnitCount];
        private readonly TrackedAgent?[] _blockRep = new TrackedAgent?[FearMath.UnitCount];
        private BlockHoldRules _blockRules = new BlockHoldRules(false, false, false, false);
        private string _seenBlockText = string.Empty;
        private bool _firstBlockPullLogged;
        private bool _blockClosed;

        /// <summary>The engine side of the block hold (the offline smoke swaps in a stand-in).</summary>
        internal IBlockBody BlockBody { get; set; } = BlockBodyDefault.Current;

        internal BlockUnits BlockUnits => _blockUnits;

        internal BlockHoldStats BlockStats => _blockStats;

        internal BlockShapeStats BlockShape => _blockShape;

        /// <summary>Men walked back into their block now.</summary>
        internal int BlockingNow => _blocking.Count;

        private static string BlockText(TraxSettings s) => BlockHoldRules.From(s).Describe();

        private void StartBlock()
        {
            _blockRules = BlockHoldRules.From(TraxSettings.Shared);
            _seenBlockText = BlockText(TraxSettings.Shared);
            TraxLog.Info("lines", "mission start: " + _seenBlockText + " - read live; it rides courage's looks (every " + F1(CourageMath.PollSeconds)
                + " s); the [block] lines log each held formation's order / behaviour changes and its first contact; the [summary] \"block hold\" and \"block shape\" lines");
        }

        private void NoteBlockSettings(TraxSettings s)
        {
            string now = BlockText(s);
            if (now == _seenBlockText) return;
            _seenBlockText = now;
            TraxLog.Info("lines", "block hold settings changed mid-mission at " + Sec(SafeNow()) + " s: " + now + " - at every man's next look");
        }

        // ------------------------------------------------------------------ the tick (from TickCourage)

        /// <summary>The switches, every tick: off → every pull let go at once.</summary>
        private void TickBlockSwitch(double now)
        {
            var r = BlockHoldRules.From(TraxSettings.Shared);
            bool wasOn = _blockRules.On;
            _blockRules = r;
            if (!r.On && _blocking.Count > 0) EndAllBlock(BlockEnd.SwitchedOff, now, native: true, r.OffBecause);
            else if (wasOn && !r.On) TraxLog.Info("lines", "the block hold switched OFF at " + Sec(now) + " s (" + r.OffBecause + "): the game's own charge; the looks keep measuring");
        }

        /// <summary>The unit table published every 0.5 s (after the fear flock's - its band's direction is the block's frame), the diagnostic sampled,
        /// the fear band told the block's width.</summary>
        private void TickBlockUnits(double now)
        {
            _blockStats.Ticks++;
            if (!_blockUnits.Due(now)) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var r = _blockRules;
                int n = _blockUnits.Publish(now, in r, _fearUnits, _blockPublished);
                _blockStats.Publishes++;
                for (int i = 0; i < n; i++)
                {
                    int key = _blockPublished[i];
                    BlockFrameDone(key, now, in r);
                }
            }
            catch (Exception e)
            {
                _blockStats.Errors++;
                Failed("block.publish", e);
            }
            _blockStats.PublishCostMs += (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
        }

        private void BlockFrameDone(int key, double now, in BlockHoldRules r)
        {
            var t = _blockUnits;
            if (!t.Counted[key])
            {
                _fearUnits.ClearBlock(key);
                return;
            }
            double s = BlockHoldMath.Strength(in r, t.Depth[key]);
            // the fear flock's band sits inside the block (step 70's band, clamped to the cap's width around the block's centre line)
            if (r.On && s > 0) _fearUnits.SetBlock(key, now, t.CentreX[key], t.CentreY[key], t.CapWidth[key] / 2);
            else _fearUnits.ClearBlock(key);
            // the diagnostic: from the first time the formation is near the enemy (a charge, an advance within the near distance) on
            var u = _blockShape.Get(key);
            if (u == null)
            {
                if (!t.Near[key] && double.IsNaN(t.FirstContact[key])) return;
                var rep = _blockRep[key];
                string name = rep != null ? SafeBlockName(rep) : "unit " + key;
                u = _blockShape.For(key, name, t.PlayerSide[key], t.Ai[key]);
                if (u == null) return;
            }
            _blockShape.Add(u, t, key, now);
            if (!ReferenceEquals(u.LastOrder, t.Order[key]) || !ReferenceEquals(u.LastBehaviour, t.Behaviour[key]) || !ReferenceEquals(u.LastArrangement, t.Arrangement[key]))
            {
                bool first = u.LastOrder.Length == 0;
                string was = first ? "-" : u.LastOrder + " / " + u.LastBehaviour + " / " + u.LastArrangement;
                u.LastOrder = t.Order[key];
                u.LastBehaviour = t.Behaviour[key];
                u.LastArrangement = t.Arrangement[key];
                if (u.Changes < BlockChangeLinesPerUnit && _blockShape.ChangeLines < BlockChangeLinesPerBattle)
                {
                    u.Changes++;
                    _blockShape.ChangeLines++;
                    TraxLog.Info("block", (first ? "first look at " : "order / behaviour change of ") + u.Name + " (" + (u.PlayerSide ? "your side" : "the other side") + ", "
                        + (u.Ai ? "AI" : "yours") + ") at " + Sec(now) + " s" + FromContact(u, now) + ": " + was + " → " + u.LastOrder + " / " + u.LastBehaviour + " / "
                        + u.LastArrangement + " - " + ShapeNow(key, now));
                }
                else _blockShape.ChangeLinesCut++;
            }
            if (!u.ContactLogged && t.InContact[key])
            {
                u.ContactLogged = true;
                TraxLog.Info("block", "FIRST CONTACT of " + u.Name + " at " + Sec(now) + " s (a man within " + F0(BlockHoldMath.ContactMetres) + " m of an enemy): "
                    + u.LastOrder + " / " + u.LastBehaviour + " / " + u.LastArrangement + " - " + ShapeNow(key, now));
            }
        }

        private string SafeBlockName(TrackedAgent rep)
        {
            try
            {
                return BlockBody.Name(rep);
            }
            catch (Exception e)
            {
                Failed("block.name", e);
                return "(a formation)";
            }
        }

        private static string FromContact(BlockShapeStats.Unit u, double now) =>
            double.IsNaN(u.FirstContact) ? " (no contact yet)" : " (" + (now - u.FirstContact >= 0 ? "+" : "") + F1(now - u.FirstContact) + " s from its first contact)";

        /// <summary>The block's spread now, for a line.</summary>
        private string ShapeNow(int key, double now)
        {
            var t = _blockUnits;
            string outShare = double.IsNaN(t.OutOfSlot[key]) ? "-" : F0(100 * t.OutOfSlot[key]) + "%";
            return t.Men[key] + " men " + F1(t.LatP90[key] - t.LatP10[key]) + " m wide (10th-90th; all " + F1(t.LatMax[key] - t.LatMin[key]) + " m), "
                   + F1(t.AlongP90[key] - t.AlongP10[key]) + " m deep, the game's width " + (double.IsNaN(t.GameWidth[key]) ? "-" : F1(t.GameWidth[key])) + " m vs step 73's cap "
                   + F1(t.CapWidth[key]) + " m (D " + F2(t.Depth[key]) + ", " + t.CapFiles[key] + " files), out of their slots " + outShare
                   + ", moving " + F1(t.Speed[key]) + " m/s, " + (t.Near[key] ? "the enemy near" : "the enemy far");
        }

        /// <summary>Every tick while on: the walked-back men's vectors refreshed every <see cref="FearMath.SteerSeconds"/> each.</summary>
        private void SteerBlockers(double now)
        {
            if (_blocking.Count == 0) return;
            long start = Stopwatch.GetTimestamp();
            for (int i = _blocking.Count - 1; i >= 0; i--)
            {
                if (i >= _blocking.Count) continue;
                var st = _blocking[i];
                var bm = st.BlockHold;
                try
                {
                    if (st.Removed || !bm.Wish || !CourageBody.IsActive(st))
                    {
                        _blocking.RemoveAt(i);
                        continue;
                    }
                    if (now >= bm.NextSteer) SteerBlocker(st, bm, now);
                }
                catch (Exception e)
                {
                    _blockStats.Errors++;
                    Failed("block.steer", e);
                    SafeEndBlock(st, now);
                }
            }
            _blockStats.SteerCostMs += (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }

        // ------------------------------------------------------------------ the look (from FearLook - every courage path)

        private void BlockLook(TrackedAgent st, double now, in FearNear near, FearCourage courage)
        {
            long start = Stopwatch.GetTimestamp();
            try
            {
                BlockLookCore(st, now, in near, courage);
            }
            catch (Exception e)
            {
                _blockStats.Errors++;
                Failed("block.look", e);
                SafeEndBlock(st, now);
            }
            _blockStats.LookCostMs += (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }

        /// <summary>A man courage does not look at: a held wall man still counts in his formation's shape; any pull ends.</summary>
        private void BlockSkipped(TrackedAgent st, CourageSkip skip, double now)
        {
            try
            {
                if (st.BlockHold.Wish) EndBlockPull(st, BlockEnd.Excluded, now, native: true);
                if (skip != CourageSkip.WallHeld) return;
                if (!FearBody.Unit(st, out int key, out _, out _, out _, out double fx, out double fy)) return;
                BlockNoteUnit(st, key, now, fx, fy, double.NaN);
            }
            catch (Exception e)
            {
                _blockStats.Errors++;
                Failed("block.skipped", e);
            }
        }

        /// <summary>The unit table's reads for one man (the cap and the formation once a period; his slot distance; his place).</summary>
        private void BlockNoteUnit(TrackedAgent st, int key, double now, double fx, double fy, double enemy)
        {
            var t = _blockUnits;
            if (t.CapDue(key, now))
            {
                bool counted = BlockBody.Cap(st, _lines, out double depth, out int files, out double width, out int men, out bool side, out bool ai);
                t.NoteCap(key, now, counted, depth, files, width, men, side, ai);
                _blockRep[key] = st;
            }
            if (t.FormationDue(key, now))
            {
                BlockBody.Formation(st, out string order, out string behaviour, out string arrangement, out double gameWidth);
                t.NoteFormation(key, now, order, behaviour, arrangement, gameWidth);
            }
            CourageBody.Position(st, out double px, out double py);
            double slot = double.NaN;
            if (FearBody.Slot(st, out double sx, out double sy)) slot = Math.Sqrt((sx - px) * (sx - px) + (sy - py) * (sy - py));
            t.Note(key, px, py, fx, fy, slot, enemy);
        }

        private void BlockLookCore(TrackedAgent st, double now, in FearNear near, FearCourage courage)
        {
            var r = _blockRules;
            var bm = st.BlockHold;
            if (!FearBody.Unit(st, out int key, out var order, out _, out _, out double fx, out double fy))
            {
                if (bm.Wish) EndBlockPull(st, BlockEnd.Excluded, now, native: true);
                return;
            }
            double enemy = near.InContact ? near.EnemyDist : double.NaN;
            BlockNoteUnit(st, key, now, fx, fy, enemy);
            var t = _blockUnits;
            var side = _blockStats.Side(t.PlayerSide[key]);
            var kind = BlockLookKind.Inside;
            var pull = BlockPull.None;
            double toLateral = 0, toAlong = 0;
            bool striking = false;
            if (!t.Counted[key]) kind = BlockLookKind.NotCounted;
            else if (!t.Live(key, now)) kind = BlockLookKind.NoFrame;
            else
            {
                double s = BlockHoldMath.Strength(in r, t.Depth[key]);
                if (s <= 0) kind = BlockLookKind.Elite;
                else if (order == FearOrder.Retreat || order == FearOrder.Wall) kind = BlockLookKind.Retreat;
                else if (order != FearOrder.Charge && !t.Near[key]) kind = BlockLookKind.Far;
                else
                {
                    // the measurement (ON and OFF): his offset from the centre line against the half-width, before / after the first contact
                    t.Frame(key, now, near.Px, near.Py, out double lateral, out double along);
                    double half = t.CapWidth[key] / 2;
                    bool after = !double.IsNaN(t.FirstContact[key]) && now >= t.FirstContact[key];
                    side.AddOffset(r.On, after, lateral, half, BlockHoldMath.Allowance(in r, half, s));
                    double win = _fearUnits.WinOf(key, now);
                    if (!double.IsNaN(win) && 100 * win >= r.MopUpWinPercent) kind = BlockLookKind.MopUp;
                    else if (courage == FearCourage.Walking || (st.Fear.Wish && _fearRules.Enabled)) kind = BlockLookKind.Yielded;
                    else
                    {
                        double depth = BlockHoldMath.Depth(in r, Math.Max(t.CapMen[key], t.Men[key]), t.CapFiles[key]);
                        pull = BlockHoldMath.Decide(in r, s, half, depth, lateral, along, enemy, bm.Wish, out toLateral, out toAlong, out striking);
                        kind = pull != BlockPull.None ? BlockLookKind.Pulled : striking ? BlockLookKind.Striking : BlockLookKind.Inside;
                        if (pull != BlockPull.None) toAlong += BlockHoldMath.Lead(in r, t.Speed[key]);
                    }
                }
            }
            side.LookKinds[(int)kind]++;
            bm.Striking = striking;
            if (!r.On)
            {
                if (bm.Wish) EndBlockPull(st, BlockEnd.SwitchedOff, now, native: true);
                return;
            }
            if (kind != BlockLookKind.Pulled)
            {
                if (bm.Wish)
                    EndBlockPull(st, kind == BlockLookKind.Inside ? BlockEnd.BackInside : kind == BlockLookKind.Striking ? BlockEnd.Striking
                        : kind == BlockLookKind.Yielded ? BlockEnd.Yielded : BlockEnd.Released, now, native: true);
                return;
            }
            bm.Key = key;
            bm.ToLateral = toLateral;
            bm.ToAlong = toAlong;
            bm.Kind = pull;
            if (!bm.Wish)
            {
                if (!StartBlockInput(st, bm, now)) return;
                side.Pulls[(int)pull]++;
                if (!bm.EverPulled)
                {
                    bm.EverPulled = true;
                    side.PulledMen++;
                }
                if (!_firstBlockPullLogged)
                {
                    _firstBlockPullLogged = true;
                    TraxLog.Info("block", "first man walked back into his block at " + Sec(now) + " s: " + Name(st) + " of " + SafeBlockName(st) + " (D " + F2(t.Depth[key])
                        + ", the cap " + F1(t.CapWidth[key]) + " m, the hold " + F2(BlockHoldMath.Strength(in r, t.Depth[key])) + ") - " + BlockHoldMath.PullName(pull)
                        + "; his place " + F1(toLateral) + " m across the centre line, " + F1(toAlong) + " m along from the block's median; "
                        + (double.IsNaN(enemy) ? "no enemy within the scan" : "the nearest enemy " + F1(enemy) + " m") + " - " + ShapeNow(key, now));
                }
            }
            SteerBlocker(st, bm, now);
        }

        // ------------------------------------------------------------------ the lever

        private bool StartBlockInput(TrackedAgent st, BlockMind bm, double now)
        {
            EnsureInput(st);
            AiInputHook.HookResult hook;
            try
            {
                hook = CourageBody.Hook(st);
            }
            catch (Exception e)
            {
                _blockStats.Errors++;
                Failed("block.hook", e);
                return false;
            }
            NoteHooked(st, hook);
            AiInputHook.SetBlock(st, true, now);
            bm.Wish = true;
            bm.Moving = true;
            bm.Since = now;
            bm.NextSteer = now;
            bm.NextEdge = now + AiInputMath.EdgeCheckSeconds;
            _blocking.Add(st);
            return true;
        }

        /// <summary>His vector now: toward his place in the block (it moves with the block - extrapolated by its speed since the frame), the stick
        /// slower near it; 0, 0 within it; the ground ahead checked.</summary>
        private void SteerBlocker(TrackedAgent st, BlockMind bm, double now)
        {
            bm.NextSteer = now + FearMath.SteerSeconds;
            var s = st.Input;
            if (s == null) return;
            if (!_blockUnits.Live(bm.Key, now))
            {
                EndBlockPull(st, BlockEnd.Released, now, native: true);
                return;
            }
            var body = CourageBody;
            body.Position(st, out double px, out double py);
            _blockUnits.World(bm.Key, now, bm.ToLateral, bm.ToAlong, out double tx, out double ty);
            double dx = tx - px, dy = ty - py;
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (bm.Moving && d <= FearMath.ArriveMetres) bm.Moving = false;
            else if (!bm.Moving && d > FearMath.MoveAgainMetres) bm.Moving = true;
            if (bm.Moving && d > 1e-6)
            {
                dx /= d;
                dy /= d;
                if (now >= bm.NextEdge)
                {
                    bm.NextEdge = now + AiInputMath.EdgeCheckSeconds;
                    if (!body.GroundOk(st, dx, dy)) bm.Moving = false; // the ground ahead ends (the rock): he stands
                }
            }
            if (bm.Moving && body.ToLocal(st, dx, dy, out float lx, out float ly))
            {
                float k = (float)FearMath.Stick(d);
                s.BlockX = lx * k;
                s.BlockY = ly * k;
                _blockStats.SteerUpdates++;
            }
            else
            {
                s.BlockX = 0f;
                s.BlockY = 0f;
                _blockStats.StandUpdates++;
            }
        }

        private void EndBlockPull(TrackedAgent st, BlockEnd why, double now, bool native)
        {
            var bm = st.BlockHold;
            if (!bm.Wish) return;
            var side = _blockStats.Side(bm.Key >= 0 && bm.Key < FearMath.UnitCount && _blockUnits.PlayerSide[bm.Key]);
            if (!double.IsNaN(bm.Since) && now >= bm.Since) side.PulledSeconds += now - bm.Since;
            side.Ends[(int)why]++;
            bm.End();
            _blocking.Remove(st);
            if (st.Input != null && st.Input.Block)
            {
                AiInputHook.SetBlock(st, false, now);
                if (native && !st.Removed)
                {
                    try
                    {
                        CourageBody.Release(st);
                    }
                    catch (Exception e)
                    {
                        Failed("block.release", e);
                    }
                }
                DrainInputError(st);
            }
        }

        private void SafeEndBlock(TrackedAgent st, double now)
        {
            try
            {
                if (st.BlockHold.Wish) EndBlockPull(st, BlockEnd.Error, now, native: true);
            }
            catch
            {
                st.BlockHold.End();
                _blocking.Remove(st);
                if (st.Input != null) st.Input.Block = false;
            }
        }

        private void EndAllBlock(BlockEnd why, double now, bool native, string? because)
        {
            int ended = 0;
            for (int i = _blocking.Count - 1; i >= 0; i--)
            {
                if (i >= _blocking.Count) continue;
                var st = _blocking[i];
                try
                {
                    EndBlockPull(st, why, now, native);
                    ended++;
                }
                catch (Exception e)
                {
                    Failed("block.end-all", e);
                    SafeEndBlock(st, now);
                }
            }
            _blocking.Clear();
            if (why == BlockEnd.SwitchedOff && ended > 0)
                TraxLog.Info("lines", (because ?? "the block hold") + " switched OFF mid-mission at " + Sec(now) + " s: " + ended
                    + " men walked back into their blocks let go at once - the game's own charge; the looks keep measuring");
        }

        private void BlockLeftField(TrackedAgent st)
        {
            try
            {
                if (st.BlockHold.Wish) EndBlockPull(st, BlockEnd.LeftField, SafeNow(), native: false);
            }
            catch (Exception e)
            {
                Failed("block.left", e);
            }
        }

        private void CloseBlock(bool agentsAlive)
        {
            if (_blockClosed) return;
            _blockClosed = true;
            if (_blocking.Count > 0) EndAllBlock(BlockEnd.MissionEnd, SafeNow(), agentsAlive, null);
        }

        internal void WriteBlockSummary()
        {
            var lines = new List<string>(8);
            _blockStats.BandClamps = _fearUnits.BandBlockClamps;
            _blockStats.AddSummaryLines(lines, BlockHoldRules.From(TraxSettings.Shared));
            _blockShape.AddSummaryLines(lines);
            foreach (var line in lines) TraxLog.Info("summary", line);
        }
    }
}
