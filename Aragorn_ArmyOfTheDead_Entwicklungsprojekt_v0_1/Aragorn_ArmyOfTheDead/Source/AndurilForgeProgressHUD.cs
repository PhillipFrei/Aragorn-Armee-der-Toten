using RimWorld;
using UnityEngine;
using Verse;

namespace AragornArmy
{
    // Visible green-and-gray progress bar floating above the vanilla forge.
    // Using world rendering avoids UI toolkit dependencies and zoom interference.
    public sealed class AndurilForgeProgressHUD : MapComponent
    {
        private static readonly Material FillMaterial =
            SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.95f, 0.71f, 0.17f));
        private static readonly Material EmptyMaterial =
            SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.15f, 0.15f, 0.15f));

        public AndurilForgeProgressHUD(Map map) : base(map) { }

        public override void MapComponentDraw()
        {
            AragornForgeGame forge = Current.Game?.GetComponent<AragornForgeGame>();
            if (forge == null || !forge.ShowForgeProgress(map)) return;
            if (Find.CurrentMap != map) return;
            Thing bench = forge.ForgeBuilding;
            if (bench == null || !bench.Spawned) return;
            GenDraw.DrawFillableBar(new GenDraw.FillableBarRequest {
                center = bench.DrawPos + new Vector3(0f, 0.5f, 1.1f),
                size = new Vector2(2.9f, .28f),
                fillPercent = forge.ForgeProgressFraction,
                filledMat = FillMaterial,
                unfilledMat = EmptyMaterial,
                margin = .12f,
                rotation = Rot4.North
            });
        }
    }
}
