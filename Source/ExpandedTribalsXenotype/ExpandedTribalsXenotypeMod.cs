using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace ExpandedTribalsXenotype
{
    public class ExpandedTribalsXenotypeMod : Mod
    {
        public static ExpandedTribalsXenotypeSettings Settings;

        public ExpandedTribalsXenotypeMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<ExpandedTribalsXenotypeSettings>();
        }

        public override string SettingsCategory() => "ExpandedTribalsXenotypeTitle".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);

            Settings.DoSettingsWindowContents(inRect);
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            ExpandedTribalsXenotypePatcher.Apply();
        }
    }

    public class ExpandedTribalsXenotypeSettings : ModSettings
    {
        public List<string> selectedXenotypeDefNames = new List<string>();

        private Vector2 scrollPosition;

        public override void ExposeData()
        {
            Scribe_Collections.Look(
                ref selectedXenotypeDefNames,
                "selectedXenotypeDefNames",
                LookMode.Value);

            selectedXenotypeDefNames ??= new List<string>();
        }

        public void DoSettingsWindowContents(Rect inRect)
        {
            //lol, well i tried doing a side-by-side box list, but that was too much effort; so maximum lazy.
            List<XenotypeDef> xenotypes = DefDatabase<XenotypeDef>.AllDefsListForReading
                .Where(x => x != null)
                .OrderBy(x => x.label)
                .ToList();

            Rect viewRect = new Rect(
                0f,
                0f,
                inRect.width - 16f,
                xenotypes.Count * 32f + 120f);

            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);
            listing.GapLine();

            if (listing.ButtonText("ExpandedTribalsXenotypeSelectAll".Translate()))
            {
                selectedXenotypeDefNames = xenotypes
                    .Select(x => x.defName)
                    .ToList();
            }

            if (listing.ButtonText("ExpandedTribalsXenotypeClearAll".Translate()))
            {
                selectedXenotypeDefNames.Clear();
            }

            listing.GapLine();

            foreach (XenotypeDef xenotype in xenotypes)
            {
                bool selected = selectedXenotypeDefNames.Contains(xenotype.defName);

                listing.CheckboxLabeled(
                    xenotype.LabelCap,
                    ref selected,
                    xenotype.description);

                if (selected)
                {
                    if (!selectedXenotypeDefNames.Contains(xenotype.defName))
                        selectedXenotypeDefNames.Add(xenotype.defName);
                }
                else
                {
                    selectedXenotypeDefNames.Remove(xenotype.defName);
                }
            }

            listing.End();
            Widgets.EndScrollView();

            selectedXenotypeDefNames = selectedXenotypeDefNames
                .Distinct()
                .Where(defName => xenotypes.Any(x => x.defName == defName))
                .ToList();
        }
    }

    [StaticConstructorOnStartup]
    public static class ExpandedTribalsXenotypePatcher
    {
        private static readonly FieldInfo XenotypeChancesField =
            typeof(XenotypeSet).GetField(
                "xenotypeChances",
                BindingFlags.Instance | BindingFlags.NonPublic);

        static ExpandedTribalsXenotypePatcher()
        {
            LongEventHandler.ExecuteWhenFinished(Apply);
        }

        public static void Apply()
        {
            if (!ModsConfig.BiotechActive)
                return;

            PawnKindDef wildperson =
                DefDatabase<PawnKindDef>.GetNamedSilentFail("VFET_Wildperson");

            if (wildperson == null)
            {
                Log.Warning("[ExpandedTribalsXenotype] VFET_Wildperson Probably means you don't have VFE Tribals installed.");
                return;
            }

            if (ExpandedTribalsXenotypeMod.Settings == null ||
                ExpandedTribalsXenotypeMod.Settings.selectedXenotypeDefNames.NullOrEmpty())
            {
                Log.Warning("[ExpandedTribalsXenotype] No xenotypes selected. VFET_Wildperson was not patched; so its just gonna run as vanilla.");
                return;
            }

            List<XenotypeChance> chances =
                ExpandedTribalsXenotypeMod.Settings.selectedXenotypeDefNames
                    .Select(defName => DefDatabase<XenotypeDef>.GetNamedSilentFail(defName))
                    .Where(x => x != null)
                    .Select(x => new XenotypeChance
                    {
                        xenotype = x,
                        chance = 1f
                    })
                    .ToList();

            if (chances.Count == 0)
            {
                Log.Warning("[ExpandedTribalsXenotype] Selected xenotypes were invalid. VFET_Wildperson was not patched; I'm not even sure how we got here.");
                return;
            }

            XenotypeSet set = new XenotypeSet();

            XenotypeChancesField.SetValue(set, chances);

            wildperson.xenotypeSet = set;
            wildperson.useFactionXenotypes = false;

            Log.Message($"[ExpandedTribalsXenotype] Patched VFET_Wildperson with {chances.Count} xenotypes. Now you can start your tribal monster girl quest.");
        }
    }
}