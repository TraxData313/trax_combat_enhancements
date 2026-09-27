using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Order;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Hud
{
    /// <summary>
    /// The engine side of the orders-menu strip's alignment (step 9) behind one seam - the offline
    /// smoke plays it with made-up cards, like <see cref="IHudLayer"/>. <see cref="GauntletOrderCards"/>
    /// is the real one.
    /// </summary>
    internal interface IOrderCardSource
    {
        /// <summary>Finds the vanilla formation cards (each open of the orders menu). False when none
        /// were found; <paramref name="detail"/> says where it looked / what it found.</summary>
        bool Scan(out string detail);

        /// <summary>Reads the cards found by the last scan into <paramref name="frame"/> (pixels, drawn
        /// or not, members). False when they are gone from the screen (the movie was replaced) - scan
        /// again. Allocation-free.</summary>
        bool Read(OrderCardFrame frame);
    }

    /// <summary>
    /// The real card reader: the game's order layer ("MissionOrder", built by
    /// GauntletOrderUIHandler - vanilla, War Sails' naval handler and RTS Camera Command System all
    /// keep it) found with the PUBLIC <c>ScreenBase.FindLayer</c>, its live widget tree walked once per
    /// open (<c>UIContext.Root</c>), the card widgets kept in the tree's order. A card is an
    /// <see cref="OrderTroopItemBrushWidget"/> with children whose parent holds a second one (the
    /// selection highlight) - that leaves out the transfer popup's cards, which stand alone under a
    /// button. Every frame: each card's GlobalPosition / Size (screen pixels), recursive visibility
    /// and CurrentMemberCount. Nothing is patched, injected or reflected - only read.
    /// </summary>
    internal sealed class GauntletOrderCards : IOrderCardSource
    {
        /// <summary>The layer GauntletOrderUIHandler builds (engine fact).</summary>
        public const string OrderLayerName = "MissionOrder";

        private readonly MissionScreen _screen;
        private readonly List<OrderTroopItemBrushWidget> _cards = new List<OrderTroopItemBrushWidget>(OrderCardFrame.MaxCards);
        private GauntletLayer? _layer;

        public GauntletOrderCards(MissionScreen screen)
        {
            _screen = screen;
        }

        public bool Scan(out string detail)
        {
            _cards.Clear();
            _layer = null;
            var named = _screen.FindLayer<GauntletLayer>(OrderLayerName);
            if (named != null)
            {
                Collect(named.UIContext?.Root);
                if (_cards.Count > 0)
                {
                    _layer = named;
                    detail = "layer " + OrderLayerName + ": " + _cards.Count + " cards";
                    return true;
                }
            }
            // Another order UI (a mod) with its own layer name: any Gauntlet layer holding card slots.
            foreach (var l in _screen.Layers)
            {
                if (!(l is GauntletLayer g) || ReferenceEquals(g, named)) continue;
                Collect(g.UIContext?.Root);
                if (_cards.Count == 0) continue;
                _layer = g;
                detail = "layer " + (g.Name ?? "(unnamed)") + " (not " + OrderLayerName + "): " + _cards.Count + " cards";
                return true;
            }
            detail = named == null
                ? "no " + OrderLayerName + " layer on the screen and no other layer holds formation cards"
                : "the " + OrderLayerName + " layer holds no formation cards";
            return false;
        }

        /// <summary>Depth-first in child order (the prefab's order = TroopItem0..7 per set).</summary>
        private void Collect(Widget? w)
        {
            if (w == null || _cards.Count >= OrderCardFrame.MaxCards) return;
            if (w is OrderTroopItemBrushWidget card && card.ChildCount > 0 && InSlot(card))
            {
                _cards.Add(card);
                return; // nothing below a card is a card
            }
            for (int i = 0; i < w.ChildCount; i++) Collect(w.GetChild(i));
        }

        /// <summary>The card sits in a slot beside another brush widget of its type (the highlight).</summary>
        private static bool InSlot(Widget card)
        {
            var parent = card.ParentWidget;
            if (parent == null) return false;
            for (int i = 0; i < parent.ChildCount; i++)
            {
                var sibling = parent.GetChild(i);
                if (!ReferenceEquals(sibling, card) && sibling is OrderTroopItemBrushWidget) return true;
            }
            return false;
        }

        public bool Read(OrderCardFrame frame)
        {
            var layer = _layer;
            if (layer == null || !_screen.HasLayer(layer)) return false;
            var context = layer.UIContext;
            var root = context?.Root;
            if (root == null) return false;
            var page = context!.EventManager.PageSize;
            frame.ScreenWidth = page.X;
            frame.ScreenHeight = page.Y;
            frame.Scale = context.CustomScale > 0f ? context.CustomScale : 1f;
            int n = _cards.Count;
            for (int i = 0; i < n; i++)
            {
                var c = _cards[i];
                // Connected to this layer's root and drawn: one walk up the parents.
                bool visible = true;
                Widget? top = c;
                for (Widget? w = c; w != null; w = w.ParentWidget)
                {
                    if (!w.IsVisible) visible = false;
                    top = w;
                }
                if (!ReferenceEquals(top, root)) return false; // the movie was released / replaced
                var pos = c.GlobalPosition;
                var size = c.Size;
                ref var slot = ref frame.Cards[i];
                slot.Visible = visible;
                slot.X = pos.X;
                slot.Y = pos.Y;
                slot.Width = size.X;
                slot.Height = size.Y;
                slot.Members = c.CurrentMemberCount;
            }
            frame.Count = n;
            return true;
        }
    }

    /// <summary>The player's formations as the strip needs them - a seam, the smoke plays it.</summary>
    internal interface IStripFormations
    {
        /// <summary>Formations 0..7 of the player's team into <paramref name="into"/> (8 entries).
        /// Allocation-free.</summary>
        void Read(StripFormation[] into);
    }

    /// <summary>The real one: <c>Mission.PlayerTeam.GetFormation(k)</c> (an index), the men under
    /// command exactly as the card counts them (units minus the player when he is in it) and the
    /// Athletics logic's snapshot (<see cref="AthleticsLogic.TryGetFormationStats"/>).</summary>
    internal sealed class MissionStripFormations : IStripFormations
    {
        private readonly Mission _mission;

        public MissionStripFormations(Mission mission)
        {
            _mission = mission;
        }

        public void Read(StripFormation[] into)
        {
            var team = _mission.PlayerTeam;
            for (int k = 0; k < into.Length; k++)
            {
                into[k] = default;
                if (team == null || k >= OrderStripMath.CardsPerSet) continue;
                var f = team.GetFormation((FormationClass)k);
                if (f == null) continue;
                int units = f.CountOfUnits;
                into[k].Exists = true;
                into[k].Members = f.IsPlayerTroopInFormation && units > 0 ? units - 1 : units;
                if (AthleticsLogic.TryGetFormationStats(f, out var stats)) into[k].Stats = stats;
            }
        }
    }
}
