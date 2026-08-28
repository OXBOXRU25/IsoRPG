using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Небо от Synty вместо COZY.
    ///
    /// COZY даёт идущие сутки и погоду, но она из другого художественного
    /// языка: у неё небо мягкое и «фотографическое», а весь наш мир —
    /// гранёный low-poly от Synty. Заказчик попросил держать один язык.
    ///
    /// <b>Что теряем, вслух:</b> ход времени, ночь, погодные переходы.
    /// Skybox Synty — это статичный градиент с облаками, нарисованными в их
    /// стиле. Свет ведёт наше собственное солнце, теми числами, что стояли
    /// до COZY: закат под 21 градусом, длинные тени.
    /// </summary>
    public static class SyntySky
    {
        private const string Folder = "Assets/PolygonNatureBiomes";

        /// <param name="which">
        /// Имя материала без пути: Skybox_Mat_01, Skybox_Meadows_Mat_01 и т.д.
        /// </param>
        public static void Apply(string which = "Skybox_Mat_01")
        {
            // Сначала снимаем COZY: пока купол висит, он ведёт небо сам, и
            // подмена материала не даст ничего видимого.
            CozySky.Remove();

            string[] found = AssetDatabase.FindAssets("t:Material " + which)
                                          .Select(AssetDatabase.GUIDToAssetPath)
                                          .Where(p => p.StartsWith(Folder))
                                          .ToArray();

            if (found.Length == 0)
            {
                Debug.LogError("[IsoRPG] Небо Synty «" + which + "» не найдено в " +
                               Folder + ". Набор биомов не установлен?");
                return;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(found[0]);

            if (mat == null)
            {
                Debug.LogError("[IsoRPG] Материал не загрузился: " + found[0]);
                return;
            }

            RenderSettings.skybox = mat;

            // Рассеянный свет берём с неба: иначе тени останутся окрашены
            // под прежнее небо, и подмена будет читаться как ошибка цвета.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            DynamicGI.UpdateEnvironment();

            Debug.Log("[IsoRPG] Небо Synty: " + found[0] +
                      ". Рассеянный свет пересчитан с него. " +
                      "Суток и погоды больше нет — небо статичное.");

            EditorSceneManager.MarkAllScenesDirty();
        }

        /// <summary>Какие небеса вообще есть в наборе — чтобы выбирать по списку.</summary>
        public static void List()
        {
            var all = AssetDatabase.FindAssets("t:Material Skybox")
                                   .Select(AssetDatabase.GUIDToAssetPath)
                                   .Where(p => p.StartsWith(Folder))
                                   .OrderBy(p => p);

            Debug.Log("[IsoRPG] Небеса Synty в наборе:\n  " + string.Join("\n  ", all));
        }
    }
}
