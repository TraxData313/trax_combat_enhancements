using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 73 - what the line discipline reads from and does to the game (<see cref="GameLineWorld"/>; the offline smoke's stand-in).
    /// Formations are SLOTS (team index × 8 + the formation's index); <see cref="BeginLook"/> refreshes the team list once a look.
    /// </summary>
    internal interface ILineWorld
    {
        /// <summary>The battle's kind (naval: never; sieges: LineDisciplineInSieges).</summary>
        LineMission Mission { get; }

        /// <summary>Refresh the teams; returns the slot count.</summary>
        int BeginLook();

        /// <summary>The formation in this slot; false = none.</summary>
        bool Read(int slot, ref LineFacts facts);

        /// <summary>Its men's ranks by the map: the sum, the men, and the men per tier (0-6) and heroes (index 7) into <paramref name="tiers"/>.</summary>
        void Ranks(int slot, in LineDisciplineRules rules, int[] tiers, out double sum, out int men);

        /// <summary>Narrow it: the game's own custom form order of this width (the game's line keeps it as men fall). False = the game's
        /// line did not take it (still wider - read back).</summary>
        bool Clamp(int slot, double width);

        /// <summary>The switch went off: the game's own form order back (the one our first clamp replaced) if ours still stands.</summary>
        bool GiveBack(int slot);

        /// <summary>The player's formation: carry the cap as its maximum width (the preview cap); NaN = take ours out. Returns what happened.</summary>
        PreviewChange SetPreview(int slot, double capWidth);

        /// <summary>The game asked the capped arrangement for its maximum width and got the cap - running total over the battle.</summary>
        long PreviewCapReads { get; }

        /// <summary>"Infantry of the Vlandians" - for the log.</summary>
        string Name(int slot);

        /// <summary>A line on screen.</summary>
        void Show(string text);
    }

    /// <summary>Step 73: what <see cref="ILineWorld.SetPreview"/> did.</summary>
    internal enum PreviewChange
    {
        None,
        Installed,
        Reinstalled,
        Removed,
        Skipped,
    }

    /// <summary>
    /// Step 73 - LINE DISCIPLINE BY TIER (Toni 2026-10-20; AI_NOTES "Step 73", DESIGN decision 67). Every look (the logic's pre-tick - right
    /// after the game's agent and team tick, where the AI's behaviours set their form orders; before any man reads his slot): each formation
    /// that counts (a line, shield wall, loose or scatter arrangement; not cavalry / horse archers; ranged by the switch; the player's own by his
    /// switch) gets its minimum depth D - the mean of its men's map ranks, recounted every LineDisciplinePollSeconds and at once when its count
    /// changes - and its cap F = ceil(men ÷ D) files; a line wider than F files is narrowed to F at once (the game's custom form order), counted
    /// as WIDENED (it grew since the last look - an order, the AI) or AUTO-SHRINK (the cap fell - losses, a new D). The player's formations
    /// also carry the cap as their arrangement's maximum width, so the order drag's preview stops at it. The master switch FIRST, read live;
    /// off = every width we set given back, every capped arrangement taken out. Allocation-free per look (arrays grow only when a team appears).
    /// </summary>
    internal sealed class LineDiscipline
    {
        /// <summary>A clamp the game did not take is tried again after this long (plumbing).</summary>
        public const double NotTakenRetrySeconds = 1.0;

        private readonly ILineWorld _world;
        private readonly LineDisciplineStats _stats = new LineDisciplineStats();
        private readonly int[] _tiers = new int[LineDisciplineMath.MaxTier + 2];
        private LineFacts _facts;

        // per slot
        private double[] _depth = Array.Empty<double>();
        private int[] _depthMen = Array.Empty<int>();
        private double[] _nextPoll = Array.Empty<double>();
        private int[] _lastFiles = Array.Empty<int>();
        private bool[] _counted = Array.Empty<bool>();
        private bool[] _everCounted = Array.Empty<bool>();
        private double[] _retryAt = Array.Empty<double>();
        private bool[] _clamped = Array.Empty<bool>();
        private bool[] _preview = Array.Empty<bool>();
        private double[] _lastMessage = Array.Empty<double>();
        private LineRecord?[] _records = Array.Empty<LineRecord?>();

        private bool _started;
        private bool _wasOn;
        private bool _wasPlayerOn;
        private LineDisciplineRules _seen;
        private bool _firstCounted;
        private bool _firstClampAi;
        private bool _firstClampPlayer;
        private bool _firstShrink;
        private bool _firstPreview;
        private bool _firstPreviewRead;
        private bool _firstOrder;
        private bool _firstSkippedPreview;

        public LineDiscipline(ILineWorld world)
        {
            _world = world;
        }

        public LineDisciplineStats Stats => _stats;

        /// <summary>The mission starts: the rules in one sentence.</summary>
        public void Start()
        {
            var rules = LineDisciplineRules.From(TraxSettings.Shared);
            _started = true;
            _wasOn = rules.On;
            _wasPlayerOn = rules.PlayerOn;
            _seen = rules;
            string where = _world.Mission == LineMission.Naval ? " - a NAVAL battle: never here"
                : _world.Mission == LineMission.Siege && !rules.InSieges ? " - a SIEGE: not here (LineDisciplineInSieges off)" : "";
            TraxLog.Info("lines", "mission start: " + rules.Describe() + where);
        }

        /// <summary>One look: right after the game's own update (the logic's pre-tick).</summary>
        public void Tick(double now)
        {
            if (!_started) Start();
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var rules = LineDisciplineRules.From(TraxSettings.Shared);
                NoteRules(in rules, now);
                if (!rules.On) return; // the master switch FIRST (NoteRules gave everything back)
                if (_world.Mission == LineMission.Naval || (_world.Mission == LineMission.Siege && !rules.InSieges))
                {
                    if (AnyPreview()) RemoveAllPreviews();
                    return;
                }
                int slots = _world.BeginLook();
                Ensure(slots);
                for (int slot = 0; slot < slots; slot++)
                {
                    try
                    {
                        Look(slot, in rules, now);
                    }
                    catch (Exception e)
                    {
                        _stats.Errors++;
                        AthleticsLogic.Failed("lines.look", e);
                    }
                }
                NoteFirstPreviewRead(now);
            }
            catch (Exception e)
            {
                _stats.Errors++;
                AthleticsLogic.Failed("lines.tick", e);
            }
            finally
            {
                double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                _stats.Ticks++;
                _stats.TickMs += ms;
                if (ms > _stats.MaxTickMs) _stats.MaxTickMs = ms;
            }
        }

        private void Look(int slot, in LineDisciplineRules rules, double now)
        {
            _facts = default;
            if (!_world.Read(slot, ref _facts))
            {
                if (_preview[slot]) TakePreviewOut(slot);
                _counted[slot] = false;
                return;
            }
            var skip = LineDisciplineMath.Verdict(in rules, _world.Mission, in _facts);
            if (skip != LineSkip.None)
            {
                if (_preview[slot]) TakePreviewOut(slot);
                _counted[slot] = false;
                return;
            }
            var side = _stats.Side(_facts.PlayerSide);
            if (!_counted[slot])
            {
                _counted[slot] = true;
                _nextPoll[slot] = double.NegativeInfinity; // recount now
                _lastFiles[slot] = 0;
                if (!_everCounted[slot])
                {
                    _everCounted[slot] = true;
                    side.Counted++;
                }
            }

            int men = _facts.Men;
            if (men != _depthMen[slot] || now >= _nextPoll[slot])
            {
                Array.Clear(_tiers, 0, _tiers.Length);
                _world.Ranks(slot, in rules, _tiers, out double sum, out int counted);
                _depth[slot] = LineDisciplineMath.MinDepth(sum, counted);
                _depthMen[slot] = men;
                _nextPoll[slot] = now + rules.PollSeconds;
                if (!_firstCounted)
                {
                    _firstCounted = true;
                    TraxLog.Info("lines", "first formation counted at " + Sec(now) + " s: " + _world.Name(slot) + " - " + TierText(counted) + " → D "
                                          + LineDisciplineMath.F(_depth[slot]) + " → at most " + LineDisciplineMath.CapFiles(men, _depth[slot]) + " files ("
                                          + LineDisciplineMath.M(LineDisciplineMath.Width(LineDisciplineMath.CapFiles(men, _depth[slot]), _facts.Interval, _facts.Diameter))
                                          + " m at its spacing); it stands " + LineDisciplineMath.FilesOf(_facts.Width, _facts.Interval, _facts.Diameter) + " files ("
                                          + LineDisciplineMath.M(_facts.Width) + " m)");
                }
            }

            double depth = _depth[slot];
            int cap = LineDisciplineMath.CapFiles(men, depth);
            double capWidth = LineDisciplineMath.CapWidth(cap, _facts.Interval, _facts.Diameter);
            int files = LineDisciplineMath.FilesOf(_facts.Width, _facts.Interval, _facts.Diameter);

            if (files > cap && now < _retryAt[slot])
            {
                // the game did not take our last clamp (its line could not narrow yet - a transformation): again in a moment, not every frame
            }
            else if (files > cap)
            {
                var cause = files > _lastFiles[slot] ? LineClampCause.Widened : LineClampCause.AutoShrink;
                var r = Record(slot, side, men, depth, cap, capWidth);
                if (files > r.WidestAskedFiles)
                {
                    r.WidestAskedFiles = files;
                    r.WidestAskedWidth = _facts.Width;
                }
                bool taken = _world.Clamp(slot, capWidth);
                _clamped[slot] = true;
                if (!taken)
                {
                    _retryAt[slot] = now + NotTakenRetrySeconds;
                    _stats.ClampsNotTaken++;
                }
                r.Clamps++;
                if (cause == LineClampCause.AutoShrink) r.AutoShrinks++;
                if (_facts.PlayerOrdered)
                {
                    if (cause == LineClampCause.Widened) side.PlayerWidenClamps++;
                    else side.PlayerAutoShrinks++;
                }
                else
                {
                    if (cause == LineClampCause.Widened) side.AiWidenClamps++;
                    else side.AiAutoShrinks++;
                }
                FirstClampLine(slot, cause, now, men, depth, files, cap, capWidth);
                if (_facts.PlayerOrdered && cause == LineClampCause.Widened) Say(slot, in rules, now, depth, cap);
                if (taken) files = cap;
            }
            _lastFiles[slot] = files;
            var rec = _records[slot];
            if (rec != null)
            {
                rec.LastMen = men;
                rec.LastDepth = depth;
                rec.LastCapFiles = cap;
            }

            // the preview cap: the player's own formations only
            if (_facts.PlayerOrdered && rules.PlayerOn && rules.PreviewCap)
            {
                var change = _world.SetPreview(slot, cap < men ? capWidth : double.NaN);
                NotePreview(slot, change, now, cap, capWidth);
            }
            else if (_preview[slot])
            {
                TakePreviewOut(slot);
            }
        }

        /// <summary>
        /// The player's line order (a click or a drag - the game's MoveToLineSegment) went out for this formation: <paramref name="askedWidth"/>
        /// = its share of the dragged line, <paramref name="widthNow"/> = what the order left it at. Counted, and said on screen when wider.
        /// </summary>
        public void NotePlayerLineOrder(int slot, double askedWidth, double now)
        {
            try
            {
                var rules = LineDisciplineRules.From(TraxSettings.Shared);
                if (!rules.PlayerOn) return; // the master switch FIRST
                Ensure(_world.BeginLook());
                if (slot < 0 || slot >= _counted.Length) return;
                _facts = default;
                if (!_world.Read(slot, ref _facts)) return;
                if (LineDisciplineMath.Verdict(in rules, _world.Mission, in _facts) != LineSkip.None || !_facts.PlayerOrdered) return;
                if (_depthMen[slot] != _facts.Men || _depth[slot] <= 0)
                {
                    Array.Clear(_tiers, 0, _tiers.Length);
                    _world.Ranks(slot, in rules, _tiers, out double sum, out int counted);
                    _depth[slot] = LineDisciplineMath.MinDepth(sum, counted);
                    _depthMen[slot] = _facts.Men;
                    _nextPoll[slot] = now + rules.PollSeconds;
                }
                _stats.PlayerLineOrders++;
                double depth = _depth[slot];
                int cap = LineDisciplineMath.CapFiles(_facts.Men, depth);
                int asked = LineDisciplineMath.FilesOf(askedWidth, _facts.Interval, _facts.Diameter);
                int got = LineDisciplineMath.FilesOf(_facts.Width, _facts.Interval, _facts.Diameter);
                if (asked <= cap) return;
                _stats.PlayerAskedWider++;
                var side = _stats.Side(_facts.PlayerSide);
                var r = Record(slot, side, _facts.Men, depth, cap, LineDisciplineMath.CapWidth(cap, _facts.Interval, _facts.Diameter));
                if (asked > r.WidestAskedFiles)
                {
                    r.WidestAskedFiles = asked;
                    r.WidestAskedWidth = askedWidth;
                }
                bool atCap = got <= cap;
                if (atCap) _stats.PlayerOrderAtCap++;
                else _stats.PlayerOrderTooWide++;
                if (!_firstOrder)
                {
                    _firstOrder = true;
                    TraxLog.Info("lines", "first line order of yours wider than the men hold at " + Sec(now) + " s: " + _world.Name(slot) + " - asked "
                                          + asked + " files (" + LineDisciplineMath.M(askedWidth) + " m), " + _facts.Men + " men at D " + LineDisciplineMath.F(depth)
                                          + " hold at most " + cap + " → the order left it at " + got + " files (" + LineDisciplineMath.M(_facts.Width) + " m)"
                                          + (atCap ? " - AT THE CAP (the game's own drag / move kept to the capped maximum width)" : " - wider: narrowed at the next look (the preview was not capped)"));
                }
                Say(slot, in rules, now, depth, cap);
            }
            catch (Exception e)
            {
                _stats.Errors++;
                AthleticsLogic.Failed("lines.order", e);
            }
        }

        /// <summary>The mission ends: every width / arrangement stays (the mission is over); the summary lines.</summary>
        public IList<string> SummaryLines()
        {
            _stats.PreviewCapReads = _world.PreviewCapReads;
            return _stats.SummaryLines(LineDisciplineRules.From(TraxSettings.Shared));
        }

        private void Say(int slot, in LineDisciplineRules rules, double now, double depth, int cap)
        {
            if (!rules.ShowMessage || now - _lastMessage[slot] < 2) return;
            _lastMessage[slot] = now;
            _stats.Messages++;
            _world.Show(_world.Name(slot) + ": these men hold no thinner than " + DepthWords(depth) + " deep - " + cap + " men wide at most");
        }

        private void NoteRules(in LineDisciplineRules rules, double now)
        {
            if (!rules.Same(in _seen))
            {
                _seen = rules;
                TraxLog.Info("lines", "settings changed mid-mission at " + Sec(now) + " s: " + rules.Describe());
            }
            if (_wasOn && !rules.On)
            {
                int back = GiveBackAll();
                RemoveAllPreviews();
                TraxLog.Info("lines", "switched OFF at " + Sec(now) + " s (" + rules.OffBecause + "): " + back + " formations given the game's own width back, every capped arrangement taken out");
            }
            else if (_wasPlayerOn && !rules.PlayerOn && rules.On)
            {
                int back = 0;
                for (int s = 0; s < _clamped.Length; s++)
                {
                    if (!_clamped[s] || _records[s] == null || !_records[s]!.PlayerOrdered) continue;
                    if (_world.GiveBack(s)) back++;
                    _clamped[s] = false;
                }
                _stats.GivenBack += back;
                RemoveAllPreviews();
                TraxLog.Info("lines", "your formations let go at " + Sec(now) + " s (LineDisciplineForPlayer off): " + back + " given their width back, the drag's cap taken out");
            }
            else if (!_wasOn && rules.On)
            {
                TraxLog.Info("lines", "switched ON at " + Sec(now) + " s: every formation that counts is recounted and held to its depth from the next look");
                Array.Clear(_counted, 0, _counted.Length);
            }
            _wasOn = rules.On;
            _wasPlayerOn = rules.PlayerOn;
        }

        private int GiveBackAll()
        {
            int back = 0;
            for (int s = 0; s < _clamped.Length; s++)
            {
                if (!_clamped[s]) continue;
                try
                {
                    if (_world.GiveBack(s)) back++;
                }
                catch (Exception e)
                {
                    _stats.Errors++;
                    AthleticsLogic.Failed("lines.give-back", e);
                }
                _clamped[s] = false;
            }
            Array.Clear(_counted, 0, _counted.Length);
            _stats.GivenBack += back;
            return back;
        }

        private bool AnyPreview()
        {
            for (int s = 0; s < _preview.Length; s++)
                if (_preview[s]) return true;
            return false;
        }

        private void RemoveAllPreviews()
        {
            for (int s = 0; s < _preview.Length; s++)
                if (_preview[s]) TakePreviewOut(s);
        }

        private void TakePreviewOut(int slot)
        {
            try
            {
                var change = _world.SetPreview(slot, double.NaN);
                if (change == PreviewChange.Removed) _stats.PreviewRemovals++;
            }
            catch (Exception e)
            {
                _stats.Errors++;
                AthleticsLogic.Failed("lines.preview-out", e);
            }
            _preview[slot] = false;
        }

        private void NotePreview(int slot, PreviewChange change, double now, int cap, double capWidth)
        {
            switch (change)
            {
                case PreviewChange.Installed:
                    _stats.PreviewInstalls++;
                    _preview[slot] = true;
                    if (!_firstPreview)
                    {
                        _firstPreview = true;
                        TraxLog.Info("lines", "first capped arrangement at " + Sec(now) + " s: " + _world.Name(slot) + " now tells the game its maximum width is "
                                              + LineDisciplineMath.M(capWidth) + " m (" + cap + " files) - the order drag's preview and the move stop there");
                    }
                    break;
                case PreviewChange.Reinstalled:
                    _stats.PreviewInstalls++;
                    _stats.PreviewReinstalls++;
                    _preview[slot] = true;
                    break;
                case PreviewChange.Removed:
                    _stats.PreviewRemovals++;
                    _preview[slot] = false;
                    break;
                case PreviewChange.Skipped:
                    _stats.PreviewSkipped++;
                    if (!_firstSkippedPreview)
                    {
                        _firstSkippedPreview = true;
                        TraxLog.Info("lines", "the capped arrangement could not go into " + _world.Name(slot) + " at " + Sec(now)
                                              + " s: its arrangement is not the game's plain line (another mod's?) - it is narrowed after each order instead");
                    }
                    break;
                default:
                    break;
            }
        }

        private void NoteFirstPreviewRead(double now)
        {
            if (_firstPreviewRead || !_firstPreview) return;
            long reads = _world.PreviewCapReads;
            if (reads <= 0) return;
            _firstPreviewRead = true;
            TraxLog.Info("lines", "first time the game read a capped maximum width at " + Sec(now) + " s (the order drag's preview or a move kept to the cap) - the PREVIEW IS CAPPED");
        }

        private LineRecord Record(int slot, LineSideStats side, int men, double depth, int cap, double capWidth)
        {
            var r = _records[slot];
            if (r != null) return r;
            r = new LineRecord
            {
                Name = _world.Name(slot),
                PlayerOrdered = _facts.PlayerOrdered,
                MenAtFirstCap = men,
                DepthAtFirstCap = depth,
                CapFilesAtFirstCap = cap,
                CapWidthAtFirstCap = capWidth,
            };
            _records[slot] = r;
            side.Records.Add(r);
            side.Capped++;
            return r;
        }

        private void FirstClampLine(int slot, LineClampCause cause, double now, int men, double depth, int files, int cap, double capWidth)
        {
            bool player = _facts.PlayerOrdered;
            if (cause == LineClampCause.AutoShrink)
            {
                if (_firstShrink) return;
                _firstShrink = true;
            }
            else if (player)
            {
                if (_firstClampPlayer) return;
                _firstClampPlayer = true;
            }
            else
            {
                if (_firstClampAi) return;
                _firstClampAi = true;
            }
            TraxLog.Info("lines", "first " + (cause == LineClampCause.AutoShrink ? "AUTO-SHRINK" : player ? "clamp of YOUR line" : "clamp of an AI line") + " at " + Sec(now) + " s: "
                                  + _world.Name(slot) + " (" + (_facts.PlayerSide ? "your side" : "the other side") + ") stood " + files + " files ("
                                  + LineDisciplineMath.M(_facts.Width) + " m) - " + men + " men at D " + LineDisciplineMath.F(depth) + " hold at most " + cap + " → narrowed to "
                                  + LineDisciplineMath.M(capWidth) + " m: " + LineDisciplineMath.ShapeText(men, cap)
                                  + (cause == LineClampCause.AutoShrink ? " (the cap fell - losses or a new mean depth)" : " (it was made wider - an order or the AI's behaviour)"));
        }

        private string TierText(int men)
        {
            var parts = new List<string>();
            for (int t = 0; t <= LineDisciplineMath.MaxTier; t++)
                if (_tiers[t] > 0) parts.Add(_tiers[t] + " T" + t);
            if (_tiers[LineDisciplineMath.MaxTier + 1] > 0) parts.Add(_tiers[LineDisciplineMath.MaxTier + 1] + " heroes");
            return men + " men (" + string.Join(", ", parts) + ")";
        }

        private static string DepthWords(double depth)
        {
            double r = Math.Round(depth * 2) / 2; // 3.5 says 3.5, 4.8 says 5
            return r.ToString("0.#", CultureInfo.InvariantCulture);
        }

        private void Ensure(int slots)
        {
            if (slots <= _counted.Length) return;
            int n = Math.Max(slots, _counted.Length * 2);
            Array.Resize(ref _depth, n);
            Array.Resize(ref _depthMen, n);
            Array.Resize(ref _nextPoll, n);
            Array.Resize(ref _lastFiles, n);
            Array.Resize(ref _counted, n);
            Array.Resize(ref _everCounted, n);
            Array.Resize(ref _retryAt, n);
            Array.Resize(ref _clamped, n);
            Array.Resize(ref _preview, n);
            int old = _lastMessage.Length;
            Array.Resize(ref _lastMessage, n);
            for (int i = old; i < n; i++) _lastMessage[i] = double.NegativeInfinity;
            Array.Resize(ref _records, n);
        }

        private static string Sec(double t) => t.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
