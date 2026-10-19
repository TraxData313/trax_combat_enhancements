using System;
using System.Collections.Generic;
using System.Diagnostics;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 67 - THE FEAR FLOCK, courage's evolution (Anton, 2026-10-19: "as troops lose their morale and freshness, they flock less to their
    /// formation position and more to the centre of their unit's mass, but pushed back away from the enemy mass ... stepping behind their
    /// comrades"; AI_NOTES "Step 67", DESIGN decision 60). It rides courage's look (every AI man on foot in a formation, ~every 0.5 s,
    /// staggered, courage's scan): his FEAR (low morale + lost freshness, × (1 − discipline by level), + nobody behind him, mixed with his
    /// unit's) → the PULL → his PLACE between his slot and his unit's SHELTER (the centre pushed back from the enemy near the unit, its
    /// backward drift capped by order), behind a comrade when nobody covers him, held while a FRESH comrade in front anchors him, sprinting
    /// back when far or alone. He is walked there through courage's own lever - his input (a <c>Flock</c> wish after the walk back's: he
    /// faces whatever his AI faces and still strikes what reaches him; 0, 0 = he stands). WHO STEERS: a step back's backpedal, then the wall's
    /// pull, then COURAGE'S WALK BACK, then the flock; a fever press or a surge ends a spell (the charge goes in). The unit table and the
    /// looks run ON and OFF - the [summary] "fear flock" lines compare. No allocation per tick.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly FearStats _fearStats = new FearStats();
        private readonly FearUnits _fearUnits = new FearUnits();
        private readonly List<TrackedAgent> _flocking = new List<TrackedAgent>(256);
        private readonly bool[] _wallBroken = new bool[FearMath.UnitCount];
        private FearRules _fearRules = new FearRules(false, false, false);
        private string _seenFearText = string.Empty;
        private int _fearNow;
        private bool _firstFearLogged;
        private bool _firstAnchorLogged;
        private bool _firstFearSprintLogged;
        private bool _firstWallBreakLogged;
        private bool _fearClosed;

        /// <summary>This mission's fear-flock numbers (the offline smoke reads them).</summary>
        internal FearStats FearStats => _fearStats;

        /// <summary>The unit table (the offline smoke reads it).</summary>
        internal FearUnits FearUnits => _fearUnits;

        /// <summary>The engine side of the fear flock (the offline smoke swaps in a stand-in).</summary>
        internal IFearBody FearBody { get; set; } = FearBodyDefault.Current;

        /// <summary>Men in a fear-flock spell now / with the steering on now.</summary>
        internal int FearNow => _fearNow;

        internal int FearSteeringNow => _flocking.Count;

        /// <summary>One look's neighbours as the flock reads them (from courage's scan, before it is cleared).</summary>
        private struct FearNear
        {
            public double Px, Py;
            public bool InContact;
            public double EnemyX, EnemyY, EnemyDist;
            public bool Covered;
            public double CoverFraction;
            public bool Behind;
            public bool HasSpot;
            public double SpotX, SpotY;
            public int Mates, Foes;
            public bool HasMass;
            public double MassX, MassY;

            /// <summary>Step 69: courage's local odds (NaN = no enemy within its radius), the enemy near him shaken (0..1), the scan's reach (m).</summary>
            public double Odds, Shaken;
            public float ScanMetres;
        }

        /// <summary>What courage decided at this look (who steers).</summary>
        private enum FearCourage
        {
            /// <summary>Courage is off (it measures only) - the flock decides alone.</summary>
            Off,

            /// <summary>Brave or holding - the flock may steer.</summary>
            Free,

            /// <summary>Courage's walk back runs - it steers (the flock yields).</summary>
            Walking,

            /// <summary>A surge (courage's or the punish's) - brave, the spell ends.</summary>
            Surge,

            /// <summary>His fever presses - the spell ends.</summary>
            FeverPress,
        }

        private static string FearText(TraxSettings s) => FearRules.From(s).Describe();

        private void StartFear()
        {
            _seenFearText = FearText(TraxSettings.Shared);
            TraxLog.Info("fear", "mission start: " + _seenFearText + " - read live; it rides courage's looks (every " + F1(CourageMath.PollSeconds)
                + " s); the lever is courage's (his input: courage's walk back goes first); the [summary] \"fear flock\" lines compare the units' shape, drift and cover ON and OFF");
        }

        private void NoteFearSettings(TraxSettings s)
        {
            string now = FearText(s);
            if (now == _seenFearText) return;
            _seenFearText = now;
            TraxLog.Info("fear", "settings changed mid-mission at " + Sec(SafeNow()) + " s: " + now + " - at every man's next look (within " + F1(CourageMath.PollSeconds) + " s)");
        }

        // ------------------------------------------------------------------ the tick (from TickCourage)

        /// <summary>The fear flock's part of courage's tick: switched off → every spell let go at once; the unit table published every 0.5 s.</summary>
        private void TickFearSwitch(double now)
        {
            _fearRules = FearRules.From(TraxSettings.Shared);
            if (!_fearRules.Enabled && _fearNow > 0) EndAllFear(FearEnd.SwitchedOff, now, native: true, _fearRules.OffBecause);
        }

        private void TickFearUnits(double now)
        {
            _fearStats.Ticks++;
            if (!_fearUnits.Due(now)) return;
            try
            {
                _fearUnits.Publish(now, in _fearRules, _fearStats, _fearRules.Enabled);
                NoteWallBreaks(now);
            }
            catch (Exception e)
            {
                _fearStats.Errors++;
                Failed("fear.publish", e);
            }
        }

        /// <summary>Every tick: the flockers' vectors refreshed every <see cref="FearMath.SteerSeconds"/> each (their frame read then).</summary>
        private void SteerFlockers(double now)
        {
            if (_flocking.Count == 0) return;
            long start = Stopwatch.GetTimestamp();
            for (int i = _flocking.Count - 1; i >= 0; i--)
            {
                if (i >= _flocking.Count) continue;
                var st = _flocking[i];
                var fm = st.Fear;
                try
                {
                    if (st.Removed || !fm.Wish || !CourageBody.IsActive(st))
                    {
                        _flocking.RemoveAt(i);
                        continue;
                    }
                    if (now >= fm.NextSteer) SteerFlocker(st, fm, now);
                }
                catch (Exception e)
                {
                    _fearStats.Errors++;
                    Failed("fear.steer", e);
                    SafeEndFear(st, now);
                }
            }
            _fearStats.SteerCostMs += (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }

        // ------------------------------------------------------------------ the look (from PollCourage)

        /// <summary>Courage's scan read for the flock (before it is cleared): the nearest enemy, a comrade covering him, somebody behind him,
        /// the spot behind the nearest comrade in front, mates / foes, the enemy's mass (toughness-weighted, from courage's sums).</summary>
        private void FearScan(ref FearNear n, int nearestIndex, double ex, double ey, double ew)
        {
            n.InContact = nearestIndex >= 0;
            n.Covered = n.Behind = n.HasSpot = false;
            n.CoverFraction = double.NaN;
            n.HasMass = ew > 0;
            n.MassX = ew > 0 ? ex / ew : double.NaN;
            n.MassY = ew > 0 ? ey / ew : double.NaN;
            if (!n.InContact) return;
            var scan = _courageScan;
            n.EnemyX = scan.X[nearestIndex];
            n.EnemyY = scan.Y[nearestIndex];
            double ux = n.EnemyX - n.Px, uy = n.EnemyY - n.Py;
            double d = Math.Sqrt(ux * ux + uy * uy);
            n.EnemyDist = d;
            if (!(d > 1e-6)) return;
            ux /= d;
            uy /= d;
            var r = _fearRules;
            double nearestCover = double.MaxValue, nearestSpot = double.MaxValue;
            for (int i = 0; i < scan.Count; i++)
            {
                var o = scan.Men[i];
                if (o == null || scan.Enemy[i]) continue;
                FearMath.AlongAcross(n.Px, n.Py, ux, uy, scan.X[i], scan.Y[i], out double along, out double across);
                if (FearMath.Covers(in r, along, across, d))
                {
                    n.Covered = true;
                    if (along < nearestCover)
                    {
                        nearestCover = along;
                        n.CoverFraction = o.Fraction;
                    }
                }
                else if (FearMath.Behind(along, across)) n.Behind = true;
                if (along > FearMath.CoverMinAlongMetres && along < d)
                {
                    FearMath.BehindSpot(scan.X[i], scan.Y[i], ux, uy, out double sx, out double sy);
                    double sd = (sx - n.Px) * (sx - n.Px) + (sy - n.Py) * (sy - n.Py);
                    if (sd < nearestSpot)
                    {
                        nearestSpot = sd;
                        n.HasSpot = true;
                        n.SpotX = sx;
                        n.SpotY = sy;
                    }
                }
            }
        }

        /// <summary>A man courage does not look at this time: a held wall man is still counted in his unit (its morale breaks the wall);
        /// any other gate ends his spell.</summary>
        private void FearSkipped(TrackedAgent st, CourageSkip skip, double now)
        {
            var fm = st.Fear;
            if (skip != CourageSkip.WallHeld)
            {
                if (fm.Flocking) EndFear(st, FearEnd.Excluded, now, native: true);
                return;
            }
            if (fm.Flocking) EndFear(st, FearEnd.WallHolds, now, native: true);
            var r = _fearRules;
            if (!FearBody.Unit(st, out int key, out var order, out double cx, out double cy, out double fx, out double fy)) return;
            fm.Key = key;
            CourageBody.Position(st, out double px, out double py);
            double morale = st.Morale = CourageBody.Morale(st);
            double fraction = r.AthleticsEnabled ? st.Fraction : 1.0;
            double d = FearMath.Discipline(in r, st.CourageLevel < 0 ? 0 : st.CourageLevel, st.IsHero);
            double fear = FearMath.Mixed(in r, FearMath.Own(in r, morale, fraction, d), _fearUnits.FearOf(key, now));
            _fearUnits.Note(key, fear, 0, morale, fraction, false, double.NaN, double.NaN, cx, cy, fx, fy, px, py, order);
            IdleLook(st, now, px, py, order, double.NaN); // step 69: a held wall man standing off counts too (no scan: one enemy read)
        }

        /// <summary>
        /// One man's fear-flock look (courage's gates passed, courage decided): his fear and pull into the table and the stats (ON and OFF);
        /// while on - the gates (a surge / fever press, a retreat, a wall that holds, courage's walk back), the spell, his place, the cover,
        /// the anchor, the steering and the sprint back.
        /// </summary>
        private void FearLook(TrackedAgent st, double now, in FearNear near, FearCourage courage)
        {
            long start = Stopwatch.GetTimestamp();
            try
            {
                FearLookCore(st, now, in near, courage);
            }
            catch (Exception e)
            {
                _fearStats.Errors++;
                Failed("fear.look", e);
                SafeEndFear(st, now);
            }
            _fearStats.LookCostMs += (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }

        private void FearLookCore(TrackedAgent st, double now, in FearNear near, FearCourage courage)
        {
            var r = _fearRules;
            var fm = st.Fear;
            fm.LastLook = now;
            if (!FearBody.Unit(st, out int key, out var order, out double cx, out double cy, out double fx, out double fy))
            {
                if (fm.Flocking) EndFear(st, FearEnd.Excluded, now, native: true);
                return;
            }
            fm.Key = key;
            if (!fm.Seen)
            {
                fm.Seen = true;
                _fearStats.MenLooked++;
            }
            double morale = st.Morale = CourageBody.Morale(st);
            double fraction = r.AthleticsEnabled ? st.Fraction : 1.0;
            double discipline = fm.Discipline = FearMath.Discipline(in r, st.CourageLevel < 0 ? 0 : st.CourageLevel, st.IsHero);
            double own = FearMath.Own(in r, morale, fraction, discipline, near.InContact, near.Behind);
            double unitFear = _fearUnits.FearOf(key, now);
            double baseFear = FearMath.Mixed(in r, own, unitFear); // step 67's fear (the unit's table feeds on it)
            // ---- step 69: WINNING CALMS - his side's local odds and the enemy near him shaken, the unit's win when better; losing = unchanged
            double win = FearMath.Win(in r, near.Odds, near.Shaken);
            double unitWin = _fearUnits.WinOf(key, now);
            double winMix = FearMath.WinMixed(in r, win, unitWin);
            double fear = FearMath.Calmed(baseFear, winMix);
            double pull = FearMath.Pull(in r, fear);
            bool afraidBefore = FearMath.Afraid(in r, baseFear, fm.Flocking);
            bool afraid = FearMath.Afraid(in r, fear, fm.Flocking);
            bool on = r.Enabled;
            // ---- step 69: NO PULL WITHOUT A THREAT - an enemy within FearThreatMetres (courage's scan; beyond its reach one more read, only
            // for an afraid man while the flock is on)
            bool threat = true;
            if (afraid && r.ThreatMetres > 0)
            {
                double nearest = near.InContact ? near.EnemyDist : double.NaN;
                if (double.IsNaN(nearest) && on && r.ThreatMetres > near.ScanMetres)
                {
                    nearest = FearBody.NearestEnemy(st, (float)r.ThreatMetres);
                    _fearStats.ThreatReads++;
                }
                threat = FearMath.Threatened(in r, nearest);
            }
            bool afraidNow = afraid && threat;
            fm.Own = own;
            fm.BaseFear = baseFear;
            fm.Win = winMix;
            fm.Threat = threat;
            fm.Fear = fear;
            fm.Pull = afraidNow ? pull : 0;
            fm.Covered = near.Covered;
            fm.SomebodyBehind = near.Behind;
            fm.InContact = near.InContact;
            bool alone = near.Foes > 0 && near.Mates < FearMath.AloneMates;
            _fearUnits.Note(key, baseFear, fm.Pull, morale, fraction, near.HasMass, near.MassX, near.MassY, cx, cy, fx, fy, near.Px, near.Py, order, win);
            _fearStats.AddLook(on, near.InContact, near.Covered, near.Behind, alone);
            _fearStats.AddFear(discipline, fear, fm.Pull, afraidNow);
            _fearStats.AddWin(in r, near.Odds, winMix, baseFear, fear, afraidBefore, afraidNow);
            if (afraidBefore && !afraid) _fearStats.CalmedByWinLooks++;
            if (afraid && !threat) _fearStats.NoThreatLooks++;
            IdleLook(st, now, near.Px, near.Py, order, near.InContact ? near.EnemyDist : double.NaN); // step 69: the idle-men diagnostic (ON and OFF)
            if (!on)
            {
                if (fm.Flocking) EndFear(st, FearEnd.SwitchedOff, now, native: true);
                return;
            }

            // ---- the gates: courage's surge / fever press, a retreat, a wall that holds, courage's walk back
            if (courage == FearCourage.Surge || courage == FearCourage.FeverPress)
            {
                if (fm.Flocking) EndFear(st, courage == FearCourage.Surge ? FearEnd.Surge : FearEnd.FeverPress, now, native: true);
                return;
            }
            if (order == FearOrder.Retreat)
            {
                if (fm.Flocking) EndFear(st, FearEnd.Retreat, now, native: true);
                return;
            }
            if (order == FearOrder.Wall && !FearMath.WallBreaks(in r, _fearUnits.MoraleOf(key, now)))
            {
                if (fm.Flocking) EndFear(st, FearEnd.WallHolds, now, native: true);
                return;
            }
            if (!afraidNow)
            {
                if (fm.Flocking) EndFear(st, !threat ? FearEnd.NoThreat : afraidBefore ? FearEnd.Winning : FearEnd.Calm, now, native: true);
                return;
            }
            if (!fm.Flocking)
            {
                fm.Start(now, FearPhase.Flocking);
                _fearNow++;
                _fearStats.Spells++;
                if (!fm.EverFlocked)
                {
                    fm.EverFlocked = true;
                    _fearStats.SpellMen++;
                }
            }
            _fearStats.AddFlockLook(order);
            if (courage == FearCourage.Walking)
            {
                // courage's walk back steers him (it goes first): the flock's wish off while it runs; the spell goes on
                if (fm.Wish) StopFlockInput(st, now, native: true);
                _fearStats.YieldedLooks++;
                return;
            }

            // ---- his place: slot → shelter by the pull (a charge: the loose mass around the shelter)
            double sx = cx, sy = cy;
            if (_fearUnits.Live(key, now) && _fearUnits.HasShelter[key])
            {
                sx = _fearUnits.ShelterX[key];
                sy = _fearUnits.ShelterY[key];
            }
            double px = near.Px, py = near.Py;
            double tx, ty;
            bool loose = false;
            double slotX = double.NaN, slotY = double.NaN;
            // step 69: a charging unit whose men win (the enemy near it few or broken) is MOPPING UP - no leash, they go for the kill
            bool mopUp = order == FearOrder.Charge && FearMath.MopUp(in r, unitWin);
            fm.MopUp = mopUp;
            if (order == FearOrder.Charge)
            {
                double radius = FearMath.LooseRadius(in r, pull);
                double ds = Math.Sqrt((sx - px) * (sx - px) + (sy - py) * (sy - py));
                bool pulled = fm.Wish && !fm.Loose;
                loose = mopUp || (pulled ? ds <= radius : ds <= radius + FearMath.ChargeAgainMetres);
                tx = sx;
                ty = sy;
            }
            else
            {
                if (!FearBody.Slot(st, out slotX, out slotY))
                {
                    slotX = px;
                    slotY = py;
                }
                FearMath.Place(in r, slotX, slotY, sx, sy, pull, out tx, out ty);
            }

            // ---- the cover: a FRESH comrade in front anchors him a while; uncovered, he steps in behind the nearest comrade
            var phase = FearPhase.Flocking;
            if (mopUp)
            {
                // the mop-up: no anchor, no stepping in behind a comrade - his AI's charge drives him
                if (!double.IsNaN(fm.AnchorSince))
                {
                    _fearStats.AddAnchorEnd(now - fm.AnchorSince, broken: false);
                    fm.AnchorSince = double.NaN;
                }
                fm.AnchorSpent = false;
            }
            else if (near.InContact)
            {
                if (near.Covered)
                {
                    if (!fm.AnchorSpent && FearMath.Anchors(in r, near.CoverFraction))
                    {
                        if (double.IsNaN(fm.AnchorSince))
                        {
                            fm.AnchorSince = now;
                            _fearStats.Anchors++;
                            if (!_firstAnchorLogged)
                            {
                                _firstAnchorLogged = true;
                                TraxLog.Info("fear", "first ANCHOR this battle: " + Name(st) + " at " + Sec(now) + " s - fear " + F2(fear) + " (pull " + F2(pull)
                                    + "), a comrade between him and the nearest enemy (" + F1(near.EnemyDist) + " m) with " + F0(100 * near.CoverFraction)
                                    + "% of his bar (anchors from " + r.AnchorFreshPercent + "%): he holds this spot up to " + F0(r.AnchorSeconds) + " s");
                            }
                        }
                        if (FearMath.AnchorRunning(in r, fm.AnchorSince, now))
                        {
                            tx = px;
                            ty = py;
                            phase = FearPhase.Anchored;
                            loose = false;
                        }
                        else
                        {
                            _fearStats.AddAnchorEnd(now - fm.AnchorSince, broken: false);
                            fm.AnchorSince = double.NaN;
                            fm.AnchorSpent = true;
                        }
                    }
                    else if (!double.IsNaN(fm.AnchorSince))
                    {
                        _fearStats.AddAnchorEnd(now - fm.AnchorSince, broken: true); // the comrade tired (or the anchor switched off)
                        fm.AnchorSince = double.NaN;
                        fm.AnchorSpent = true;
                    }
                }
                else
                {
                    if (!double.IsNaN(fm.AnchorSince))
                    {
                        _fearStats.AddAnchorEnd(now - fm.AnchorSince, broken: false);
                        fm.AnchorSince = double.NaN;
                    }
                    fm.AnchorSpent = false; // a new cover may anchor him again
                    if (near.HasSpot && pull > 0)
                    {
                        tx += (near.SpotX - tx) * pull;
                        ty += (near.SpotY - ty) * pull;
                        loose = false;
                        _fearStats.CoverSeeks++;
                    }
                }
            }
            else if (!double.IsNaN(fm.AnchorSince))
            {
                _fearStats.AddAnchorEnd(now - fm.AnchorSince, broken: false);
                fm.AnchorSince = double.NaN;
                fm.AnchorSpent = false;
            }
            fm.Phase = phase;
            fm.TargetX = tx;
            fm.TargetY = ty;
            fm.HasTarget = true;
            fm.Loose = loose;
            if (phase == FearPhase.Flocking && order != FearOrder.Charge && FearMath.NearSlot(tx, ty, slotX, slotY))
            {
                // a small pull: his place is near his slot - the game's own AI keeps him (no tug of war with its walk to the slot)
                _fearStats.NearSlotLooks++;
                if (fm.Wish) StopFlockInput(st, now, native: true);
                return;
            }
            if (loose)
            {
                // a charging man inside the loose mass (or the unit mopping up - step 69): his AI's charge drives him
                if (mopUp) _fearStats.MopUpLooks++;
                else _fearStats.LooseLooks++;
                if (fm.Wish) StopFlockInput(st, now, native: true);
                return;
            }

            // ---- the wish and this look's vector
            if (!fm.Wish && !StartFlockInput(st, fm, now)) return;
            SteerFlocker(st, fm, now);

            // ---- the sprint back: far, or alone with an enemy near; now and then when a few metres off
            double dist = Math.Sqrt((tx - px) * (tx - px) + (ty - py) * (ty - py));
            if (FearMath.SprintWanted(in r, dist, alone)) AskFearSprint(st, fm, now, in r, alone, byChance: false, dist);
            else if (dist > FearMath.ChanceSprintMetres && !st.Sprint.Active && r.SprintChancePercent > 0
                     && CourageDice.NextDouble() < r.SprintChancePercent / 100.0) AskFearSprint(st, fm, now, in r, alone, byChance: true, dist);

            if (!_firstFearLogged)
            {
                _firstFearLogged = true;
                TraxLog.Info("fear", "first fear-flock spell this battle: " + Name(st) + " at " + Sec(now) + " s - morale " + F0(morale) + ", bar " + F0(100 * fraction)
                    + "%, level " + st.CourageLevel + " (discipline " + F2(discipline) + ")" + (near.InContact ? ", an enemy " + F1(near.EnemyDist) + " m off" : ", no enemy near")
                    + (near.InContact && !near.Behind ? ", NOBODY behind him" : string.Empty) + " → his own fear " + F2(own) + ", the unit's "
                    + (double.IsNaN(unitFear) ? "- (first look)" : F2(unitFear)) + " → fear " + F2(baseFear)
                    + (winMix > 0 ? ", calmed by winning " + F2(winMix) + " (his odds " + (double.IsNaN(near.Odds) ? "-" : F2(near.Odds)) + ") to " + F2(fear) : string.Empty) + ", pull " + F2(pull) + " (" + r.StartPercent + "-" + r.FullPercent
                    + "%); order " + FearMath.OrderName(order) + "; his place " + F1(dist) + " m from him, the shelter "
                    + F1(Math.Sqrt((sx - cx) * (sx - cx) + (sy - cy) * (sy - cy))) + " m behind the unit's centre"
                    + (phase == FearPhase.Anchored ? " - ANCHORED behind a fresh comrade" : near.Covered ? " - covered" : near.HasSpot ? " - stepping in behind a comrade" : string.Empty)
                    + (st.Sprint.Active && fm.SprintOurs ? ", sprinting " + F2(st.Sprint.Length) + " s" : string.Empty));
            }
            else if (TraxLog.VerboseWants("fear"))
            {
                TraxLog.Verbose("fear", Name(st) + " flocks at " + Sec(now) + " s (fear " + F2(fear) + ", pull " + F2(pull) + ", " + F1(dist) + " m to his place"
                    + (phase == FearPhase.Anchored ? ", anchored" : string.Empty) + ")", "fear");
            }
        }

        // ------------------------------------------------------------------ the lever

        private bool StartFlockInput(TrackedAgent st, FearMind fm, double now)
        {
            EnsureInput(st);
            AiInputHook.HookResult hook;
            try
            {
                hook = CourageBody.Hook(st);
            }
            catch (Exception e)
            {
                _fearStats.Errors++;
                Failed("fear.hook", e);
                return false; // not hooked = no steering this look (he keeps his AI)
            }
            NoteHooked(st, hook);
            AiInputHook.SetFlock(st, true, now);
            fm.Wish = true;
            fm.Moving = true;
            fm.NextSteer = now;
            fm.NextEdge = now + AiInputMath.EdgeCheckSeconds;
            _flocking.Add(st);
            return true;
        }

        private void StopFlockInput(TrackedAgent st, double now, bool native)
        {
            var fm = st.Fear;
            fm.Wish = false;
            fm.Moving = false;
            _flocking.Remove(st);
            if (fm.SprintOurs)
            {
                // the flock no longer steers him: its sprint ends with it (courage's walk back may ask its own)
                fm.SprintOurs = false;
                if (st.Sprint.Active && st.Sprint.Regroup && st.Courage.Phase != CouragePhase.Moving)
                    EndSprint(st, native ? SprintEnd.Regrouped : SprintEnd.LeftField, now);
            }
            if (st.Input != null && st.Input.Flock)
            {
                AiInputHook.SetFlock(st, false, now);
                if (native && !st.Removed)
                {
                    try
                    {
                        CourageBody.Release(st);
                    }
                    catch (Exception e)
                    {
                        Failed("fear.release", e);
                    }
                }
                DrainInputError(st);
            }
        }

        /// <summary>His vector now: toward his place (the stick slower near it), or 0, 0 within it (he stands); the ground ahead checked while
        /// walking; a flock sprint ended on arrival.</summary>
        private void SteerFlocker(TrackedAgent st, FearMind fm, double now)
        {
            fm.NextSteer = now + FearMath.SteerSeconds;
            var s = st.Input;
            if (s == null || !fm.HasTarget) return;
            var body = CourageBody;
            body.Position(st, out double px, out double py);
            double dx = fm.TargetX - px, dy = fm.TargetY - py;
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (fm.Moving && d <= FearMath.ArriveMetres) fm.Moving = false;
            else if (!fm.Moving && d > FearMath.MoveAgainMetres) fm.Moving = true;
            if (fm.Moving && d > 1e-6)
            {
                dx /= d;
                dy /= d;
                if (now >= fm.NextEdge)
                {
                    fm.NextEdge = now + AiInputMath.EdgeCheckSeconds;
                    if (!body.GroundOk(st, dx, dy)) fm.Moving = false; // the ground ahead ends: he stands
                }
            }
            if (fm.Moving && body.ToLocal(st, dx, dy, out float lx, out float ly))
            {
                float k = (float)FearMath.Stick(d);
                s.FlockX = lx * k;
                s.FlockY = ly * k;
                _fearStats.SteerUpdates++;
            }
            else
            {
                s.FlockX = 0f;
                s.FlockY = 0f;
                _fearStats.StandUpdates++;
                if (fm.SprintOurs && st.Sprint.Active && st.Sprint.Regroup && !st.Courage.Timid)
                {
                    fm.SprintOurs = false;
                    EndSprint(st, SprintEnd.Regrouped, now); // there
                }
            }
        }

        /// <summary>The flock's sprint back: what he holds above FearSprintKeepPercent, at most once in <see cref="FearMath.SprintCooldownSeconds"/>.</summary>
        private void AskFearSprint(TrackedAgent st, FearMind fm, double now, in FearRules r, bool alone, bool byChance, double dist)
        {
            if (!double.IsNaN(fm.LastSprint) && now - fm.LastSprint < FearMath.SprintCooldownSeconds) return;
            fm.LastSprint = now;
            _fearStats.SprintsAsked++;
            if (alone) _fearStats.SprintsAlone++;
            if (byChance) _fearStats.SprintsByChance++;
            var s = SprintRules.From(TraxSettings.Shared);
            if (!s.Enabled || !s.AiOn)
            {
                _fearStats.AddRefusal(CourageSprintRefusal.Off);
                return;
            }
            var sp = st.Sprint;
            if (sp.Active)
            {
                _fearStats.AddRefusal(CourageSprintRefusal.Sprinting);
                return;
            }
            var ar = AthleticsRules.From(TraxSettings.Shared);
            double cap = SprintMath.Capacity(in s, in ar, st);
            if (!(cap > SprintMath.EmptySeconds))
            {
                _fearStats.AddRefusal(CourageSprintRefusal.NoStore);
                return;
            }
            sp.Clamp(cap);
            double amount = FearMath.SprintAmount(in r, sp.StoredUpTo(cap), cap);
            if (sp.TryStartRegroup(in s, now, cap, amount) != SprintStart.Started)
            {
                _fearStats.AddRefusal(CourageSprintRefusal.ReserveKept);
                return;
            }
            _fearStats.SprintsStarted++;
            fm.SprintOurs = true;
            BeginSprint(st, now, in s, in ar, cap);
            if (!_firstFearSprintLogged)
            {
                _firstFearSprintLogged = true;
                TraxLog.Info("fear", "first fear-flock sprint back this battle: " + Name(st) + " at " + Sec(now) + " s - " + F1(dist) + " m from his place"
                    + (alone ? ", ALONE (fewer than " + FearMath.AloneMates + " mates with an enemy near)" : byChance ? " (the " + r.SprintChancePercent + "% roll)" : " (beyond " + F0(r.SprintMetres) + " m)")
                    + ", sprinting " + F2(sp.Length) + " s, keeping " + r.SprintKeepPercent + "% of his store");
            }
        }

        // ------------------------------------------------------------------ the walls

        /// <summary>Step 67's wall gate (step 41's look asks it for every held man): his unit's mean morale fell below FearWallBreakMorale - the
        /// wall breaks and he joins the flock.</summary>
        internal bool FearWallBreaks(TrackedAgent st, double now)
        {
            var r = FearRules.From(TraxSettings.Shared);
            int key = st.Fear.Key;
            if (key < 0) return false;
            return FearMath.WallBreaks(in r, _fearUnits.MoraleOf(key, now));
        }

        /// <summary>A held man let go by the break (counted; the first unit's break in full).</summary>
        internal void NoteWallBreakMan(TrackedAgent st, double now)
        {
            _fearStats.WallBreakMen++;
            int key = st.Fear.Key;
            if (key < 0 || key >= _wallBroken.Length || _wallBroken[key]) return;
            _wallBroken[key] = true;
            _fearStats.WallsBroken++;
            if (_firstWallBreakLogged) return;
            _firstWallBreakLogged = true;
            TraxLog.Info("fear", "first shield wall BROKEN this battle at " + Sec(now) + " s: its men's morale " + F0(_fearUnits.MoraleOf(key, now)) + " < "
                + TraxSettings.Shared.FearWallBreakMorale + " (FearWallBreakMorale) - step 41 lets them go, the flock takes the frightened (first: " + Name(st) + ")");
        }

        /// <summary>A broken wall whose morale came back above the line counts as a new break next time.</summary>
        private void NoteWallBreaks(double now)
        {
            var r = _fearRules;
            for (int k = 0; k < _wallBroken.Length; k++)
                if (_wallBroken[k] && !FearMath.WallBreaks(in r, _fearUnits.MoraleOf(k, now))) _wallBroken[k] = false;
        }

        // ------------------------------------------------------------------ the end

        private void EndFear(TrackedAgent st, FearEnd why, double now, bool native)
        {
            var fm = st.Fear;
            if (!fm.Flocking) return;
            if (!double.IsNaN(fm.AnchorSince)) _fearStats.AddAnchorEnd(now - fm.AnchorSince, broken: false);
            if (fm.Wish) StopFlockInput(st, now, native);
            if (fm.SprintOurs && st.Sprint.Active && st.Sprint.Regroup && !(st.Courage.Phase == CouragePhase.Moving))
                EndSprint(st, native ? SprintEnd.Regrouped : SprintEnd.LeftField, now);
            fm.End(why);
            if (_fearNow > 0) _fearNow--;
            _fearStats.AddEnd(why);
            if (TraxLog.VerboseWants("fear"))
                TraxLog.Verbose("fear", Name(st) + " fear flock ended at " + Sec(now) + " s: " + FearMath.EndName(why), "fear");
        }

        private void SafeEndFear(TrackedAgent st, double now)
        {
            try
            {
                if (st.Fear.Flocking) EndFear(st, FearEnd.Error, now, native: true);
            }
            catch
            {
                st.Fear.End(FearEnd.Error);
                _flocking.Remove(st);
                if (st.Input != null) st.Input.Flock = false;
            }
        }

        private void EndAllFear(FearEnd why, double now, bool native, string? because)
        {
            int ended = 0;
            for (int i = 0; i < _count; i++)
            {
                var st = _dense[i];
                if (!st.Fear.Flocking) continue;
                try
                {
                    EndFear(st, why, now, native);
                    ended++;
                }
                catch (Exception e)
                {
                    Failed("fear.end-all", e);
                    SafeEndFear(st, now);
                }
            }
            _fearNow = 0;
            _flocking.Clear();
            if (why == FearEnd.SwitchedOff && ended > 0)
                TraxLog.Info("fear", (because == "ModEnabled" ? "the whole mod (ModEnabled)" : because ?? "the fear flock") + " switched OFF mid-mission at " + Sec(now) + " s: "
                    + ended + " flocking men let go at once - their formation places again; the looks keep measuring");
        }

        /// <summary>He left the field: his spell ends without an engine call.</summary>
        private void FearLeftField(TrackedAgent st)
        {
            try
            {
                if (st.Fear.Flocking) EndFear(st, FearEnd.LeftField, SafeNow(), native: false);
            }
            catch (Exception e)
            {
                Failed("fear.left", e);
            }
        }

        private void CloseFear(bool agentsAlive)
        {
            if (_fearClosed) return;
            _fearClosed = true;
            if (_fearNow > 0) EndAllFear(FearEnd.MissionEnd, SafeNow(), agentsAlive, null);
        }

        internal void WriteFearSummary()
        {
            var lines = new List<string>(10);
            var r = FearRules.From(TraxSettings.Shared);
            _fearStats.AddSummaryLines(lines, in r);
            _fearStats.AddWinLines(lines, in r); // step 69
            if (_fearStats.MenLooked > 0) _idleStats.AddSummaryLines(lines); // step 69
            foreach (var line in lines) TraxLog.Info("summary", line);
        }

        // ------------------------------------------------------------------ step 69: the idle-men diagnostic

        private readonly IdleStats _idleStats = new IdleStats();
        private readonly bool[] _firstIdleLogged = new bool[(int)IdleHolder.Count];

        /// <summary>This mission's idle-men numbers (the offline smoke reads them).</summary>
        internal IdleStats IdleStats => _idleStats;

        /// <summary>
        /// Step 69 (measurement only - it changes nothing): at his look, an AI man on foot whose unit charges / advances, standing (his
        /// displacement since the last look below 0.5 m/s), an enemy within 25 m and none within 3 m (courage's scan within its reach; beyond
        /// it one enemy read, only for a standing man of a moving order), for more than 5 s - counted by who held him: a ranged weapon / formation
        /// (normal), the wall's hold, the fear flock's steering, courage's timidity, the brace, else the game. The first of each in full.
        /// </summary>
        private void IdleLook(TrackedAgent st, double now, double px, double py, FearOrder order, double nearestInScan)
        {
            try
            {
                var m = st.Idle;
                var stats = _idleStats;
                stats.Looks++;
                double gap = IdleMath.Gap(m, now, px, py, out double speed);
                bool moving = order == FearOrder.Charge || order == FearOrder.Advance || (order == FearOrder.Wall && IdleMath.MovingOrder(FearBody.OrderKind(st)));
                if (!moving)
                {
                    m.Reset();
                    return;
                }
                stats.MovingOrderLooks++;
                if (double.IsNaN(gap) || !(speed < IdleMath.StandMetresPerSecond))
                {
                    m.Reset();
                    return;
                }
                double nearest = nearestInScan;
                if (double.IsNaN(nearest))
                {
                    nearest = FearBody.NearestEnemy(st, (float)IdleMath.EnemyMetres);
                    stats.EnemyReads++;
                }
                double add = IdleMath.Advance(m, now, gap, IdleMath.Idle(true, speed, nearest));
                if (!(add > 0)) return;
                bool wall = st.Wall != null && st.Wall.Held;
                var fm = st.Fear;
                bool fear = fm.Wish;
                bool timid = st.Courage.Timid;
                bool brace = st.Brace != null && st.Brace.Active;
                var h = IdleMath.Holder(FearBody.Ranged(st), wall, fear, timid, brace);
                stats.Add(m, h, add, nearest);
                if (h == IdleHolder.Ranged && (wall || fear || timid || brace)) stats.RangedOursSeconds += add;
                int i = (int)h;
                if (_firstIdleLogged[i]) return;
                _firstIdleLogged[i] = true;
                TraxLog.Info("fear", "first IDLE man held by " + IdleMath.HolderName(h) + " (step 69): " + Name(st) + " at " + Sec(now) + " s - his unit on "
                    + FearMath.OrderName(order) + (order == FearOrder.Wall ? " (" + FearBody.OrderKind(st) + ")" : string.Empty) + ", the nearest enemy " + F1(nearest)
                    + " m (none within " + F0(IdleMath.ContactMetres) + " m), standing (" + F2(speed) + " m/s) for " + F1(now - m.Since) + " s; fear " + F2(fm.BaseFear)
                    + " → " + F2(fm.Fear) + " (win " + F2(fm.Win) + (fm.Threat ? string.Empty : ", no threat") + "), pull " + F2(fm.Pull) + ", flock "
                    + (fm.Flocking ? fm.Phase + (fm.Wish ? " steering" : fm.Loose ? " loose" : " near his slot") : "calm") + (fm.MopUp ? ", MOP-UP" : string.Empty)
                    + "; courage " + (timid ? "TIMID (" + st.Courage.Phase + ")" : "brave") + "; brace " + (brace ? "ON" : "off") + "; wall " + (wall ? "HELD" : "no")
                    + "; his bar " + F0(100 * st.Fraction) + "%");
            }
            catch (Exception e)
            {
                _idleStats.Errors++;
                Failed("fear.idle", e);
            }
        }
    }
}
