using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 23 - BRACE BY ORDERS (Anton, 2026-09-29: "make soldiers not swing but only defend when they have reached a certain
    /// floor, that depends on their current orders, until they have replenished the floor +20%" ... "when defending they whip
    /// out their shields if they have one"; AI_NOTES "Step 23", DESIGN §2 "Brace by orders"). Every AI man is looked at about
    /// every <see cref="BraceMath.PollSeconds"/>, staggered over the ticks: his bar (points ÷ his FULL pool - what the bar shows)
    /// against his formation's order floor (<see cref="BraceMath.Band"/>, his own margin rolled once per battle). At or below
    /// it he BRACES - step 16's input component takes his melee attack bits out (guard up) until he refills to floor + his
    /// margin (capped by his wounds). The brace is its own wish in <see cref="AiInputState"/>: the AI timer's and the step
    /// back's ends never lift it, its end never lifts theirs. Ranged attacks go on. The shield is taken out if he carries one
    /// (<see cref="IBraceBody.Wield"/>, re-checked every <see cref="BraceMath.ShieldCheckSeconds"/>) and held up while he has
    /// no guard of his own. Never the player; the master switch, AthleticsEnabled and BraceEnabled lift every brace at once.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly BraceStats _braceStats = new BraceStats();
        private readonly List<TrackedAgent> _bracing = new List<TrackedAgent>(64);
        private BraceRules _seenBrace;
        private int _braceCursor;
        private double _braceLastTick = double.NaN;
        private double _braceCarry;
        private bool _firstBraceLogged;
        private bool _braceClosed;

        /// <summary>This mission's brace numbers (the offline smoke reads them).</summary>
        internal BraceStats BraceStats => _braceStats;

        /// <summary>The engine side of the brace (the offline smoke swaps in a stand-in).</summary>
        internal IBraceBody BraceBody { get; set; } = BraceBodyDefault.Current;

        /// <summary>The dice for each man's margin roll (seeded in the offline smoke).</summary>
        internal IRandomSource BraceDice { get; set; } = ThreadSafeRandom.Shared;

        /// <summary>Men bracing right now.</summary>
        internal int BracingNow => _bracing.Count;

        /// <summary>Mission start: the settings line, read live from now on.</summary>
        private void StartBrace()
        {
            _seenBrace = BraceRules.From(TraxSettings.Shared);
            TraxLog.Info("brace", "mission start: " + _seenBrace.Describe() + " - read live, every AI man looked at about every "
                + F2(BraceMath.PollSeconds) + " s; the [summary] \"brace\" lines say who braced, under which order, how long and why it ended");
        }

        /// <summary>A brace setting changed (from ApplySettingsChange): one line; the floors apply at every man's next look.</summary>
        private void NoteBraceSettings(TraxSettings s)
        {
            var now = BraceRules.From(s);
            if (now.SameAs(in _seenBrace)) return;
            _seenBrace = now;
            _braceStats.SettingsChanged = true;
            TraxLog.Info("brace", "settings changed mid-mission at " + Sec(SafeNow()) + " s: " + now.Describe()
                + " - the floors and margins apply at every man's next look (within " + F2(BraceMath.PollSeconds) + " s)");
        }

        /// <summary>
        /// Every tick: switched off → every brace lifted at once; else a slice of the fighters is looked at so that each is seen
        /// about every <see cref="BraceMath.PollSeconds"/> (a call after a longer gap - or the first - looks at everyone once).
        /// No allocation.
        /// </summary>
        internal void TickBrace(double now)
        {
            var settings = TraxSettings.Shared;
            var br = BraceRules.From(settings);
            if (!br.Enabled)
            {
                if (_bracing.Count > 0) EndAllBraces(BraceEnd.SwitchedOff, now, native: true, br.OffBecause);
                _braceLastTick = now;
                return;
            }
            var ar = AthleticsRules.From(settings);
            double dt = double.IsNaN(_braceLastTick) || now < _braceLastTick ? BraceMath.PollSeconds : now - _braceLastTick;
            _braceLastTick = now;
            if (dt > BraceMath.PollSeconds) dt = BraceMath.PollSeconds;
            _braceCarry += _count * dt / BraceMath.PollSeconds;
            int n = (int)(_braceCarry + 1e-9);
            _braceCarry -= n;
            if (_braceCarry < 0) _braceCarry = 0;
            if (n > _count) n = _count;
            for (int k = 0; k < n; k++)
            {
                if (_braceCursor >= _count) _braceCursor = 0;
                var st = _dense[_braceCursor++];
                try
                {
                    PollBrace(st, now, in br, in ar);
                }
                catch (Exception e)
                {
                    Failed("brace.poll", e);
                    var bs = st.Brace;
                    if (bs != null && bs.Active) SafeEndBrace(st, bs, now);
                }
            }
        }

        /// <summary>One man's look: not AI → none (a running brace ends); his order, his band, enter / leave / keep.</summary>
        private void PollBrace(TrackedAgent st, double now, in BraceRules br, in AthleticsRules ar)
        {
            if (st.Removed) return;
            var body = BraceBody;
            if (!body.IsActive(st)) return;
            var bs = st.Brace;
            if (IsPlayer(st) || !body.IsAiControlled(st))
            {
                if (bs != null && bs.Active) EndBrace(st, bs, BraceEnd.PlayerControl, now, native: true);
                return;
            }
            if (bs == null)
            {
                bs = st.Brace = new BraceState();
                bs.Unit = BraceMath.RollUnit(BraceDice); // once per man and battle (Anton's "bravery")
                _braceStats.AddMan(bs.Unit);
            }
            else if (!double.IsNaN(bs.LastPoll))
            {
                double gap = now - bs.LastPoll;
                if (gap > 0 && gap <= 4 * BraceMath.PollSeconds) _braceStats.AiManSeconds += gap; // a longer gap = the feature was off
            }
            bs.LastPoll = now;
            var kind = body.ReadOrder(st);
            var group = BraceMath.Group(kind);
            var band = BraceMath.Band(in br, group, AthleticsMath.UsableFraction(in ar, st), bs.Unit);
            double x = st.Fraction;
            if (!bs.Active)
            {
                if (BraceMath.Enter(in band, x)) BeginBrace(st, bs, kind, group, in band, x, now, in br);
                return;
            }
            bs.KindNow = kind;
            if (BraceMath.Leave(in band, x))
            {
                EndBrace(st, bs, BraceMath.LeaveReason(bs.GroupAtStart, group, in band), now, native: true);
                return;
            }
            KeepBracing(st, bs, now, in br);
        }

        private void BeginBrace(TrackedAgent st, BraceState bs, BraceOrderKind kind, BraceOrder group, in BraceBand band, double x, double now, in BraceRules br)
        {
            EnsureInput(st);
            var s = st.Input!;
            AiInputHook.HookResult hook;
            try
            {
                hook = BraceBody.Hook(st);
            }
            catch (Exception e)
            {
                Failed("brace.hook", e);
                return; // not hooked = not braced (tried again at his next look)
            }
            NoteHooked(st, hook);
            AiInputHook.SetBrace(st, true, now);
            bs.Active = true;
            bs.Since = now;
            bs.KindAtStart = bs.KindNow = kind;
            bs.GroupAtStart = group;
            bs.FractionAtStart = x;
            bs.BandAtStart = band;
            bs.CallsAtStart = s.Calls;
            bs.HitsTaken = bs.HitsBlocked = 0;
            bool firstForHim = !bs.EverBraced;
            bs.EverBraced = true;
            _braceStats.AddStart(kind, firstForHim);
            _bracing.Add(st);

            var h = BraceBody.ReadHands(st);
            s.BraceRanged = h.MainRanged;
            s.BraceShieldInHand = h.ShieldInOffHand;
            bs.ShieldAsked = bs.ShieldSeen = bs.SwitchedBackCounted = false;
            bs.WieldCalls = 0;
            bs.StepAtStart = BraceMath.ShieldStepFor(in h);
            bs.NextShieldCheck = now + BraceMath.ShieldCheckSeconds;
            if (br.WieldShield)
            {
                _braceStats.AddShieldAtStart(bs.StepAtStart);
                if (bs.StepAtStart == ShieldStep.WieldShield || bs.StepAtStart == ShieldStep.WieldOneHanded) DoWield(st, bs, in h, bs.StepAtStart);
            }
            else
            {
                _braceStats.ShieldOffSwitch++;
            }

            bs.First = !_firstBraceLogged;
            _firstBraceLogged = true;
            if (bs.First) TraxLog.Info("brace", "first brace this mission: " + BraceText(st, bs, in band, x, now, in br) + "; " + HookText(hook));
            else if (TraxLog.VerboseWants("brace")) TraxLog.Verbose("brace", "brace: " + BraceText(st, bs, in band, x, now, in br), "brace");
        }

        /// <summary>While bracing: his hands read (ranged in hand, the shield in hand - the hook's frame uses both), the shield
        /// seen / put away again, and every <see cref="BraceMath.ShieldCheckSeconds"/> a wield when it is still needed.</summary>
        private void KeepBracing(TrackedAgent st, BraceState bs, double now, in BraceRules br)
        {
            var s = st.Input;
            if (s == null) return;
            var h = BraceBody.ReadHands(st);
            s.BraceRanged = h.MainRanged;
            s.BraceShieldInHand = h.ShieldInOffHand;
            if (h.ShieldInOffHand)
            {
                if (bs.ShieldAsked && !bs.ShieldSeen)
                {
                    bs.ShieldSeen = true;
                    if (!bs.SwitchedBackCounted) _braceStats.ShieldHeld++;
                }
            }
            else if (bs.ShieldSeen)
            {
                bs.ShieldSeen = false;
                if (!bs.SwitchedBackCounted)
                {
                    bs.SwitchedBackCounted = true;
                    _braceStats.SwitchedBack++;
                }
            }
            if (!br.WieldShield || now < bs.NextShieldCheck) return;
            bs.NextShieldCheck = now + BraceMath.ShieldCheckSeconds;
            if (bs.WieldCalls >= BraceMath.MaxWieldCalls) return;
            var step = BraceMath.ShieldStepFor(in h);
            if (step == ShieldStep.WieldShield || step == ShieldStep.WieldOneHanded) DoWield(st, bs, in h, step);
        }

        private void DoWield(TrackedAgent st, BraceState bs, in HandFacts h, ShieldStep step)
        {
            bool oneHander = step == ShieldStep.WieldOneHanded;
            int slot = oneHander ? h.OneHandedSlot : h.ShieldSlot;
            if (slot < 0) return;
            bs.ShieldAsked = true;
            bs.WieldCalls++;
            try
            {
                BraceBody.Wield(st, slot);
                if (oneHander) _braceStats.OneHandedCalls++;
                else _braceStats.ShieldCalls++;
            }
            catch (Exception e)
            {
                _braceStats.WieldErrors++;
                Failed("brace.wield", e);
            }
        }

        /// <summary>Ends a running brace: the wish off (managed - the callback writes nothing of ours from the next frame),
        /// counted, and with <paramref name="native"/> the callback flag off if he is idle and it was ours.</summary>
        private void EndBrace(TrackedAgent st, BraceState bs, BraceEnd why, double now, bool native)
        {
            bs.Active = false;
            double held = now - bs.Since;
            if (!(held >= 0)) held = 0;
            AiInputHook.SetBrace(st, false, now);
            var s = st.Input;
            if (s != null && s.Calls == bs.CallsAtStart && held >= NoCallGraceSeconds && why != BraceEnd.LeftField) _braceStats.BracesWithoutACall++;
            if (bs.ShieldAsked && !bs.ShieldSeen && !bs.SwitchedBackCounted) _braceStats.NeverGotIt++;
            _braceStats.AddEnd(why, held, bs.Unit, held > _braceStats.LongestSeconds ? Name(st) : null);
            _bracing.Remove(st);
            if (native && !st.Removed)
            {
                try
                {
                    BraceBody.Release(st);
                }
                catch (Exception e)
                {
                    Failed("brace.release", e);
                }
            }
            if (s != null) DrainInputError(st);
            if (bs.First)
                TraxLog.Info("brace", "first brace ended at " + Sec(now) + " s after " + F1(held) + " s - " + BraceMath.Name(why) + "; bar now "
                    + Pct(st.Fraction) + ", order now " + BraceMath.Name(bs.KindNow) + "; melee hits taken " + bs.HitsTaken + ", blocked " + bs.HitsBlocked
                    + (s != null ? "; his attacks taken out " + s.BraceCleared + " frames, the shield held up " + s.BraceShieldRaised + " frames, engine calls " + s.Calls : string.Empty));
            else if (TraxLog.VerboseWants("brace"))
                TraxLog.Verbose("brace", "brace ended: " + Name(st) + " after " + F1(held) + " s - " + BraceMath.Name(why) + ", bar " + Pct(st.Fraction), "brace");
        }

        private void SafeEndBrace(TrackedAgent st, BraceState bs, double now)
        {
            try
            {
                EndBrace(st, bs, BraceEnd.Error, now, native: true);
            }
            catch (Exception e)
            {
                Failed("brace.end", e);
                bs.Active = false;
                _bracing.Remove(st);
                try
                {
                    if (st.Input != null) AiInputHook.SetBrace(st, false, now);
                }
                catch
                {
                    // the record is gone; his next brace rewrites the wish
                }
            }
        }

        /// <summary>Every brace at once (switched off, the battle's end).</summary>
        private void EndAllBraces(BraceEnd why, double now, bool native, string? offBecause)
        {
            int n = _bracing.Count;
            for (int i = _bracing.Count - 1; i >= 0; i--)
            {
                if (i >= _bracing.Count) continue;
                var st = _bracing[i];
                var bs = st.Brace;
                if (bs == null || !bs.Active)
                {
                    _bracing.RemoveAt(i);
                    continue;
                }
                try
                {
                    EndBrace(st, bs, why, now, native);
                }
                catch (Exception e)
                {
                    Failed("brace.end-all", e);
                    SafeEndBrace(st, bs, now);
                }
            }
            _bracing.Clear();
            if (why == BraceEnd.SwitchedOff)
                TraxLog.Info("brace", (offBecause == "ModEnabled" ? "the whole mod (ModEnabled)" : offBecause ?? "the brace") + " switched OFF mid-mission at "
                    + Sec(now) + " s: " + n + " braces lifted at once - those men attack again");
        }

        /// <summary>He left the field: his brace ends without an engine call.</summary>
        private void BraceLeftField(TrackedAgent st)
        {
            var bs = st.Brace;
            if (bs != null && bs.Active) EndBrace(st, bs, BraceEnd.LeftField, SafeNow(), native: false);
        }

        /// <summary>Step 19's fresh start ends his brace (he is full to his cap now anyway).</summary>
        private void BraceFreshStart(TrackedAgent st, double now)
        {
            var bs = st.Brace;
            if (bs == null || !bs.Active) return;
            try
            {
                EndBrace(st, bs, BraceEnd.FreshStart, now, native: true);
            }
            catch (Exception e)
            {
                Failed("hideout.brace", e);
                SafeEndBrace(st, bs, now);
            }
        }

        /// <summary>The battle's end: every brace lifted (through the engine while the agents live) before the summary counts.</summary>
        private void CloseBraces(bool agentsAlive)
        {
            if (_braceClosed) return;
            _braceClosed = true;
            if (_bracing.Count > 0) EndAllBraces(BraceEnd.MissionEnd, SafeNow(), agentsAlive, null);
        }

        /// <summary>A melee collision on a bracing man (the summary's brace GUARD line) - blocked by shield or weapon, or landed.</summary>
        private void BraceHitTaken(Agent? victim, in AttackCollisionData cd)
        {
            if (victim == null || cd.IsHorseCharge) return;
            var st = Get(victim);
            var bs = st?.Brace;
            if (bs == null || !bs.Active) return;
            var result = cd.CollisionResult;
            bool blocked = cd.AttackBlockedWithShield || result == CombatCollisionResult.Blocked || result == CombatCollisionResult.Parried
                           || result == CombatCollisionResult.ChamberBlocked;
            _braceStats.AddHit(blocked);
            bs.HitsTaken++;
            if (blocked) bs.HitsBlocked++;
        }

        /// <summary>A release by a bracing man: melee should not happen (counted), ranged is allowed (counted).</summary>
        private void NoteBraceRelease(TrackedAgent st, bool melee)
        {
            var bs = st.Brace;
            if (bs == null || !bs.Active) return;
            if (melee) _braceStats.MeleeWhileBracing++;
            else _braceStats.RangedWhileBracing++;
        }

        /// <summary>The summary's brace lines: the hook's per-man brace counts summed first (own try in the caller).</summary>
        private void WriteBraceSummary()
        {
            foreach (var st in _inputMen)
            {
                var s = st.Input;
                if (s == null) continue;
                _braceStats.FramesCleared += s.BraceCleared;
                _braceStats.GuardsRaised += s.BraceGuardRaised;
                _braceStats.ShieldRaisedFrames += s.BraceShieldRaised;
                _braceStats.RangedFramesPassed += s.BraceRangedPassed;
            }
            foreach (var line in _braceStats.SummaryLines(BraceRules.From(TraxSettings.Shared)))
                TraxLog.Info("summary", line);
        }

        /// <summary>A brace in words: who, the order and its floor, the bar, his margin, the target, the shield.</summary>
        private string BraceText(TrackedAgent st, BraceState bs, in BraceBand band, double x, double now, in BraceRules br)
        {
            double margin = BraceMath.Margin(in br, bs.Unit);
            string shield = !br.WieldShield ? "the shield left as it is (BraceWieldShield off)"
                : "shield: " + BraceMath.Name(bs.StepAtStart) + (bs.WieldCalls > 0 ? " (wield asked)" : string.Empty);
            return Name(st) + " at " + Sec(now) + " s - order " + BraceMath.Name(bs.KindAtStart) + " (the " + BraceMath.Name(bs.GroupAtStart) + " floor "
                   + br.FloorPercent(bs.GroupAtStart) + "%), his bar " + Pct(x) + " of his full pool (" + F1(AthleticsMath.Points(AthleticsRules.From(TraxSettings.Shared), st))
                   + " points) → no melee attacks, guard up, until " + Pct(band.Target) + " = floor " + Pct(band.SetFloor) + " + his margin " + F1(margin * 100)
                   + " points (BraceRecoverPercent " + br.RecoverPercent + " " + (BraceMath.Offset(in br, bs.Unit) >= 0 ? "+ " : "- ")
                   + F1(Math.Abs(BraceMath.Offset(in br, bs.Unit)) * 100) + ", his own roll)"
                   + (band.Capped ? " - capped by his wounds (he can refill to " + Pct(band.Target) + " only; he braces from " + Pct(band.Floor) + ")" : string.Empty)
                   + "; " + shield;
        }

        private static string Pct(double fraction) => (fraction * 100).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%";
    }
}
