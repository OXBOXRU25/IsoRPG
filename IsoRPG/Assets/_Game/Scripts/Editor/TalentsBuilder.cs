using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using IsoRPG.Progression;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Создаёт дерево талантов: три ветки по шесть, три яруса.
    ///
    /// Числа собраны так, чтобы каждая ветка отвечала на свой вопрос. Убийство
    /// повышает потолок урона (криты и добивания) — играть от удачных ударов.
    /// Бой повышает пол (скорость, здоровье, броня) — играть на выносливость.
    /// Скрытность усиливает подход и первый удар — играть от засады.
    ///
    /// Ярусы стоят 0, 3 и 6 очков в ветке. При 60 уровнях это 59 очков —
    /// хватает открыть две ветки до конца, но не три. Так и задумано: выбор
    /// должен что-то стоить.
    /// </summary>
    public static class TalentsBuilder
    {
        private const string Folder = "Assets/_Game/Data/Talents";

        [MenuItem("Tools/IsoRPG/Создать таланты", priority = 34)]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "В режиме Play ассеты не сохраняются на диск.", "Понятно");
                return;
            }

            EnsureFolder(Folder);

            int made = 0;

            foreach (var row in Rows())
            {
                if (Create(row)) made++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[IsoRPG] Таланты: создано новых " + made +
                      ", всего в дереве " + Rows().Count + ".");
        }

        /// <summary>Все таланты дерева по порядку. Нужно окну и сборщику сцены.</summary>
        public static List<TalentDefinition> LoadAll()
        {
            var list = new List<TalentDefinition>();

            foreach (var row in Rows())
            {
                var talent = AssetDatabase.LoadAssetAtPath<TalentDefinition>(
                    Folder + "/" + row.file + ".asset");

                if (talent != null) list.Add(talent);
            }

            return list;
        }

        // ------------------------------------------------------------------

        private struct Row
        {
            public string file;
            public string name;
            public string text;
            public TalentBranch branch;
            public int row;
            public int column;
            public int maxRank;
            public TalentEffect effect;
            public float value;
        }

        private static List<Row> Rows() => new List<Row>
        {
            // --- Убийство: потолок урона --------------------------------
            Make("TL_Precision", "Точность", "Рука не дрожит, и удар приходится туда, куда смотрел глаз.",
                 TalentBranch.Assassination, 0, 0, 3, TalentEffect.CritChance, 0.02f),

            Make("TL_DeadlyStrike", "Смертельный удар", "Клинок входит глубже, чем рассчитывал носивший доспех.",
                 TalentBranch.Assassination, 0, 1, 3, TalentEffect.Damage, 0.03f),

            Make("TL_Bleeding", "Кровопускание", "Приёмы оставляют раны, которые не закрываются сами.",
                 TalentBranch.Assassination, 1, 0, 3, TalentEffect.AbilityDamage, 0.05f),

            Make("TL_ColdBlood", "Хладнокровие", "Ни спешки, ни злости — только выбор места для удара.",
                 TalentBranch.Assassination, 1, 1, 2, TalentEffect.CritChance, 0.03f),

            Make("TL_Bloodthirst", "Жажда крови", "Чем дольше длится бой, тем меньше остаётся сомнений.",
                 TalentBranch.Assassination, 2, 0, 2, TalentEffect.CritMultiplier, 0.15f),

            Make("TL_Execution", "Казнь", "Последний удар наносится не в горячке, а спокойно.",
                 TalentBranch.Assassination, 2, 1, 1, TalentEffect.AbilityDamage, 0.12f),

            // --- Бой: пол урона и живучесть -----------------------------
            Make("TL_Swiftness", "Ловкость рук", "Клинок возвращается раньше, чем противник успевает выдохнуть.",
                 TalentBranch.Combat, 0, 0, 3, TalentEffect.AttackSpeed, 0.05f),

            Make("TL_Evasion", "Уклонение", "Удар проходит по касательной, а не по рёбрам.",
                 TalentBranch.Combat, 0, 1, 3, TalentEffect.Armor, 3f),

            Make("TL_Toughness", "Живучесть", "Тело привыкло к тому, что его бьют.",
                 TalentBranch.Combat, 1, 0, 3, TalentEffect.MaxHealth, 0.05f),

            Make("TL_Brutality", "Жестокость", "Каждый удар весит чуть больше, чем должен.",
                 TalentBranch.Combat, 1, 1, 3, TalentEffect.Damage, 0.03f),

            Make("TL_Parry", "Парирование", "Чужой клинок встречает свой, а не плечо.",
                 TalentBranch.Combat, 2, 0, 2, TalentEffect.Armor, 5f),

            Make("TL_BladeFlurry", "Клинковый вихрь", "Руки движутся быстрее, чем взгляд успевает за ними.",
                 TalentBranch.Combat, 2, 1, 1, TalentEffect.AttackSpeed, 0.1f),

            // --- Скрытность: подход и первый удар ------------------------
            Make("TL_Camouflage", "Мастер маскировки", "Тень движется так же быстро, как и тот, кто идёт в открытую.",
                 TalentBranch.Subtlety, 0, 0, 3, TalentEffect.StealthSpeed, 0.08f),

            Make("TL_Silence", "Бесшумность", "Дыхание ровное, шаги не считаются.",
                 TalentBranch.Subtlety, 0, 1, 3, TalentEffect.EnergyRegen, 0.06f),

            Make("TL_Ambush", "Засада", "Тот, кто не увидел удара, не успел и напрячься.",
                 TalentBranch.Subtlety, 1, 0, 3, TalentEffect.StealthDamage, 0.08f),

            Make("TL_Slip", "Ускользание", "Уйти из-под удара проще, чем его выдержать.",
                 TalentBranch.Subtlety, 1, 1, 3, TalentEffect.Armor, 2f),

            Make("TL_Shadow", "Тень", "Из темноты бьют не рукой, а самой темнотой.",
                 TalentBranch.Subtlety, 2, 0, 2, TalentEffect.StealthDamage, 0.1f),

            Make("TL_Vanish", "Растворение", "Силы возвращаются быстрее, чем противник понимает, куда все делись.",
                 TalentBranch.Subtlety, 2, 1, 1, TalentEffect.EnergyRegen, 0.15f),
        };

        private static Row Make(string file, string name, string text, TalentBranch branch,
                                int row, int column, int maxRank, TalentEffect effect, float value)
            => new Row
            {
                file = file, name = name, text = text, branch = branch,
                row = row, column = column, maxRank = maxRank,
                effect = effect, value = value,
            };

        private static bool Create(Row row)
        {
            string path = Folder + "/" + row.file + ".asset";

            var existing = AssetDatabase.LoadAssetAtPath<TalentDefinition>(path);
            if (existing != null)
            {
                // Существующий не трогаем: правки Павла в балансе должны
                // переживать пересборку. Обновляем только иконку, если она
                // появилась после создания.
                BindIcon(existing, row.file);
                return false;
            }

            var talent = ScriptableObject.CreateInstance<TalentDefinition>();
            talent.displayName = row.name;
            talent.description = row.text;
            talent.branch = row.branch;
            talent.row = row.row;
            talent.column = row.column;
            talent.maxRank = row.maxRank;
            talent.effect = row.effect;
            talent.valuePerRank = row.value;

            AssetDatabase.CreateAsset(talent, path);
            BindIcon(talent, row.file);

            return true;
        }

        /// <summary>
        /// Иконка по имени файла таланта. Пусто — не беда: окно нарисует
        /// плашку цветом ветки, и дерево работает без единой картинки.
        /// </summary>
        private static void BindIcon(TalentDefinition talent, string file)
        {
            if (talent.icon != null) return;

            const string folder = "Assets/_Game/Art/UI/Icons/Talents";
            if (!AssetDatabase.IsValidFolder(folder)) return;

            IconBinder.PrepareSprites(folder);

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(folder + "/" + file + ".png");
            if (sprite == null) return;

            talent.icon = sprite;
            EditorUtility.SetDirty(talent);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = folder.Substring(0, folder.LastIndexOf('/'));
            string leaf = folder.Substring(folder.LastIndexOf('/') + 1);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
