using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Печатает, из каких мешей собран герой в сцене и из каких — манекен.
    ///
    /// Заведён 04.09.2026, когда Павлон сказал: «ты это уже 1000 раз путаешь,
    /// у нашего героя другая модель». Он прав: манекен собирается из
    /// Player.prefab, а тот основан на SM_Chr_Commoner_Male_01. Что стоит на
    /// живом герое — надо не помнить, а прочитать.
    /// </summary>
    public static class HeroModelProbe
    {
        public static void Run()
        {
            // Открываем ИГРОВУЮ сцену явно.
            //
            // В проекте две арены, и в сборку идёт `ArenaAuthor`, а пакетный
            // прогон по умолчанию открывает `Arena`. Первый прогон этого щупа
            // читал сцену, которой в игре нет, — и его ответ ничего не
            // доказывал, хотя выглядел уверенно.
            const string arena = "Assets/_Game/Scenes/ArenaAuthor.unity";

            if (EditorSceneManager.GetActiveScene().path != arena)
                EditorSceneManager.OpenScene(arena, OpenSceneMode.Single);

            Debug.Log($"[IsoRPG] Смотрю сцену: {EditorSceneManager.GetActiveScene().path}");

            foreach (var name in new[] { "Player", "Манекен" })
            {
                var go = GameObject.Find(name);

                if (go == null)
                {
                    Debug.LogWarning($"[IsoRPG] «{name}»: в сцене нет.");
                    continue;
                }

                Debug.Log($"[IsoRPG] === {name} ===");

                var skins = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);

                foreach (var skin in skins.Take(20))
                {
                    string mesh = skin.sharedMesh != null ? skin.sharedMesh.name : "нет меша";
                    string mat = skin.sharedMaterial != null ? skin.sharedMaterial.name : "нет материала";

                    Debug.Log($"[IsoRPG]   {skin.name}: меш {mesh}, материал {mat}");
                }

                Debug.Log($"[IsoRPG]   всего скелетных мешей: {skins.Length}");

                var animator = go.GetComponentInChildren<Animator>();

                if (animator != null)
                    Debug.Log($"[IsoRPG]   аниматор: {(animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "нет")}, аватар {(animator.avatar != null ? animator.avatar.name : "нет")}");
            }

            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player.prefab");

            if (source != null)
            {
                var skins = source.GetComponentsInChildren<SkinnedMeshRenderer>(true);

                Debug.Log($"[IsoRPG] === Player.prefab (из него делается манекен) ===");

                foreach (var skin in skins.Take(20))
                    Debug.Log($"[IsoRPG]   {skin.name}: меш {(skin.sharedMesh != null ? skin.sharedMesh.name : "нет")}");
            }
        }
    }
}
