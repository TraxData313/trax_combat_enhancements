using System;
using System.Collections.Generic;
using System.Diagnostics;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 49 - COURAGE AND COHESION (Anton, 2026-10-17 night: "AI men are unrealistically brave … real men stick with their group, not out
    /// of professionalism, out of instinct"; AI_NOTES "Step 49", DESIGN §2 "Courage and cohesion"). Every AI man on foot in a formation is
    /// looked at about every <see cref="CourageMath.PollSeconds"/> (staggered, the brace's pattern): the tracked men within
    /// CourageRadiusMetres (one native proximity query into a reused list), each weighed by his TOUGHNESS (armour, level, wounds; an enemy
    /// with a two-hander × CourageHeavyWeaponPercent), his side's sum ÷ the enemy's = his local odds, against the odds he needs (the base ×
    /// tiredness × morale × SHOCK × fever × hero). Below it he is TIMID: far from his mates he WALKS BACK - to the nearest bigger group of
    /// his side (FLOCKING), else his formation's centre of mass - through his own input (step 16's backpedal: he keeps facing whatever his AI
    /// faces), sprinting when far or pressed (in a PANIC below his reserve); among his mates he HOLDS with tightened behaviour weights (he
    /// strikes only within reach). Brave again above the required odds × the hysteresis after the minimum hold. The looks run ON and OFF -
    /// the [summary] "courage" lines compare isolated men and their distance from the formation's centre both ways. No allocation per tick.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly CourageStats _courageStats = new CourageStats();
        private readonly CourageScan _courageScan = new CourageScan();
        private readonly List<TrackedAgent> _walking = new List<TrackedAgent>(64);
        private string _seenCourageText = string.Empty;
        private double _courageLastTick = double.NaN;
        private double _courageCarry;
        private int _courageCursor;
        private int _timidNow;
        private bool _firstTimidLogged;
        private bool _firstWalkEndLogged;
        private bool _firstPanicLogged;
        private bool _firstSurgeLogged;

        /// <summary>Step 49b: the surge's dice (the smoke swaps in a fixed one).</summary>
        internal IRandomSource CourageDice { get; set; } = ThreadSafeRandom.Shared;
        private TrackedAgent? _firstWalker;
        private bool _courageClosed;

        /// <summary>This mission's courage numbers (the offline smoke reads them).</summary>
        internal CourageStats CourageStats => _courageStats;

        /// <summary>The engine side of courage (the offline smoke swaps in a stand-in).</summary>
        internal ICourageBody CourageBody { get; set; } = CourageBodyDefault.Current;

        /// <summary>Men timid now / walking back now.</summary>
        internal int TimidNow => _timidNow;
        internal int WalkingNow => _walking.Count;

        private static string CourageText(TraxSettings s) => CourageRules.From(s).Describe();

        private void StartCourage()
        {
            _courageScan.Lookup = Get;
            _seenCourageText = CourageText(TraxSettings.Shared);
            TraxLog.Info("courage", "mission start: " + _seenCourageText + " - read live; every AI man looked at about every " + F1(CourageMath.PollSeconds)
                + " s; the [summary] \"courage\" lines compare isolated men and their distance from the formation's centre with courage ON and OFF");
            StartFear(); // step 67: the fear flock rides these looks
        }

        /// <summary>A courage setting changed (from ApplySettingsChange): one line; applied at every man's next look.</summary>
        private void NoteCourageSettings(TraxSettings s)
        {
            NoteFearSettings(s); // step 67
            string now = CourageText(s);
            if (now == _seenCourageText) return;
            _seenCourageText = now;
            TraxLog.Info("courage", "settings changed mid-mission at " + Sec(SafeNow()) + " s: " + now + " - at every man's next look (within "
                + F1(CourageMath.PollSeconds) + " s)");
        }

        // ------------------------------------------------------------------ the tick

        /// <summary>Every tick: switched off → every timid man let go at once (walks stopped, the game's weights back); then the staggered
        /// looks (ON and OFF - the OFF looks only measure) and, while on, the walkers steered.</summary>
        internal void TickCourage(double now)
        {
            var cr = CourageRules.From(TraxSettings.Shared);
            if (!cr.Enabled && _timidNow > 0) EndAllTimid(CourageEnd.SwitchedOff, now, native: true, cr.OffBecause);
            TickFearSwitch(now); // step 67: the fear flock's switch (the master switch first) - every spell let go at once when off
            var body = CourageBody;
            if (!body.MissionAllows())
            {
                _courageLastTick = now;
                return;
            }
            _courageStats.Ticks++;
            TickFearUnits(now); // step 67: the unit table published every 0.5 s (the shelters, the shape / drift samples)
            double dt = double.IsNaN(_courageLastTick) || now < _courageLastTick ? CourageMath.PollSeconds : now - _courageLastTick;
            _courageLastTick = now;
            if (dt > CourageMath.PollSeconds) dt = CourageMath.PollSeconds;
            _courageCarry += _count * dt / CourageMath.PollSeconds;
            int n = (int)(_courageCarry + 1e-9);
            _courageCarry -= n;
            if (_courageCarry < 0) _courageCarry = 0;
            if (n > _count) n = _count;
            long start = Stopwatch.GetTimestamp();
            if (n > 0)
            {
                bool impactLive = ImpactLive();
                for (int k = 0; k < n; k++)
                {
                    if (_courageCursor >= _count) _courageCursor = 0;
                    var st = _dense[_courageCursor++];
                    try
                    {
                        PollCourage(st, now, in cr, impactLive);
                    }
                    catch (Exception e)
                    {
                        Failed("courage.poll", e);
                        SafeEndTimid(st, now);
                    }
                }
            }
            long mid = Stopwatch.GetTimestamp();
            _courageStats.PollCostMs += (mid - start) * 1000.0 / Stopwatch.Frequency;
            if (cr.Enabled && _walking.Count > 0) SteerWalkers(now, in cr);
            _courageStats.SteerCostMs += (Stopwatch.GetTimestamp() - mid) * 1000.0 / Stopwatch.Frequency;
            if (_fearRules.Enabled) SteerFlockers(now); // step 67
        }

        /// <summary>An impact family changes the battle now (then its outcomes are shock too) - once a tick.</summary>
        private static bool ImpactLive()
        {
            var ir = new ImpactRules(TraxSettings.Shared);
            return ir.Applies(ImpactFamily.Kicks) || ir.Applies(ImpactFamily.Blocks) || ir.Applies(ImpactFamily.Hits) || ir.Applies(ImpactFamily.Riposte);
        }

        /// <summary>His shock events in the window: landed hits (OnAgentHit) + live impact outcomes.</summary>
        private static int ShockEvents(TrackedAgent st, double now, in CourageRules cr, bool impactLive)
        {
            if (!(cr.ShockSeconds > 0)) return 0;
            int n = st.Hits.Recent(now, cr.ShockSeconds);
            if (impactLive) n += st.Impact.Recent(now, cr.ShockSeconds);
            return n;
        }

        /// <summary>A man's toughness now (his kit read once).</summary>
        private double CourageToughness(TrackedAgent o, in CourageRules cr)
        {
            if (double.IsNaN(o.CourageBase))
            {
                double armour = 0;
                int level = 0;
                bool heavy = false;
                try
                {
                    CourageBody.ReadKit(o, out armour, out level, out heavy);
                }
                catch (Exception e)
                {
                    Failed("courage.kit", e); // counted as a plain man in rags
                }
                o.CourageBase = CourageMath.BaseToughness(in cr, armour, level);
                o.CourageHeavy = heavy;
                o.CourageLevel = level; // step 67: his discipline
                _courageStats.AddToughness(o.CourageBase);
            }
            return CourageMath.Toughness(in cr, o.CourageBase, CourageBody.HealthShare(o));
        }

        private void PollCourage(TrackedAgent st, double now, in CourageRules cr, bool impactLive)
        {
            if (st.Removed) return;
            var body = CourageBody;
            if (!body.IsActive(st)) return;
            var cm = st.Courage;
            double gap = double.IsNaN(cm.LastLook) || now < cm.LastLook ? 0 : now - cm.LastLook;
            if (gap > 4 * CourageMath.PollSeconds) gap = 0; // a longer gap = not looked at meanwhile
            cm.LastLook = now;
            var skip = IsPlayer(st) ? CourageSkip.NotAi : body.Gate(st);
            if (skip == CourageSkip.Count && st.Wall != null && st.Wall.Held) skip = CourageSkip.WallHeld;
            if (skip != CourageSkip.Count)
            {
                _courageStats.AddSkip(skip);
                if (cm.Timid) EndTimid(st, CourageEnd.Excluded, now, native: true);
                FearSkipped(st, skip, now); // step 67: a held wall man still counts in his unit (its morale breaks the wall); any other gate ends his spell
                return;
            }
            if (!st.CourageSeen)
            {
                st.CourageSeen = true;
                _courageStats.MenLooked++;
            }

            // ---- the circle: his side's toughness vs the enemy's, weighted by distance
            double self = CourageToughness(st, in cr);
            double allies = self, enemies = 0, ex = 0, ey = 0, ew = 0;
            int mates = 0, foes = 0;
            // step 49b: the scan reaches 8 m at least (the surge's and the standoff's nearest enemy); the odds still count within the radius
            float radius = (float)Math.Max(cr.RadiusMetres, CourageMath.SurgeEnemyMetres);
            double nearest = double.NaN;
            TrackedAgent? nearestFoe = null;
            int nearestIndex = -1;
            body.Scan(st, radius, _courageScan);
            if (_courageScan.Truncated) _courageStats.ScanTruncated++;
            for (int i = 0; i < _courageScan.Count; i++)
            {
                var o = _courageScan.Men[i];
                if (o == null || ReferenceEquals(o, st)) continue;
                if (_courageScan.Enemy[i] && (double.IsNaN(nearest) || _courageScan.Dist[i] < nearest))
                {
                    nearest = _courageScan.Dist[i];
                    nearestFoe = o;
                    nearestIndex = i;
                }
                double w = CourageMath.Weight(_courageScan.Dist[i], cr.RadiusMetres);
                if (!(w > 0)) continue;
                double t = CourageToughness(o, in cr);
                if (_courageScan.Enemy[i])
                {
                    double te = w * CourageMath.EnemyToughness(in cr, t, o.CourageHeavy);
                    enemies += te;
                    ex += te * _courageScan.X[i];
                    ey += te * _courageScan.Y[i];
                    ew += te;
                    foes++;
                }
                else
                {
                    allies += w * t;
                    mates++;
                }
            }
            body.Position(st, out double px, out double py);
            // step 67: the fear flock reads the same scan (the nearest enemy, a comrade covering him, somebody behind, the spot behind a comrade)
            var near = new FearNear { Px = px, Py = py, Mates = mates, Foes = foes };
            FearScan(ref near, nearestIndex, ex, ey, ew);
            _courageScan.Clear();
            double odds = CourageMath.Odds(allies, enemies);
            bool hasCentre = body.FormationCentre(st, out double cx, out double cy);
            double dist = hasCentre ? Math.Sqrt((cx - px) * (cx - px) + (cy - py) * (cy - py)) : double.NaN;

            // ---- the measurement (ON and OFF)
            bool on = cr.Enabled;
            _courageStats.AddLook(on, gap, foes > 0 && mates < CourageMath.IsolatedMates, foes > 0);
            _courageStats.AddDistance(on, dist);
            if (!on)
            {
                // step 67: courage measures only - the flock decides alone; a fever without shock still sends him in (courage's 49b rule)
                var fvOff = st.Fever;
                var frOff = FeverRules.From(TraxSettings.Shared);
                double feverOff = frOff.Enabled ? CourageMath.FeverShare(fvOff.Phase, fvOff.Left, fvOff.Full) : 0;
                bool shockedOff = CourageMath.ShockShare(in cr, ShockEvents(st, now, in cr, impactLive)) > 0;
                FearLook(st, now, in near, CourageMath.FeverPresses(feverOff, shockedOff) ? FearCourage.FeverPress : FearCourage.Off);
                return;
            }

            // ---- the decision
            var fv = st.Fever;
            var fr = FeverRules.From(TraxSettings.Shared);
            double fever = fr.Enabled ? CourageMath.FeverShare(fv.Phase, fv.Left, fv.Full) : 0;
            int shockEvents = ShockEvents(st, now, in cr, impactLive);
            double shock = CourageMath.ShockShare(in cr, shockEvents);
            bool shocked = shock > 0;
            double required = CourageMath.Required(in cr, st.Fraction, body.Morale(st), fever, st.IsHero, shock, out var parts);
            double awayX = 0, awayY = 0;
            if (ew > 0)
            {
                awayX = px - ex / ew;
                awayY = py - ey / ew;
            }
            // ---- step 49b: a surge running = brave, left alone; a fever without shock = he presses whatever the odds
            if (cm.Surging(now))
            {
                if (cm.Timid) EndTimid(st, CourageEnd.Surge, now, native: true);
                if (cm.WeightsSet) RestoreWeights(st, cm);
                if (foes > 0) _courageStats.AddOdds(odds, false, dist > cr.RegroupRadiusMetres * CourageMath.OutOfFormationRadii);
                FearLook(st, now, in near, FearCourage.Surge); // step 67: a surge sends him in - the flock lets go
                return;
            }
            if (CourageMath.FeverPresses(fever, shocked))
            {
                double calm = CourageMath.Required(in cr, st.Fraction, body.Morale(st), 0, st.IsHero, 0, out _);
                if (CourageMath.Timid(odds, calm)) _courageStats.FeverPressLooks++;
                if (cm.Timid)
                {
                    _courageStats.FeverPressEnds++;
                    EndTimid(st, CourageEnd.FeverPress, now, native: true);
                }
                if (cm.WeightsSet) RestoreWeights(st, cm);
                if (foes > 0) _courageStats.AddOdds(odds, false, dist > cr.RegroupRadiusMetres * CourageMath.OutOfFormationRadii);
                FearLook(st, now, in near, FearCourage.FeverPress); // step 67: the fever presses - the flock lets go
                return;
            }
            var before = cm.Phase;
            double lookDistance = dist;
            if (before == CouragePhase.Moving)
            {
                PickTarget(st, cm, in cr, hasCentre, cx, cy);
                lookDistance = Math.Sqrt((cm.CentreX - px) * (cm.CentreX - px) + (cm.CentreY - py) * (cm.CentreY - py));
            }
            int wobblesBefore = cm.Wobbles;
            bool everBefore = cm.EverTimid;
            var step = cm.Look(in cr, now, odds, required, lookDistance, mates, shocked);
            _courageStats.Wobbles += cm.Wobbles - wobblesBefore;
            if (!everBefore && cm.EverTimid) _courageStats.TimidMen++;
            bool panic = CourageMath.Panic(in cr, shock, odds, required);
            switch (step)
            {
                case CourageStep.TimidMoving:
                case CourageStep.TimidHolding:
                    _timidNow++;
                    _courageStats.TimidStarts++;
                    if (shocked) _courageStats.TimidByShock++;
                    cm.ShockStart = shocked;
                    if (step == CourageStep.TimidMoving)
                    {
                        _courageStats.StartedMoving++;
                        BeginWalk(st, cm, now, in cr, hasCentre, cx, cy, dist, px, py, awayX, awayY, foes, panic, odds, required, in parts, mates, self, allies, enemies, shockEvents);
                    }
                    else
                    {
                        _courageStats.StartedHolding++;
                        if (TraxLog.VerboseWants("courage"))
                            TraxLog.Verbose("courage", Name(st) + " timid at " + Sec(now) + " s, holding among " + mates + " mates (odds " + F2(odds) + " < " + F2(required) + ")", "courage");
                    }
                    break;
                case CourageStep.StartMoving:
                    _courageStats.MovedAgain++;
                    BeginWalk(st, cm, now, in cr, hasCentre, cx, cy, dist, px, py, awayX, awayY, foes, panic, odds, required, in parts, mates, self, allies, enemies, shockEvents);
                    break;
                case CourageStep.StopMoving:
                    WalkEnded(st, cm, now, dist);
                    break;
                case CourageStep.Brave:
                    if (before == CouragePhase.Moving) WalkEnded(st, cm, now, dist);
                    EndTimidCommon(st, CourageEnd.Brave, now, native: true);
                    break;
            }
            // ---- step 49b: now and then a timid man among his mates steps in (a SURGE); the standoff measure
            if (CourageMath.SurgeEligible(in cr, cm.Timid, shocked, mates, nearest))
            {
                _courageStats.SurgeEligibleLooks++;
                if (CourageMath.SurgeRoll(CourageMath.SurgeChance(in cr, st.Fraction), CourageDice.NextDouble()))
                {
                    cm.SurgeUntil = now + cr.SurgeSeconds;
                    _courageStats.Surges++;
                    EndTimid(st, CourageEnd.Surge, now, native: true);
                    FearLook(st, now, in near, FearCourage.Surge); // step 67
                    if (!_firstSurgeLogged)
                    {
                        _firstSurgeLogged = true;
                        TraxLog.Info("courage", "first surge this battle: " + Name(st) + " at " + Sec(now) + " s - timid among " + mates + " mates (odds " + F2(odds) + " < " + F2(required)
                            + "), an enemy " + F1(nearest) + " m off: brave for " + F1(cr.SurgeSeconds) + " s (CourageSurgeChancePercent " + cr.SurgeChancePercent + " x his bar "
                            + F0(100 * st.Fraction) + "%)");
                    }
                    return;
                }
            }
            if (cm.Timid && gap > 0)
            {
                double lastAttack = Math.Max(st.LastReleaseTime, nearestFoe != null ? nearestFoe.LastReleaseTime : -1);
                if (CourageMath.Standoff(true, nearest, lastAttack, now)) _courageStats.StandoffSeconds += gap;
            }
            if (cm.Timid && foes > 0) _courageStats.AddOdds(odds, true, dist > cr.RegroupRadiusMetres * CourageMath.OutOfFormationRadii);
            else if (foes > 0) _courageStats.AddOdds(odds, false, dist > cr.RegroupRadiusMetres * CourageMath.OutOfFormationRadii);

            // ---- while walking: the target, "away", a panic's sprint
            if (cm.Phase == CouragePhase.Moving && step != CourageStep.TimidMoving && step != CourageStep.StartMoving)
            {
                SetAway(cm, awayX, awayY);
                if (panic && !cm.Panic)
                {
                    cm.Panic = true;
                    _courageStats.PanicWalks++;
                    AskSprintBack(st, cm, now, in cr, panic: true, dist: lookDistance, foes: foes);
                }
            }

            // ---- the behaviour weights: on (re-applied every look - the game resets them on orders) while timid, the game's when brave
            if (cm.Timid && cr.TightenWeights) TightenWeights(st, cm, in cr);
            else if (cm.WeightsSet) RestoreWeights(st, cm);

            // ---- step 67: the fear flock - courage's walk back goes first (it steers while it runs), else the flock may steer
            FearLook(st, now, in near, cm.Phase == CouragePhase.Moving ? FearCourage.Walking : FearCourage.Free);
        }

        // ------------------------------------------------------------------ the walk

        /// <summary>The walk's target: the nearest bigger group of his side (FLOCKING - his allies within CourageFlockRadiusMetres when at
        /// least CourageFlockMinMen are there), else his formation's centre of mass.</summary>
        private void PickTarget(TrackedAgent st, CourageMind cm, in CourageRules cr, bool hasCentre, double cx, double cy)
        {
            cm.FlockTarget = false;
            if (cr.FlockRadiusMetres > 0)
            {
                int n = CourageBody.Flock(st, (float)cr.FlockRadiusMetres, out double fx, out double fy);
                if (n >= cr.FlockMinMen && !double.IsNaN(fx))
                {
                    cm.CentreX = fx;
                    cm.CentreY = fy;
                    cm.FlockTarget = true;
                    return;
                }
            }
            if (hasCentre)
            {
                cm.CentreX = cx;
                cm.CentreY = cy;
            }
        }

        private static void SetAway(CourageMind cm, double awayX, double awayY)
        {
            double len = Math.Sqrt(awayX * awayX + awayY * awayY);
            if (len > 1e-9)
            {
                cm.AwayX = awayX / len;
                cm.AwayY = awayY / len;
            }
            else
            {
                cm.AwayX = cm.AwayY = 0;
            }
        }

        private void BeginWalk(TrackedAgent st, CourageMind cm, double now, in CourageRules cr, bool hasCentre, double cx, double cy, double dist, double px, double py,
            double awayX, double awayY, int foes, bool panic, double odds, double required, in CourageRequired parts, int mates, double self, double allies, double enemies,
            int shockEvents)
        {
            PickTarget(st, cm, in cr, hasCentre, cx, cy);
            if (!cm.FlockTarget && !hasCentre)
            {
                cm.StopMoving(RegroupEnd.Arrived, now); // nowhere to go: he holds
                return;
            }
            if (cm.FlockTarget) _courageStats.WalksToGroup++;
            else _courageStats.WalksToCentre++;
            SetAway(cm, awayX, awayY);
            cm.DistanceAtStart = dist;
            cm.SprintAsked = false;
            cm.Panic = panic;
            if (panic) _courageStats.PanicWalks++;
            cm.NextEdgeCheck = now + AiInputMath.EdgeCheckSeconds;
            EnsureInput(st);
            AiInputHook.HookResult hook;
            try
            {
                hook = CourageBody.Hook(st);
            }
            catch (Exception e)
            {
                Failed("courage.hook", e);
                cm.StopMoving(RegroupEnd.Ended, now); // not hooked = no walk (he holds)
                return;
            }
            NoteHooked(st, hook);
            AiInputHook.SetRegroup(st, true, now);
            _walking.Add(st);
            double targetDist = Math.Sqrt((cm.CentreX - px) * (cm.CentreX - px) + (cm.CentreY - py) * (cm.CentreY - py));
            Steer(st, cm, px, py);
            if (CourageMath.SprintWanted(in cr, targetDist, foes, panic)) AskSprintBack(st, cm, now, in cr, panic, targetDist, foes);
            if (!_firstTimidLogged)
            {
                _firstTimidLogged = true;
                _firstWalker = st;
                TraxLog.Info("courage", "first walk back this battle: " + Name(st) + " at " + Sec(now) + " s - his side " + F2(allies) + " (himself " + F2(self) + " + " + mates
                    + " mates) vs the enemy " + F2(enemies) + " (" + foes + " men) within " + F1(cr.RadiusMetres) + " m = odds " + F2(odds)
                    + " < required " + F2(required) + " (base " + F2(parts.Base) + " x tired " + F2(parts.Tired) + " x morale " + F2(parts.Morale) + " x shock " + F2(parts.Shock)
                    + " (" + shockEvents + " hits in " + F1(cr.ShockSeconds) + " s) x fever " + F2(parts.Fever) + " x hero " + F2(parts.Hero) + "); "
                    + F1(dist) + " m from his formation's centre, walking to " + (cm.FlockTarget ? "the nearest bigger group of his side" : "his formation's centre")
                    + " " + F1(targetDist) + " m away" + (panic ? " - in PANIC" : string.Empty) + (st.Sprint.Active && st.Sprint.Regroup ? ", sprinting " + F2(st.Sprint.Length) + " s" : string.Empty));
            }
            else if (TraxLog.VerboseWants("courage"))
            {
                TraxLog.Verbose("courage", Name(st) + " walks back at " + Sec(now) + " s (odds " + F2(odds) + " < " + F2(required) + ", " + F1(targetDist) + " m to "
                    + (cm.FlockTarget ? "a bigger group" : "the centre") + (panic ? ", PANIC" : string.Empty) + ")", "courage");
            }
            if (panic && !_firstPanicLogged)
            {
                _firstPanicLogged = true;
                TraxLog.Info("courage", "first PANIC this battle: " + Name(st) + " at " + Sec(now) + " s - shocked (" + shockEvents + " hits in " + F1(cr.ShockSeconds) + " s), odds "
                    + F2(odds) + " below " + cr.PanicOddsPercent + "% of the required " + F2(required) + "; "
                    + (st.Sprint.Active && st.Sprint.Regroup ? "sprints " + F2(st.Sprint.Length) + " s, keeping only " + cr.PanicSprintKeepPercent + "% of his store" : "no sprint (" + (cr.SprintBack ? "nothing to spend or the sprint off" : "CourageSprintBack off") + ")"));
            }
        }

        /// <summary>The regroup sprint: what he holds above CourageSprintKeepPercent (a panic: CouragePanicSprintKeepPercent) - once a walk
        /// (and once more when a panic starts on the way).</summary>
        private void AskSprintBack(TrackedAgent st, CourageMind cm, double now, in CourageRules cr, bool panic, double dist, int foes)
        {
            if (cm.SprintAsked && !panic) return;
            cm.SprintAsked = true;
            if (!cr.SprintBack) return;
            _courageStats.SprintsAsked++;
            var s = SprintRules.From(TraxSettings.Shared);
            if (!s.Enabled || !s.AiOn)
            {
                _courageStats.AddRefusal(CourageSprintRefusal.Off);
                return;
            }
            var sp = st.Sprint;
            if (sp.Active)
            {
                _courageStats.AddRefusal(CourageSprintRefusal.Sprinting);
                return;
            }
            var ar = AthleticsRules.From(TraxSettings.Shared);
            double cap = SprintMath.Capacity(in s, in ar, st);
            if (!(cap > SprintMath.EmptySeconds))
            {
                _courageStats.AddRefusal(CourageSprintRefusal.NoStore);
                return;
            }
            sp.Clamp(cap);
            double amount = CourageMath.SprintAmount(in cr, sp.StoredUpTo(cap), cap, panic);
            if (sp.TryStartRegroup(in s, now, cap, amount) != SprintStart.Started)
            {
                _courageStats.AddRefusal(CourageSprintRefusal.ReserveKept);
                return;
            }
            _courageStats.SprintsStarted++;
            _courageStats.SprintSeconds += sp.Length;
            if (panic) _courageStats.PanicSprints++;
            BeginSprint(st, now, in s, in ar, cap);
        }

        /// <summary>Every tick for the walkers: arrived on the way (within the regroup radius of the target), the ground ahead, the vector.</summary>
        private void SteerWalkers(double now, in CourageRules cr)
        {
            var body = CourageBody;
            for (int i = _walking.Count - 1; i >= 0; i--)
            {
                if (i >= _walking.Count) continue;
                var st = _walking[i];
                var cm = st.Courage;
                try
                {
                    if (st.Removed || cm.Phase != CouragePhase.Moving || !body.IsActive(st))
                    {
                        _walking.RemoveAt(i);
                        continue;
                    }
                    body.Position(st, out double px, out double py);
                    double d = Math.Sqrt((cm.CentreX - px) * (cm.CentreX - px) + (cm.CentreY - py) * (cm.CentreY - py));
                    bool shocked = cm.ShockStart;
                    if (CourageMath.Arrived(in cr, d, shocked))
                    {
                        cm.StopMoving(RegroupEnd.Arrived, now);
                        WalkEnded(st, cm, now, FormationDistance(st, px, py));
                        continue;
                    }
                    if (now >= cm.NextEdgeCheck)
                    {
                        cm.NextEdgeCheck = now + AiInputMath.EdgeCheckSeconds;
                        if (!body.GroundOk(st, cm.DirX, cm.DirY))
                        {
                            cm.StopMoving(RegroupEnd.EdgeAhead, now);
                            WalkEnded(st, cm, now, FormationDistance(st, px, py));
                            continue;
                        }
                    }
                    Steer(st, cm, px, py);
                    _courageStats.SteerFrames++;
                }
                catch (Exception e)
                {
                    Failed("courage.steer", e);
                    SafeEndTimid(st, now);
                }
            }
        }

        private double FormationDistance(TrackedAgent st, double px, double py) =>
            CourageBody.FormationCentre(st, out double cx, out double cy) ? Math.Sqrt((cx - px) * (cx - px) + (cy - py) * (cy - py)) : double.NaN;

        /// <summary>This frame's vector: toward the target + a little away from the enemies (round them when they stand between).</summary>
        private void Steer(TrackedAgent st, CourageMind cm, double px, double py)
        {
            if (!CourageMath.Direction(cm.CentreX - px, cm.CentreY - py, cm.AwayX, cm.AwayY, out double dx, out double dy)) return;
            cm.DirX = dx;
            cm.DirY = dy;
            var s = st.Input;
            if (s == null) return;
            if (CourageBody.ToLocal(st, dx, dy, out float lx, out float ly))
            {
                s.RegroupX = lx;
                s.RegroupY = ly;
            }
        }

        /// <summary>A walk ended (arrived, among mates, the edge, too long, brave, his spell ended): counted with its compression, the input
        /// wish off, a regroup sprint ended.</summary>
        private void WalkEnded(TrackedAgent st, CourageMind cm, double now, double distNow)
        {
            var why = cm.LastRegroupEnd;
            _courageStats.AddRegroupEnd(why, cm.LastMovingSeconds);
            _courageStats.AddCompression(cm.ShockStart, cm.DistanceAtStart, distNow);
            _walking.Remove(st);
            StopWalkInput(st, now, native: true);
            if (ReferenceEquals(st, _firstWalker) && !_firstWalkEndLogged)
            {
                _firstWalkEndLogged = true;
                TraxLog.Info("courage", "first walk back ended at " + Sec(now) + " s after " + F1(cm.LastMovingSeconds) + " s - " + CourageMath.RegroupEndName(why)
                    + "; " + F1(cm.DistanceAtStart) + " → " + F1(distNow) + " m from his formation's centre; " + (cm.Timid ? "still timid - holds" : "brave again")
                    + (st.Input != null ? "; walked " + st.Input.RegroupFrames + " frames" : string.Empty));
            }
        }

        private void StopWalkInput(TrackedAgent st, double now, bool native)
        {
            if (st.Input != null && st.Input.Regroup)
            {
                AiInputHook.SetRegroup(st, false, now);
                if (native && !st.Removed)
                {
                    try
                    {
                        CourageBody.Release(st);
                    }
                    catch (Exception e)
                    {
                        Failed("courage.release", e);
                    }
                }
                DrainInputError(st);
            }
            if (st.Sprint.Active && st.Sprint.Regroup) EndSprint(st, native ? SprintEnd.Regrouped : SprintEnd.LeftField, now);
        }

        // ------------------------------------------------------------------ the weights

        private void TightenWeights(TrackedAgent st, CourageMind cm, in CourageRules cr)
        {
            try
            {
                CourageBody.Tighten(st, cr.TimidMeleeMetres);
                if (cm.WeightsSet) _courageStats.WeightsReapplied++;
                else _courageStats.WeightsSet++;
                cm.WeightsSet = true;
            }
            catch (Exception e)
            {
                _courageStats.WeightErrors++;
                Failed("courage.weights", e);
            }
        }

        private void RestoreWeights(TrackedAgent st, CourageMind cm)
        {
            cm.WeightsSet = false;
            try
            {
                CourageBody.Restore(st);
                _courageStats.WeightsRestored++;
            }
            catch (Exception e)
            {
                _courageStats.WeightErrors++;
                Failed("courage.restore", e);
            }
        }

        // ------------------------------------------------------------------ the end

        /// <summary>His timid spell ends (any path): the walk stopped, the game's weights back (<paramref name="native"/>), counted.</summary>
        private void EndTimid(TrackedAgent st, CourageEnd why, double now, bool native)
        {
            var cm = st.Courage;
            if (!cm.Timid) return;
            bool wasMoving = cm.Phase == CouragePhase.Moving;
            int wobblesBefore = cm.Wobbles;
            cm.End(now);
            _courageStats.Wobbles += cm.Wobbles - wobblesBefore;
            if (wasMoving) WalkEndedQuiet(st, cm, now, native);
            EndTimidCommon(st, why, now, native);
        }

        private void WalkEndedQuiet(TrackedAgent st, CourageMind cm, double now, bool native)
        {
            _courageStats.AddRegroupEnd(cm.LastRegroupEnd, cm.LastMovingSeconds);
            _walking.Remove(st);
            StopWalkInput(st, now, native);
        }

        private void EndTimidCommon(TrackedAgent st, CourageEnd why, double now, bool native)
        {
            if (_timidNow > 0) _timidNow--;
            _courageStats.AddEnd(why);
            var cm = st.Courage;
            cm.Panic = false;
            if (cm.WeightsSet)
            {
                if (native && !st.Removed) RestoreWeights(st, cm);
                else cm.WeightsSet = false;
            }
            if (TraxLog.VerboseWants("courage"))
                TraxLog.Verbose("courage", Name(st) + " " + CourageMath.EndName(why) + " at " + Sec(now) + " s", "courage");
        }

        private void SafeEndTimid(TrackedAgent st, double now)
        {
            try
            {
                if (st.Courage.Timid) EndTimid(st, CourageEnd.Error, now, native: true);
            }
            catch
            {
                st.Courage.End(now);
                _walking.Remove(st);
                if (st.Input != null) st.Input.Regroup = false;
            }
        }

        private void EndAllTimid(CourageEnd why, double now, bool native, string? because)
        {
            int ended = 0;
            for (int i = 0; i < _count; i++)
            {
                var st = _dense[i];
                if (!st.Courage.Timid) continue;
                try
                {
                    EndTimid(st, why, now, native);
                    ended++;
                }
                catch (Exception e)
                {
                    Failed("courage.end-all", e);
                    SafeEndTimid(st, now);
                }
            }
            _timidNow = 0;
            _walking.Clear();
            if (why == CourageEnd.SwitchedOff && ended > 0)
                TraxLog.Info("courage", (because == "ModEnabled" ? "the whole mod (ModEnabled)" : because ?? "courage") + " switched OFF mid-mission at " + Sec(now) + " s: "
                    + ended + " timid men let go at once - the game's own bravery and behaviour weights; the looks keep measuring");
        }

        /// <summary>He left the field: his spell ends without an engine call.</summary>
        private void CourageLeftField(TrackedAgent st)
        {
            try
            {
                if (st.Courage.Timid) EndTimid(st, CourageEnd.LeftField, SafeNow(), native: false);
            }
            catch (Exception e)
            {
                Failed("courage.left", e);
            }
            FearLeftField(st); // step 67
        }

        /// <summary>A landed hit on him (OnAgentHit): his shock window.</summary>
        private void CourageHitTaken(Agent? victim, double now)
        {
            if (TraxCombat.Models.ImpactHooks.InOwnBlow) return; // step 51: our own knockdown blow on a blocker - its knockdown is ONE shock already (ImpactState)
            var st = Get(victim);
            if (st != null) st.Hits.Note(now);
        }

        /// <summary>The battle's end: every timid man let go (the weights back while the agents live) before the summary counts.</summary>
        private void CloseCourage(bool agentsAlive)
        {
            if (_courageClosed) return;
            _courageClosed = true;
            if (_timidNow > 0) EndAllTimid(CourageEnd.MissionEnd, SafeNow(), agentsAlive, null);
            CloseFear(agentsAlive); // step 67
        }

        internal void WriteCourageSummary()
        {
            var lines = new List<string>(11);
            _courageStats.AddSummaryLines(lines, CourageRules.From(TraxSettings.Shared));
            foreach (var line in lines) TraxLog.Info("summary", line);
            WriteFearSummary(); // step 67
        }
    }
}
