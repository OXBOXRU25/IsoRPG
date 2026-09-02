using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using IsoRPG.Audio;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Раздаёт зверям голоса по виду.
    ///
    /// Павлон 02.09.2026: «у мелких кабанов, гриба и волков есть свои
    /// звуки?» Не было — молчали все, кроме главаря. Голоса он нагенерил
    /// сам, здесь мы их развешиваем.
    ///
    /// В его же карте звуков (`Войс/sound-list.md`) на каждого зверя заложено
    /// пять состояний: угроза, атака, урон, смерть и холостой звук. Угрозу
    /// подаёт мозг зверя при захвате цели, замах звучит из боевой системы, а
    /// урон и смерть до сих пор не звучали ни у кого — их вешает
    /// <see cref="CreatureVoice"/>. Пустой набор клипов это не ломает: звук
    /// может быть ещё не сгенерирован, и тогда зверь просто молчит.
    ///
    /// Вид определяем по имени существа, а не по префабу: сборщиков зверей
    /// несколько, а имя одно и то же во всех.
    /// </summary>
    public static class VoiceKit
    {
        private const string Arena = "Assets/_Game/Scenes/ArenaAuthor.unity";

        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().path != Arena)
                EditorSceneManager.OpenScene(Arena, OpenSceneMode.Single);

            var bank = SoundBankBuilder.Load();

            if (bank == null)
                Debug.LogWarning("[IsoRPG] Банка звуков нет — урон и смерть останутся без голоса.");

            int wolves = 0, boars = 0, bosses = 0, mushrooms = 0;

            foreach (var brain in Object.FindObjectsByType<IsoRPG.Combat.MonsterBrain>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (brain == null) continue;

                string name = brain.name;

                // Волк рычит часто — он рядовой и встречается стаей.
                // Кабан хрюкает ещё чаще: это фон места, а не событие.
                // Главарь рычит редко, чтобы рык оставался событием.
                if (name.StartsWith("Волк"))
                {
                    brain.GiveVoice(1, 9f);
                    Hurt(brain.gameObject, bank?.wolfHurt, bank?.wolfDeath);
                    wolves++;
                }
                else if (name.StartsWith("Кабан"))
                {
                    brain.GiveVoice(2, 7f);
                    Hurt(brain.gameObject, bank?.boarHurt, bank?.boarDeath);
                    boars++;
                }
                else if (name.Contains("Босс") || name.Contains("Вожак"))
                {
                    brain.GiveVoice(0, 14f);
                    Hurt(brain.gameObject, bank?.boarHurt, bank?.boarDeath);
                    bosses++;
                }
                else if (name.Contains("Гриб"))
                {
                    // Гриб не рычит при захвате цели: он уже прокричал, когда
                    // ожил из засады (см. AmbushSleeper). Ему нужен холостой
                    // звук — чтобы стоящий рядом гриб было слышно.
                    Idle(brain.gameObject, bank?.mushroomIdle);
                    Hurt(brain.gameObject, bank?.mushroomHurt, bank?.mushroomDeath);
                    mushrooms++;
                }
                else continue;

                EditorUtility.SetDirty(brain);
            }

            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();

            Debug.Log($"[IsoRPG] Голоса розданы: волков {wolves}, кабанов {boars}, " +
                      $"главарей {bosses}, грибов {mushrooms}.");
        }

        /// <summary>Голос на урон и на смерть.</summary>
        private static void Hurt(GameObject beast, AudioClip[] hurt, AudioClip[] death)
        {
            var voice = beast.GetComponent<CreatureVoice>();
            if (voice == null) voice = beast.AddComponent<CreatureVoice>();

            voice.Setup(hurt, death);
            EditorUtility.SetDirty(voice);
        }

        /// <summary>Холостой звук: существо слышно ещё до того, как его видно.</summary>
        private static void Idle(GameObject beast, AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return;

            var voice = beast.GetComponent<AmbientVoice>();
            if (voice == null) voice = beast.AddComponent<AmbientVoice>();

            voice.Setup(clips);
            EditorUtility.SetDirty(voice);
        }
    }
}
