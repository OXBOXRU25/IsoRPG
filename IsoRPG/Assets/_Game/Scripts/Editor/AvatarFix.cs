using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Возвращает персонажам скелет после перевода рига в Humanoid.
    ///
    /// Это починка моей же поломки, и она стоит того, чтобы записать причину.
    ///
    /// Перевод модели из Generic в Humanoid **пересоздаёт аватар**. Аватар —
    /// это отдельный объект внутри FBX, и все префабы ссылались на СТАРЫЙ.
    /// После переимпорта ссылка повисает: у Animator в поле аватара пусто
    /// или лежит негодный объект. Клипы при этом целы, контроллер цел,
    /// ошибок в журнале нет — просто никто не двигается. В игре это выглядит
    /// как «все едут по земле»: агент везёт тело, а поза не меняется.
    ///
    /// Ошибка тихая вдвойне: и компилятор молчит, и Unity молчит. Ловится
    /// только запуском — то есть заказчиком. Поэтому правило: **менять тип
    /// рига и не переназначить аватары — это одна работа, а не две.**
    ///
    /// Аватар ищем не по имени файла, а по мешу: у скиннера есть ссылка на
    /// свой меш, у меша — путь к FBX, в FBX лежит нужный аватар. Так не
    /// ошибёшься даже там, где префаб назван не как модель.
    /// </summary>
    public static class AvatarFix
    {
        [MenuItem("Tools/IsoRPG/Скелеты: вернуть аватары персонажам", priority = 72)]
        public static void Fix()
        {
            int prefabs = 0, scene = 0, missed = 0;

            // --- префабы ---
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                var root = PrefabUtility.LoadPrefabContents(path);
                bool touched = false;

                foreach (var animator in root.GetComponentsInChildren<Animator>(true))
                    if (Apply(animator, ref missed)) touched = true;

                if (touched)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    prefabs++;
                }

                PrefabUtility.UnloadPrefabContents(root);
            }

            // --- то, что стоит в сцене отдельными объектами ---
            foreach (var animator in Object.FindObjectsByType<Animator>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (Apply(animator, ref missed))
                {
                    EditorUtility.SetDirty(animator);
                    scene++;
                }
            }

            AssetDatabase.SaveAssets();

            Debug.Log("[IsoRPG] Аватары возвращены: префабов " + prefabs +
                      ", объектов в сцене " + scene +
                      (missed > 0 ? ", не нашлось аватара у " + missed : "") + ".");
        }

        private static bool Apply(Animator animator, ref int missed)
        {
            if (animator == null) return false;

            var avatar = FindAvatar(animator.transform);

            if (avatar == null)
            {
                // Молчать тут нельзя: именно молчаливый пропуск и есть та
                // самая тихая поломка, из-за которой всё это пишется.
                missed++;
                return false;
            }

            if (animator.avatar == avatar) return false;

            animator.avatar = avatar;
            return true;
        }

        /// <summary>Аватар из того FBX, откуда пришёл меш персонажа.</summary>
        private static Avatar FindAvatar(Transform root)
        {
            var seen = new HashSet<string>();

            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin.sharedMesh == null) continue;

                string path = AssetDatabase.GetAssetPath(skin.sharedMesh);

                if (string.IsNullOrEmpty(path) || !seen.Add(path)) continue;

                var avatar = AssetDatabase.LoadAllAssetsAtPath(path)
                                          .OfType<Avatar>()
                                          .FirstOrDefault();

                if (avatar != null && avatar.isValid) return avatar;
            }

            return null;
        }
    }
}
