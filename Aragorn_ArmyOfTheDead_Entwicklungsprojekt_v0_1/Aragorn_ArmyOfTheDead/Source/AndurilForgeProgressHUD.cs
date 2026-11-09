using RimWorld;
using UnityEngine;
using Verse;

namespace AragornArmy
{
    // Permanent, unobtrusive on-screen quest HUD while viewing the forge map.
    // Visible while paused or zoomed; no new art or texture required.
    public sealed class AndurilForgeProgressHUD : MapComponent
    {
        public AndurilForgeProgressHUD(Map map) : base(map) { }

        public override void MapComponentOnGUI()
        {
            if (Find.CurrentMap != map || Current.ProgramState != ProgramState.Playing)
                return;
            AragornForgeGame forge = Current.Game?.GetComponent<AragornForgeGame>();
            if (forge == null || !forge.ShowForgeProgress(map)) return;

            Rect panel = new Rect(UI.screenWidth * 0.5f - 190f, 92f, 380f, 59f);
            Widgets.DrawBoxSolid(panel, new Color(0.10f, 0.08f, 0.07f, 0.91f));
            Rect label = new Rect(panel.x + 10f, panel.y + 5f, panel.width - 20f, 23f);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(label, forge.ForgeProgressLabel);
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.FillableBar(new Rect(panel.x + 14f, panel.y + 34f,
                panel.width - 28f, 15f), forge.ForgeProgressFraction);
        }
    }
}
