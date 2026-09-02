using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Раздаёт зверям голоса по виду.
    ///
    /// Павлон 02.09.2026: «у мелких кабанов, гриба и волков есть свои
    /// звуки?» Не было — молчали все, кроме главаря. Голоса он нагенерил
    /// сам, здесь мы их развешиваем.
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

            int wolves = 0, boars = 0, bosses = 0;

            foreach (var brain in Object.FindObjectsByType<IsoRPG.Combat.MonsterBrain>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (brain == null) continue;

                string name = brain.name;

                // Волк рычит часто — он рядовой и встречается стаей.
                // Кабан хрюкает ещё чаще: это фон места, а не событие.
                // Главарь рычит редко, чтобы рык оставался событием.
                if (name.StartsWith("Волк")) { brain.GiveVoice(1, 9f); wolves++; }
                else if (name.StartsWith("Кабан")) { brain.GiveVoice(2, 7f); boars++; }
                else if (name.Contains("Босс") || name.Contains("Вожак")) { brain.GiveVoice(0, 14f); bosses++; }
                else continue;

                EditorUtility.SetDirty(brain);
            }

            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();

            Debug.Log($"[IsoRPG] Голоса розданы: волков {wolves}, кабанов {boars}, главарей {bosses}.");
        }
    }
}
