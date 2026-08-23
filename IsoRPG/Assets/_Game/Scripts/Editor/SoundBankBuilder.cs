using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using IsoRPG.Audio;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Собирает банк звуков из файлов в папке Audio.
    ///
    /// Раскладка по именам, а не вручную в инспекторе: наборов будет больше,
    /// звуков в них — тоже, и перетаскивать сорок клипов мышью каждый раз,
    /// когда что-то добавилось, никто не станет. Правило «имя решает, куда
    /// попадёт» переживает любое пополнение.
    /// </summary>
    public static class SoundBankBuilder
    {
        private const string AudioFolder = "Assets/_Game/Audio";
        private const string BankPath = "Assets/_Game/Data/SoundBank.asset";

        [MenuItem("Tools/IsoRPG/Собрать банк звуков", priority = 15)]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "В режиме Play ассеты не сохраняются на диск.", "Понятно");
                return;
            }

            var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);

            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<SoundBank>();
                AssetDatabase.CreateAsset(bank, BankPath);
            }

            bank.bladeHit   = Load("knifeSlice");
            bank.heavyHit   = Load("chop", "metalClick");
            bank.bowShot    = Load("drawKnife");          // сухой щелчок тетивы
            bank.drawWeapon = Load("beltHandle");
            bank.death      = Load("dropLeather", "cloth");

            bank.gold   = Load("handleCoins");
            bank.pickup = Load("handleSmallLeather");
            bank.equip  = Load("metalLatch", "clothBelt");
            bank.chewing = Load("chewing");

            bank.openWindow  = Load("bookOpen", "bookFlip");
            bank.closeWindow = Load("bookClose");
            bank.levelUp     = Load("jingles");

            // Только Impact-версии: набор footstep00-05 из RPG-пака звучит
            // деревянным стуком, а не шагом. Разница слышна сразу, и никакой
            // разброс тона её не спасает — это просто другой звук.
            bank.stepStone = Load("footstep_concrete");
            bank.stepGrass = Load("footstep_grass");

            // Скрип — голос нежити. Он и придуман под двери, но на костях
            // звучит точнее любого рычания: скелету нечем рычать.
            bank.boneVoice = Load("creak");

            bank.music = LoadMusic();

            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            int total = Count(bank);
            Debug.Log("[IsoRPG] Банк звуков собран: клипов " + total + ". " +
                      "Пересобери песочницу, чтобы он попал в сцену.");
        }

        /// <summary>Ассет банка — им пользуется сборщик сцены.</summary>
        public static SoundBank Load() => AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);

        // ------------------------------------------------------------------

        /// <summary>
        /// Все клипы, чьё имя начинается с одного из образцов.
        /// </summary>
        private static AudioClip[] Load(params string[] prefixes)
        {
            var found = new List<AudioClip>();

            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);

                if (!prefixes.Any(p => name.StartsWith(p, System.StringComparison.OrdinalIgnoreCase)))
                    continue;

                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null) found.Add(clip);
            }

            if (found.Count == 0)
                Debug.LogWarning("[IsoRPG] Не нашлось звуков по образцу: " + string.Join(", ", prefixes));

            return found.ToArray();
        }

        /// <summary>Все треки из папки музыки — порядок задаст плейлист.</summary>
        private static AudioClip[] LoadMusic()
        {
            var found = new List<AudioClip>();

            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip",
                                                          new[] { AudioFolder + "/Music" }))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (clip != null) found.Add(clip);
            }

            if (found.Count == 0)
                Debug.LogWarning("[IsoRPG] В папке музыки пусто — играть будет нечего.");

            return found.ToArray();
        }

        private static int Count(SoundBank bank)
        {
            int n = 0;

            foreach (var field in typeof(SoundBank).GetFields())
            {
                if (field.GetValue(bank) is AudioClip[] set) n += set.Length;
            }

            return n;
        }
    }
}
