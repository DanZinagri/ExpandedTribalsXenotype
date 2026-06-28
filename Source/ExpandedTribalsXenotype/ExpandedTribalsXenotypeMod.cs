using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using VFETribals;

namespace ExpandedTribalsXenotype
{
    public class ExpandedTribalsXenotypeMod : Mod
    {
        public static ExpandedTribalsXenotypeSettings Settings;

        public ExpandedTribalsXenotypeMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<ExpandedTribalsXenotypeSettings>();

            var harmony = new Harmony("DanZinagri.ExpandedTribalsXenotype");
            harmony.PatchAll();
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
        public bool enableWildManJoinPrompt = true;


        private Vector2 scrollPosition;

        private string searchText = "";

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref selectedXenotypeDefNames,"selectedXenotypeDefNames", LookMode.Value);

            Scribe_Values.Look(ref enableWildManJoinPrompt, "enableWildManJoinPrompt", true);

            selectedXenotypeDefNames ??= new List<string>();
        }

        public void DoSettingsWindowContents(Rect inRect)
        {
            //lol, well i tried doing a side-by-side box list, but that was too much effort; so maximum lazy.    
            List<XenotypeDef> allXenotypes = DefDatabase<XenotypeDef>.AllDefsListForReading
                .Where(x => x != null)
                .OrderBy(x => x.label)
                .ToList();

            Rect searchRect = new Rect(
                inRect.x, 
                inRect.y, 
                inRect.width, 
                124f);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(searchRect);
            //searchText = Widgets.TextField(searchRect, searchText);
            listing.CheckboxLabeled(
                    "ExpandedTribalsXenotypePromptSetting".Translate(),
                    ref enableWildManJoinPrompt,
                    "ExpandedTribalsXenotypePromptDesc".Translate());
            searchText = listing.TextEntry(searchText);

            if (listing.ButtonText("ExpandedTribalsXenotypeSelectAll".Translate()))
            {
                selectedXenotypeDefNames = allXenotypes
                    .Select(x => x.defName)
                    .ToList();
            }

            if (listing.ButtonText("ExpandedTribalsXenotypeClearAll".Translate()))
            {
                selectedXenotypeDefNames.Clear();
            }
            listing.GapLine();
            listing.End();

            float controlsBottom = searchRect.height + 8f; // listing.CurHeight + 8f; //searchRect.yMax + 8f; 
            Rect scrollRect = new Rect(
                inRect.x,
                inRect.y + controlsBottom,
                inRect.width,
                inRect.height - controlsBottom);

            List<XenotypeDef> visibleXenotypes = allXenotypes;

            if (!searchText.NullOrEmpty())
            {
                string search = searchText.ToLower();

                visibleXenotypes = allXenotypes
                    .Where(x =>
                        x.label.ToLower().Contains(search) ||
                        x.defName.ToLower().Contains(search))
                    .ToList();
            }

            Rect viewRect = new Rect(
                0f,
                0f,
                scrollRect.width - 16f,
                visibleXenotypes.Count * 36f + 20f);

            Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);

            Listing_Standard scrolllisting = new Listing_Standard();
            scrolllisting.Begin(viewRect);

            foreach (XenotypeDef xenotype in visibleXenotypes)
            {
                bool selected = selectedXenotypeDefNames.Contains(xenotype.defName);

                scrolllisting.CheckboxLabeled(
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

            scrolllisting.End();
            Widgets.EndScrollView();

            selectedXenotypeDefNames = selectedXenotypeDefNames
                .Distinct()
                .Where(defName => allXenotypes.Any(x => x.defName == defName))
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

    [HarmonyPatch(typeof(LetterStack), nameof(LetterStack.ReceiveLetter),
    new[]
    {
        typeof(TaggedString),
        typeof(TaggedString),
        typeof(LetterDef),
        typeof(LookTargets),
        typeof(Faction),
        typeof(Quest),
        typeof(List<ThingDef>),
        typeof(string),
        typeof(int),
        typeof(bool)
    })]
    public static class Patch_SuppressOriginalTribalGatheringWildmanLetter
    {
        public static bool Prefix(TaggedString label, TaggedString text, LetterDef textLetterDef)
        {
            if (ExpandedTribalsXenotypeMod.Settings?.enableWildManJoinPrompt != true)
                return true;
            if (label.Resolve() != "VFET_WildmanLetterLabel".Translate().Resolve())
                return true;

            if (!Patch_TribalGatheringApplyContext.InTribalGatheringApply)
                return true;

            return false;
        }
    }

    [HarmonyPatch(typeof(RitualOutcomeEffectWorker_TribalGathering), nameof(RitualOutcomeEffectWorker_TribalGathering.Apply))]
    public static class Patch_TribalGatheringApplyContext
    {
        public static bool InTribalGatheringApply;

        public static void Prefix()
        {
            InTribalGatheringApply = true;
        }

        public static void Finalizer()
        {
            InTribalGatheringApply = false;
        }
    }


    [HarmonyPatch(typeof(GenSpawn), nameof(GenSpawn.Spawn),
        new[] { typeof(Thing), typeof(IntVec3), typeof(Map), typeof(WipeMode) })]
    public static class Patch_TribalGatheringWildmanSpawn
    {
        public static bool Prefix(Thing newThing, IntVec3 loc, Map map, WipeMode wipeMode, ref Thing __result)
        {
            if (ExpandedTribalsXenotypeMod.Settings?.enableWildManJoinPrompt != true)
                return true;

            Pawn pawn = newThing as Pawn;
            if (pawn == null || pawn.kindDef?.defName != "VFET_Wildperson")
                return true;

            if (!Patch_TribalGatheringApplyContext.InTribalGatheringApply)
                return true;

            __result = pawn;

            ChoiceLetter_TribalGatheringWildmanWishesToJoin letter =  new ChoiceLetter_TribalGatheringWildmanWishesToJoin();

            letter.def = LetterDefOf.RitualOutcomePositive;
            letter.Label = "WildJoinWish".Translate();
            letter.Text = "WildManWishesToJoinDesc".Translate(pawn.LabelShortCap, pawn.LabelShort, pawn.kindDef.label, pawn.ageTracker.AgeBiologicalYears, pawn.gender.GetLabel());

            letter.pawn = pawn;
            letter.map = map;
            letter.spawnCell = loc;
            letter.lookTargets = pawn;

            Find.LetterStack.ReceiveLetter(letter);

            return false;
        }
    }

    public class ChoiceLetter_TribalGatheringWildmanWishesToJoin : ChoiceLetter
    {
        public Pawn pawn;
        public Map map;
        public IntVec3 spawnCell;

        public override bool CanDismissWithRightClick => false;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                yield return new DiaOption("WildJoinInspect".Translate())
                {
                    action = ShowInfo,
                    resolveTree = false
                };

                yield return new DiaOption("WildJoinAccept".Translate())
                {
                    action = Accept,
                    resolveTree = true
                };

                yield return new DiaOption("WildJoinReject".Translate())
                {
                    action = Reject,
                    resolveTree = true
                };
            }
        }

        private void ShowInfo()
        {
            if (pawn != null)
            {
                Find.WindowStack.Add(new Dialog_InfoCard(pawn));
            }
        }

        private void Accept()
        {
            if (pawn != null && !pawn.Destroyed && map != null)
            {
                pawn.SetFaction(Faction.OfPlayer);

                if (!spawnCell.IsValid || !spawnCell.Walkable(map))
                {
                    CellFinder.TryFindRandomEdgeCellWith(
                        c => c.Walkable(map),
                        map,
                        CellFinder.EdgeRoadChance_Neutral,
                        out spawnCell
                    );
                }

                GenSpawn.Spawn(pawn, spawnCell, map);
            }

            Close();
        }

        private void Reject()
        {
            if (pawn != null && !pawn.Destroyed)
                Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.Discard);

            Close();
        }

        private void Close()
        {
            Find.Archive.Remove(this);
            Find.LetterStack.RemoveLetter(this);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref spawnCell, "spawnCell");
        }
    }
}