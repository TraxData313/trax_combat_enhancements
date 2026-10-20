using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 73 - the line discipline's game side (AI_NOTES "Step 73"): the formations of every team (slot = team index × 8 + its index in
    /// <c>Team.FormationsIncludingEmpty</c>), their men's tiers (<c>BasicCharacterObject.GetBattleTier()</c> - the campaign's troop tier for
    /// a soldier: ceil((level − 5) ÷ 5), 0-7; a hero by <c>Agent.IsHero</c>), the clamp (the game's own <c>Formation.SetFormOrder(FormOrder
    /// .FormOrderCustom(width))</c> - the order the player's own drag gives), the give-back (the game's form order we replaced) and the
    /// preview cap (<see cref="DisciplinedLineFormation"/> put in with the game's own <c>Formation.Rearrange</c>). Main thread only (the
    /// logic's pre-tick and the order event). Allocation-free per look.
    /// </summary>
    internal sealed class GameLineWorld : ILineWorld
    {
        public const int PerTeam = 8;

        private readonly Mission _mission;
        private Formation?[] _formations = Array.Empty<Formation?>();
        private FormOrder[] _saved = Array.Empty<FormOrder>();
        private bool[] _hasSaved = Array.Empty<bool>();
        private float[] _ours = Array.Empty<float>();
        private DisciplinedLineFormation?[] _mine = Array.Empty<DisciplinedLineFormation?>();
        private IFormationArrangement?[] _skippedFor = Array.Empty<IFormationArrangement?>();
        private long _readsDone;
        private int _slots;

        public GameLineWorld(Mission mission)
        {
            _mission = mission;
            Mission = KindOf(mission);
        }

        public LineMission Mission { get; }

        public static LineMission KindOf(Mission m)
        {
            try
            {
                if (m.IsNavalBattle || m.IsNavalRaidBattle) return LineMission.Naval;
                if (m.IsSiegeBattle || m.IsSallyOutBattle) return LineMission.Siege;
            }
            catch
            {
                // an unknown kind: a field battle
            }
            return LineMission.Field;
        }

        public int BeginLook()
        {
            var teams = _mission.Teams;
            int n = teams.Count * PerTeam;
            Ensure(n);
            for (int t = 0; t < teams.Count; t++)
            {
                var list = teams[t]?.FormationsIncludingEmpty;
                for (int i = 0; i < PerTeam; i++)
                {
                    int slot = t * PerTeam + i;
                    var f = list != null && i < list.Count ? list[i] : null;
                    if (!ReferenceEquals(f, _formations[slot]))
                    {
                        // a new formation in this slot (a team came and went): nothing of the old one carries over
                        _hasSaved[slot] = false;
                        _mine[slot] = null;
                        _skippedFor[slot] = null;
                    }
                    _formations[slot] = f;
                }
            }
            for (int slot = n; slot < _slots; slot++) _formations[slot] = null;
            _slots = n;
            return n;
        }

        /// <summary>The slot of one of the player's formations (the order event), −1 = none.</summary>
        public int SlotOf(Formation f)
        {
            if (f == null) return -1;
            var teams = _mission.Teams;
            for (int t = 0; t < teams.Count; t++)
            {
                if (!ReferenceEquals(teams[t], f.Team)) continue;
                var list = teams[t].FormationsIncludingEmpty;
                for (int i = 0; i < list.Count && i < PerTeam; i++)
                    if (ReferenceEquals(list[i], f)) return t * PerTeam + i;
            }
            return -1;
        }

        public bool Read(int slot, ref LineFacts facts)
        {
            var f = slot >= 0 && slot < _slots ? _formations[slot] : null;
            if (f == null || f.CountOfUnits <= 0) return false;
            var arr = f.Arrangement;
            if (arr == null) return false;
            var team = f.Team;
            var player = _mission.PlayerTeam;
            facts.Men = arr.UnitCount;
            facts.PlayerOrdered = player != null && ReferenceEquals(team, player) && !f.IsAIControlled;
            facts.PlayerSide = player != null && team != null && team.Side == player.Side;
            facts.Shape = arr is LineFormation && !(arr is TransposedLineFormation) ? ShapeOf(f.ArrangementOrder.OrderEnum) : LineShape.Other;
            var q = f.QuerySystem;
            facts.Mounted = q.IsCavalryFormationReadOnly || q.IsRangedCavalryFormationReadOnly;
            facts.Ranged = q.IsRangedFormationReadOnly;
            facts.Width = f.Width;
            facts.Interval = f.Interval;
            facts.Diameter = f.UnitDiameter;
            return true;
        }

        public static LineShape ShapeOf(ArrangementOrder.ArrangementOrderEnum e)
        {
            switch (e)
            {
                case ArrangementOrder.ArrangementOrderEnum.Line: return LineShape.Line;
                case ArrangementOrder.ArrangementOrderEnum.ShieldWall: return LineShape.ShieldWall;
                case ArrangementOrder.ArrangementOrderEnum.Loose: return LineShape.Loose;
                case ArrangementOrder.ArrangementOrderEnum.Scatter: return LineShape.Scatter;
                case ArrangementOrder.ArrangementOrderEnum.Column: return LineShape.Column;
                case ArrangementOrder.ArrangementOrderEnum.Square: return LineShape.Square;
                case ArrangementOrder.ArrangementOrderEnum.Circle: return LineShape.Circle;
                case ArrangementOrder.ArrangementOrderEnum.Skein: return LineShape.Skein;
                default: return LineShape.Other;
            }
        }

        public void Ranks(int slot, in LineDisciplineRules rules, int[] tiers, out double sum, out int men)
        {
            sum = 0;
            men = 0;
            var f = _formations[slot];
            var units = f?.Arrangement?.GetAllUnits();
            if (units == null) return;
            for (int i = 0; i < units.Count; i++)
            {
                if (!(units[i] is Agent a) || !a.IsHuman) continue;
                bool hero = a.IsHero;
                var ch = a.Character;
                int battle = ch != null ? ch.GetBattleTier() : 0;
                int tier = LineDisciplineMath.TierOf(battle, hero, in rules);
                sum += rules.RanksOf(tier);
                men++;
                tiers[hero ? LineDisciplineMath.MaxTier + 1 : tier]++;
            }
        }

        public bool Clamp(int slot, double width)
        {
            var f = _formations[slot];
            if (f == null) return true;
            var current = f.FormOrder;
            if (!IsOurs(slot, current))
            {
                _saved[slot] = current;
                _hasSaved[slot] = true;
            }
            _ours[slot] = (float)width;
            f.SetFormOrder(FormOrder.FormOrderCustom((float)width));
            double interval = f.Interval, diameter = f.UnitDiameter;
            return LineDisciplineMath.FilesOf(f.Width, interval, diameter) <= LineDisciplineMath.FilesOf(width, interval, diameter);
        }

        public bool GiveBack(int slot)
        {
            var f = slot < _slots ? _formations[slot] : null;
            if (f == null || !_hasSaved[slot]) return false;
            _hasSaved[slot] = false;
            if (!IsOurs(slot, f.FormOrder)) return false; // an order since - the game's own stands
            f.SetFormOrder(_saved[slot]);
            return true;
        }

        /// <summary>Our custom form order: custom, its width ours (the game re-applies it at the line's exact width - ours less the slack).</summary>
        private bool IsOurs(int slot, FormOrder order) =>
            _ours[slot] > 0 && order.OrderEnum == FormOrder.FormOrderEnum.Custom && Math.Abs(order.CustomFlankWidth - _ours[slot]) <= 0.25f;

        public PreviewChange SetPreview(int slot, double capWidth)
        {
            var f = _formations[slot];
            if (f == null) return PreviewChange.None;
            var arr = f.Arrangement;
            if (double.IsNaN(capWidth))
            {
                if (arr is DisciplinedLineFormation d)
                {
                    _readsDone += d.CapReads;
                    var plain = new LineFormation(f, d.IsStaggered);
                    plain.DeepCopyFrom(d);
                    f.Rearrange(plain);
                    _mine[slot] = null;
                    return PreviewChange.Removed;
                }
                if (_mine[slot] != null)
                {
                    _readsDone += _mine[slot]!.CapReads;
                    _mine[slot] = null;
                }
                return PreviewChange.None;
            }
            if (arr is DisciplinedLineFormation ours)
            {
                ours.CapWidth = (float)capWidth;
                return PreviewChange.None;
            }
            if (arr == null || arr.GetType() != typeof(LineFormation))
            {
                if (ReferenceEquals(_skippedFor[slot], arr)) return PreviewChange.None;
                _skippedFor[slot] = arr;
                return PreviewChange.Skipped;
            }
            var line = (LineFormation)arr;
            var mine = new DisciplinedLineFormation(f, line.IsStaggered) { CapWidth = (float)capWidth };
            mine.DeepCopyFrom(line);
            bool again = _mine[slot] != null; // the game put its own plain line back (a new arrangement order)
            if (again) _readsDone += _mine[slot]!.CapReads;
            f.Rearrange(mine);
            _mine[slot] = mine;
            return again ? PreviewChange.Reinstalled : PreviewChange.Installed;
        }

        public long PreviewCapReads
        {
            get
            {
                long n = _readsDone;
                for (int i = 0; i < _slots; i++)
                    if (_mine[i] != null) n += _mine[i]!.CapReads;
                return n;
            }
        }

        public string Name(int slot)
        {
            var f = slot >= 0 && slot < _slots ? _formations[slot] : null;
            if (f == null) return "(gone)";
            var team = f.Team;
            string who = team == null ? "" : team.IsPlayerTeam ? "your " : team.IsPlayerAlly ? "the ally's " : "the enemy's ";
            return who + f.FormationIndex.GetName();
        }

        public void Show(string text)
        {
            InformationManager.DisplayMessage(new InformationMessage(text, Color.ConvertStringToColor("#E8D9A0FF")));
        }

        private void Ensure(int n)
        {
            if (n <= _formations.Length) return;
            Array.Resize(ref _formations, n);
            Array.Resize(ref _saved, n);
            Array.Resize(ref _hasSaved, n);
            Array.Resize(ref _ours, n);
            Array.Resize(ref _mine, n);
            Array.Resize(ref _skippedFor, n);
        }
    }
}
