using System.Collections.Generic;

namespace RubiniteAP
{
    internal sealed class BossInfo
    {
        public string Key;
        public string Name;
        public string Level;
        public string Sigil;
        public bool StartUnlocked;
        public int LocationBase;

        public BossInfo(string key, string name, string level, string sigil, bool startUnlocked, int locationBase)
        {
            Key = key;
            Name = name;
            Level = level;
            Sigil = sigil;
            StartUnlocked = startUnlocked;
            LocationBase = locationBase;
        }
    }

    internal static class GameData
    {
        public const string GameName = "Rubinite";
        public const int ChallengeSaveNumber = 999;

        // Keep this order in sync with archipelago/rubinite/data.py.
        public static readonly BossInfo[] Bosses =
        {
            new BossInfo("Beast",      "The Beast",           "BeastLevel",         null,                  true, 71000),
            new BossInfo("Guardian",   "Faye the Guardian",   "GuardianLevel",      null,                  true, 71005),
            new BossInfo("EyesTree",   "The Red-Eyed Tree",   "EyesTreeLevel",      null,                  true, 71010),
            new BossInfo("Cult",       "The Priest",          "CultLevel",          "Cult Sigil",          false, 71015),
            new BossInfo("PlagueBird", "The Gravekeeper",     "PlagueLevel",        "Gravekeeper Sigil",   false, 71020),
            new BossInfo("PlagueRoot", "The Plaguebearer",    "PlagueRootLevel",    "Plaguebearer Sigil",  false, 71025),
            new BossInfo("Nestling",   "The Martyred Angel",  "NestlingLevel",      "Nestling Sigil",      false, 71030),
            new BossInfo("Guardian2",  "Faye II",             "GuardianLevel2",     "Faye II Sigil",       false, 71035),
            new BossInfo("Ghost",      "The Ghost",           "GhostLevel",         "Ghost Sigil",         false, 71040),
            new BossInfo("Master",     "Roy the Hunter",      "MasterLevel",        "Hunter Sigil",        false, 71045),
            new BossInfo("Queen",      "The Puppet Master",   "QueenLevel",         "Puppet Master Sigil", false, 71050),
            new BossInfo("Witch",      "Alcina the Witch",    "Witch_FirstRunLevel", "Witch Sigil",        false, 71055),
            new BossInfo("Witch2",     "Alcina, True Form",   "WitchLevel",         "Witch II Sigil",      false, 71060),
        };

        // First-tier skills stay unlocked so the run starts with a usable kit.
        public static readonly string[] FreeSkills = { "1_1", "1_2", "1_3", "1_4" };

        public static bool IsFreeSkill(string treeDisplayId)
        {
            if (string.IsNullOrEmpty(treeDisplayId)) return false;
            foreach (var id in FreeSkills)
                if (id == treeDisplayId) return true;
            return false;
        }

        public static readonly string[] Sigils =
        {
            "Cult Sigil", "Gravekeeper Sigil", "Plaguebearer Sigil", "Nestling Sigil",
            "Faye II Sigil", "Ghost Sigil", "Hunter Sigil", "Puppet Master Sigil",
            "Witch Sigil", "Witch II Sigil"
        };

        public static readonly string[] CheckSuits =
        {
            "First Clear", "No-Hit Clear", "No-Talisman Clear", "Flawless Clear", "S-Rank Clear"
        };

        // Talisman item name (without the "Talisman: " prefix) -> TalismanData uniqueId.
        public static readonly Dictionary<string, string> TalismanIds = new Dictionary<string, string>
        {
            { "Grounded Lunge", "01_AddStrikeDistance" },
            { "Flutterstep", "02_AgileBody" },
            { "Sharp Blade, Sharp Eye", "03_Attack3Focus" },
            { "Scorched Trail", "04_BloodFire" },
            { "Bloodwings", "05_BloodFly" },
            { "Direct Hit", "06_CriticalStrikeATKUp" },
            { "Mira's Shadow", "07_MiraTalisman" },
            { "Pocket Bombs", "08_DodgeBomb" },
            { "Killer Instinct", "09_SpecialStrikeAngle" },
            { "Thorny Rose", "10_FocusFlower" },
            { "Pensive Eyes", "11_FocusReduceSpeedDecrease" },
            { "Keen Gaze", "12_FocusSpeedUp" },
            { "Greed", "13_Greed" },
            { "Bloodsucker", "14_HPDrain" },
            { "Hatchiwi", "15_WichiHat" },
            { "Mark Meister", "16_MarkMaster" },
            { "Paralyzing Strike", "17_Paralysis" },
            { "Bracelet of Blessing", "18_Resistance" },
            { "Perfect Strike", "19_InvincibleStrike" },
            { "Earthen Barrier", "20_StrongBody" },
            { "True Ranger", "21_IncreaseShootingATK" },
            { "Roy's Dagger", "22_RoyKnife" },
            { "True Swordfighter", "23_IncreaseSlashATK" },
            { "Sharpshooter", "24_ReduceShootMoveScale" },
            { "True Hunter", "25_IncreaseATKBasedOnMarks" },
            { "Undying Blood", "26_DimonTalisman" },
            { "Strike Meister", "27_StrikeSkillMaster" },
            { "Mark of Courage", "28_CourageMark" },
            { "Rubinite", "29_Rubinite" },
            { "Talisman Toss", "X27_TalismanShooter" },
        };

        // SkillTreeData uniqueId display name -> English title. Used to rebuild the
        // AP item name when slot_data is unavailable.
        public static readonly Dictionary<string, string> SkillTitles = new Dictionary<string, string>
        {
            { "1_1", "Darts" },
            { "1_2", "Bloodblade Strike" },
            { "1_3", "Sunder Slash" },
            { "1_4", "Enhanced Slash" },
            { "2_1", "Dart Charge Accelerator" },
            { "2_2", "Rolling Recovery" },
            { "2_3", "Explosive Strike" },
            { "2_4", "Pureblood Heart" },
            { "2_5", "Sunder Slash - Enhanced" },
            { "2_6", "Enhanced Strike" },
            { "2_7", "Enhanced Heal" },
            { "3_1", "Enhanced Darts" },
            { "3_2", "Blood-Tipped Strike" },
            { "3_3", "Pureblood Heart" },
            { "3_4", "Enhanced Strike" },
            { "3_5", "Enhanced Heal" },
            { "3_6", "Critical Strike - Enhanced" },
            { "3_7", "Enhanced Slash" },
            { "4_1", "Pureblood Heart" },
            { "4_2", "Blood-Tipped Strike - Enhanced" },
            { "4_3", "Enhanced Heal" },
            { "4_4", "Explosive Strike - Enhanced" },
            { "4_5", "Bloodblade Strike - Enhanced" },
            { "4_6", "Pureblood Heart" },
        };

        public static string SkillItemName(string treeDisplayId)
        {
            if (string.IsNullOrEmpty(treeDisplayId))
                return null;
            if (!SkillTitles.TryGetValue(treeDisplayId, out var title))
                return null;
            return "Skill " + treeDisplayId.Replace('_', '-') + ": " + title;
        }

        public static BossInfo FindByLevel(string levelName)
        {
            if (string.IsNullOrEmpty(levelName)) return null;
            foreach (var boss in Bosses)
                if (boss.Level == levelName) return boss;
            return null;
        }

        public static BossInfo FindByKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var boss in Bosses)
                if (boss.Key == key) return boss;
            return null;
        }

        public static string LocationName(BossInfo boss, int index)
        {
            return boss.Name + ": " + CheckSuits[index];
        }
    }
}
